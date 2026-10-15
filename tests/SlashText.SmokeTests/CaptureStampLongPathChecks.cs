using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using SlashText.Services;

internal static class CaptureStampLongPathChecks
{
    public static void Run(string? basePath = null)
    {
        var parent = Path.GetFullPath(basePath ?? Path.Combine(Path.GetTempPath(), "slashdesk-stamp-longpath-checks"));
        Directory.CreateDirectory(parent);
        var owned = Path.GetFullPath(Path.Combine(parent, "run-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(owned);
        try
        {
            LongPaths(owned);
            FailedManifestReplacement(owned);
            Console.WriteLine("Custom stamp long paths/Unicode/normalization/reload and locked-manifest rollback: PASS");
        }
        finally
        {
            // Delete only this run's fresh owned directory, never the caller's base.
            var prefix = Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar;
            Check(owned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(owned, parent, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(owned).StartsWith("run-", StringComparison.Ordinal), "Cleanup target stays inside the owned test base");
            if (Directory.Exists(owned)) Directory.Delete(owned, recursive: true);
        }
    }

    private static void LongPaths(string root)
    {
        var input = LongDirectory(root, "origem com espaços çãõ");
        var destination = LongDirectory(root, "coleção Meus emojis ação");
        var png = Path.Combine(input, "Marca transparente ação ç.png");
        var jpeg = Path.Combine(input, "Retrato azul com espaços é.jpg");
        var small = Path.Combine(input, "Pequena imagem ü.png");
        WritePng(png, 1200, 600, transparentBorder: true);
        WriteJpeg(jpeg, 600, 1200);
        WritePng(small, 32, 20, transparentBorder: false);
        Check(new[] { png, jpeg, small, destination }.All(path => Path.GetFullPath(path).Length > 260),
            "Source files and catalog directory really exceed 260 characters");
        Console.WriteLine($"Custom stamp fixture path lengths: PNG={png.Length}; JPEG={jpeg.Length}; collection={destination.Length}; all exceed 260 characters");

        var catalog = new CaptureStampCatalog(destination);
        var first = catalog.Import(png);
        Check(catalog.Items.Single().Name == "Marca transparente ação ç", "Long-path import keeps Unicode filename as label");
        CheckImage(catalog, first, 512, 256, transparentBorder: true, blue: false);
        File.Delete(png);
        var firstReload = new CaptureStampCatalog(destination);
        Check(firstReload.Items.Single().Value == first, "Long-path PNG survives reload and original removal");
        CheckImage(firstReload, first, 512, 256, transparentBorder: true, blue: false);
        CaptureStampCatalog.ValidateDirectory(destination);
        var beforeJpeg = Snapshot(destination);
        var entriesBeforeJpeg = Entries(destination);

        var second = firstReload.Import(jpeg, "Retrato azul — ação");
        CheckImage(firstReload, second, 256, 512, transparentBorder: false, blue: true);
        CheckPreviousAssets(beforeJpeg, destination);
        CheckPreviousEntries(entriesBeforeJpeg, destination, expectedCount: 2);
        var beforeSmall = Snapshot(destination);
        var entriesBeforeSmall = Entries(destination);
        var third = firstReload.Import(small);
        CheckImage(firstReload, third, 32, 20, transparentBorder: false, blue: false);
        CheckPreviousAssets(beforeSmall, destination);
        CheckPreviousEntries(entriesBeforeSmall, destination, expectedCount: 3);
        File.Delete(jpeg); File.Delete(small);

        var reloaded = new CaptureStampCatalog(destination);
        Check(reloaded.Items.Select(item => item.Value).ToHashSet().SetEquals([first, second, third]),
            "All long-path imports retain their identities after a fresh catalog load");
        Check(reloaded.Items.Single(item => item.Value == second).Name == "Retrato azul — ação", "Explicit Unicode label survives serialization");
        CheckImage(reloaded, second, 256, 512, transparentBorder: false, blue: true);
        CheckImage(reloaded, third, 32, 20, transparentBorder: false, blue: false);
        CaptureStampCatalog.ValidateDirectory(destination);
        foreach (var item in reloaded.Items)
        {
            var assetPath = Path.Combine(destination, item.Value[7..] + ".png");
            Check(assetPath.Length > 260 && File.Exists(assetPath), "Normalized asset destination exceeds 260 characters and exists");
            using var stream = File.OpenRead(assetPath);
            using var decoded = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
            Check(decoded.RawFormat.Guid == ImageFormat.Png.Guid && decoded.Width <= 512 && decoded.Height <= 512,
                "Every persisted long-path asset is normalized PNG within the 512-pixel bound");
        }
        Check(!Directory.EnumerateFiles(destination, "*.tmp").Any(), "Successful imports leave no manifest temporaries");
    }

    private static void FailedManifestReplacement(string root)
    {
        // Keep this destination short so the original long-path defect cannot mask
        // the independent failure after the PNG has already been written.
        var directory = Path.Combine(root, "rollback collection ç");
        Check(Path.Combine(directory, new string('a', 32) + ".png").Length < 260,
            "Use a shorter test base for the independent locked-manifest case; long-path coverage has its own nested directory");
        var seed = Path.Combine(root, "seed.png"); var next = Path.Combine(root, "next.png");
        WritePng(seed, 32, 20, transparentBorder: false);
        WritePng(next, 1200, 600, transparentBorder: true);
        var catalog = new CaptureStampCatalog(directory);
        var original = catalog.Import(seed, "Anterior ç");
        var manifest = Path.Combine(directory, "catalog.json");
        // Unrelated files must survive cleanup; deleting all *.tmp would be wrong.
        File.WriteAllText(Path.Combine(directory, "keep-existing.tmp"), "owned fictitious sentinel");
        File.WriteAllText(Path.Combine(directory, "keep-existing.bin"), "owned fictitious unrelated file");
        var before = Snapshot(directory);
        var oldEntries = Entries(directory);
        Exception? failure = null;
        // Readers remain permitted. Denying write/delete makes the final atomic
        // replacement fail without ACL changes, deleting or corrupting the index.
        using (var held = new FileStream(manifest, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { catalog.Import(next, "Não deve entrar"); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { failure = exception; }
            Check(failure is not null, "Locked manifest replacement must report failure");
            // Win32 can report access denied without a filename. The only changed
            // condition is this manifest lock; byte-identical rollback and a
            // successful retry of the same source/directory after unlock establish
            // causality without localized messages, JIT stack frames or watcher timing.
            Console.WriteLine($"Observed controlled failure: {failure!.GetType().Name}; HResult=0x{failure.HResult:X8}");
            Check(failure.HResult == unchecked((int)0x80070020) || failure.HResult == unchecked((int)0x80070005),
                "Controlled manifest lock reports native sharing violation or access denied");
            CheckSnapshot(before, directory, "Failed import removes only its new PNG and temporary, preserving every pre-existing byte");
            CheckPreviousEntries(oldEntries, directory, expectedCount: 1);
            Check(catalog.Items.Single().Value == original, "Failed import leaves the in-memory catalog view consistent");
            CheckImage(catalog, original, 32, 20, transparentBorder: false, blue: false);
            CaptureStampCatalog.ValidateDirectory(directory);
        }
        var added = catalog.Import(next, "Depois do desbloqueio");
        Check(catalog.Items.Count == 2 && added != original, "The same catalog can import again after releasing the manifest lock");
        CheckPreviousAssets(before, directory);
        CheckPreviousEntries(oldEntries, directory, expectedCount: 2);
        CheckImage(catalog, added, 512, 256, transparentBorder: true, blue: false);
        var reloaded = new CaptureStampCatalog(directory);
        Check(reloaded.Items.Select(item => item.Value).ToHashSet().SetEquals([original, added]), "Only the successful retry is present after reload");
        CaptureStampCatalog.ValidateDirectory(directory);
        Check(Directory.EnumerateFiles(directory, "*.tmp").Select(Path.GetFileName).SequenceEqual(["keep-existing.tmp"]),
            "Rollback and retry preserve the unrelated temporary and leave no new temporary");
    }

    private static string LongDirectory(string root, string name)
    {
        var directory = Path.Combine(root, name);
        while (directory.Length <= 280) directory = Path.Combine(directory, "segmento fictício com espaços ção");
        Directory.CreateDirectory(directory); return directory;
    }

    private static void WritePng(string path, int width, int height, bool transparentBorder)
    {
        using var source = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(source))
        {
            graphics.Clear(transparentBorder ? Color.Transparent : Color.Red);
            if (transparentBorder) graphics.FillRectangle(Brushes.Red, width / 4, height / 4, width / 2, height / 2);
        }
        // GDI+ Save(filename) is precisely the affected path: fixture creation must
        // use .NET's stream so it does not fail before exercising catalog.Import.
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.Save(output, ImageFormat.Png);
    }

    private static void WriteJpeg(string path, int width, int height)
    {
        using var source = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(source)) graphics.Clear(Color.Blue);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.Save(output, ImageFormat.Jpeg);
    }

    private static void CheckImage(CaptureStampCatalog catalog, string value, int width, int height, bool transparentBorder, bool blue)
    {
        using var bitmap = catalog.GetImage(value)!.CreateBitmap();
        Check(bitmap.Width == width && bitmap.Height == height, "Normalization preserves aspect ratio, 512 bound and small image dimensions");
        var center = bitmap.GetPixel(width / 2, height / 2);
        Check(center.A == 255 && (blue ? center.B > 240 && center.R < 20 && center.G < 20 : center.R > 240 && center.G < 20 && center.B < 20),
            "Normalized center pixel retains the generated fixture color");
        Check(transparentBorder ? bitmap.GetPixel(0, 0).A == 0 : bitmap.GetPixel(0, 0).A == 255,
            "PNG alpha and JPEG opacity survive normalization");
    }

    private static Dictionary<string, byte[]> Snapshot(string directory) => Directory.EnumerateFiles(directory)
        .ToDictionary(path => Path.GetFileName(path), File.ReadAllBytes, StringComparer.Ordinal);

    private static Dictionary<string, string> Entries(string directory)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "catalog.json")));
        return document.RootElement.EnumerateArray().ToDictionary(entry => entry.GetProperty("Id").GetString()!, entry => entry.GetRawText(), StringComparer.Ordinal);
    }

    private static void CheckPreviousEntries(Dictionary<string, string> previous, string directory, int expectedCount)
    {
        var current = Entries(directory);
        Check(current.Count == expectedCount && previous.All(entry => current.TryGetValue(entry.Key, out var raw) && raw == entry.Value),
            "Existing manifest entries retain identity, label and serialized values");
    }

    private static void CheckPreviousAssets(Dictionary<string, byte[]> previous, string directory)
    {
        foreach (var file in previous.Where(entry => entry.Key != "catalog.json"))
            Check(File.Exists(Path.Combine(directory, file.Key)) && File.ReadAllBytes(Path.Combine(directory, file.Key)).SequenceEqual(file.Value),
                "Existing collection files remain byte-identical after a new successful import");
    }

    private static void CheckSnapshot(Dictionary<string, byte[]> previous, string directory, string message)
    {
        var current = Snapshot(directory);
        Check(current.Count == previous.Count && previous.All(file => current.TryGetValue(file.Key, out var bytes) && bytes.SequenceEqual(file.Value)), message);
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Custom stamp long-path smoke: " + message); }
}
