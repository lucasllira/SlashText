using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace SlashText.Services;

// Verification and use share the same pinned directory and read-only file handles.
// Native code opens these paths again; deny writes/deletion until the engine is disposed.
internal sealed class OcrModelLease : IDisposable
{
    private readonly LocalFileBoundary _boundary;
    private readonly List<FileStream> _files = [];
    internal string DirectoryPath { get; }
    private OcrModelLease(string directory)
    {
        DirectoryPath = Path.GetFullPath(directory);
        _boundary = LocalFileBoundary.PinDirectory(DirectoryPath);
    }

    internal static OcrModelLease Acquire(string model, string? alternateDirectory = null)
    {
        var assets = Assets(model);
        var directory = alternateDirectory is not null ? Path.GetFullPath(alternateDirectory) :
            Path.Combine(AppPaths.DataDirectory, "OcrModels-v1", model == "Fast" ? "fast-87416418" : "best-e12c65a9");
        if (alternateDirectory is null)
        {
            using var pin = LocalFileBoundary.PinDirectory(directory, create: true);
            // Serialize only cold-cache restoration. Warm readers share their immutable files.
            using var writer = AcquireWriter(directory);
            foreach (var asset in assets) Restore(directory, model, asset);
            return OpenVerified(directory, assets);
        }
        // Alternate roots are never trusted by location, including direct worker arguments.
        return OpenVerified(directory, assets);
    }

    private static OcrModelLease OpenVerified(string directory, Asset[] assets)
    {
        var lease = new OcrModelLease(directory);
        try
        {
            foreach (var asset in assets)
            {
                var file = LocalFileBoundary.OpenFile(Path.Combine(directory, asset.Language + ".traineddata"), writable: false, denyWrites: true);
                lease._files.Add(file);
                if (!Matches(file, asset)) throw new InvalidDataException("Unapproved OCR model.");
            }
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private static FileStream AcquireWriter(string directory)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try { return LocalFileBoundary.OpenFile(Path.Combine(directory, "models-v1.lock"), create: true, exclusive: true); }
            catch (IOException) when (timer.Elapsed < TimeSpan.FromSeconds(5)) { Thread.Sleep(20); }
        }
    }

    private static void Restore(string directory, string model, Asset asset)
    {
        var destination = Path.Combine(directory, asset.Language + ".traineddata");
        try
        {
            using var existing = LocalFileBoundary.OpenFile(destination, writable: false, denyWrites: true);
            if (Matches(existing, asset)) return;
        }
        catch (FileNotFoundException) { }
        var temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    "SlashText.Assets.Ocr." + (model == "Fast" ? "Fast." : "") + asset.Language + ".traineddata")
                    ?? throw new FileNotFoundException("Embedded OCR model unavailable.");
                resource.CopyTo(output);
                output.Flush(true);
            }
            using (var check = LocalFileBoundary.OpenFile(temporary, writable: false, denyWrites: true))
                if (!Matches(check, asset)) throw new InvalidDataException("Embedded OCR model mismatch.");
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (created && File.Exists(temporary))
            {
                using (LocalFileBoundary.OpenFile(temporary, exclusive: true)) { }
                File.Delete(temporary);
            }
        }
    }

    private static bool Matches(FileStream file, Asset asset) => file.Length == asset.Length &&
        Convert.ToHexStringLower(SHA256.HashData(file)) == asset.Hash;

    private sealed record Asset(string Language, long Length, string Hash);
    private static Asset[] Assets(string model) => model switch
    {
        "Fast" => [
            new("eng", 4113088, "7d4322bd2a7749724879683fc3912cb542f19906c83bcc1a52132556427170b2"),
            new("por", 1982756, "c4932b937207a9514b7514d518b931a99938c02a28a5a5a553f8599ed58b7deb")],
        "Best" => [
            new("eng", 15400601, "8280aed0782fe27257a68ea10fe7ef324ca0f8d85bd2fd145d1c2b560bcb66ba"),
            new("por", 8159939, "711de9dbb8052067bd42f16b9119967f30bada80d57e2ef24f65d09f531adb04")],
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };

    public void Dispose()
    {
        foreach (var file in _files) file.Dispose();
        _files.Clear();
        _boundary.Dispose();
    }
}
