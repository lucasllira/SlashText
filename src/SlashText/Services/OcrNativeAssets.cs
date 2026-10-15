using System.IO;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Tesseract;

namespace SlashText.Services;

// Hash identity is an allowlist, not proof of an upstream patch level or a sandbox.
internal sealed class OcrNativeAssets : IDisposable
{
    private readonly LocalFileBoundary _boundary;
    private readonly List<FileStream> _files = [];
    private readonly List<IntPtr> _modules = [];
    internal string DirectoryPath { get; }
    internal string TesseractVersion { get; private set; } = "";
    internal string LeptonicaVersion { get; private set; } = "";
    private OcrNativeAssets(string directory)
    {
        DirectoryPath = Path.GetFullPath(directory);
        _boundary = LocalFileBoundary.PinDirectory(Path.Combine(DirectoryPath, "x64"));
    }

    internal static OcrNativeAssets Acquire(string? directory = null)
    {
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("OCR requires Windows x64.");
        var lease = new OcrNativeAssets(directory ?? FindDirectory());
        try
        {
            // Verify every file before any of them is loaded, retaining handles through OCR.
            lease.Verify("leptonica-1.82.0.dll", 4168192, "dfcb3e6ed0b16bc55bfdbcf53543cfe42a354b87c3e35bd3a95eebf005d73e76");
            lease.Verify("tesseract50.dll", 2788352, "de4d04ec75095374d98f5dd7a60d14d7e2e0f76589db693eccf7ae658be8cb2b");
            var leptonica = lease.Load("leptonica-1.82.0.dll");
            var tesseract = lease.Load("tesseract50.dll");
            foreach (var name in new[] { "pixReadMem", "pixDestroy", "getLeptonicaVersion", "lept_free" }) NativeLibrary.GetExport(leptonica, name);
            foreach (var name in new[] { "TessVersion", "TessBaseAPICreate", "TessBaseAPIInit3", "TessBaseAPIGetUTF8Text", "TessBaseAPIEnd", "TessBaseAPIDelete" }) NativeLibrary.GetExport(tesseract, name);
            lease.TesseractVersion = Marshal.PtrToStringUTF8(Marshal.GetDelegateForFunctionPointer<VersionFunction>(NativeLibrary.GetExport(tesseract, "TessVersion"))()) ?? "";
            var version = Marshal.GetDelegateForFunctionPointer<VersionFunction>(NativeLibrary.GetExport(leptonica, "getLeptonicaVersion"))();
            try { lease.LeptonicaVersion = Marshal.PtrToStringUTF8(version) ?? ""; }
            finally { Marshal.GetDelegateForFunctionPointer<FreeFunction>(NativeLibrary.GetExport(leptonica, "lept_free"))(version); }
            if (lease.TesseractVersion != "5.0.0" || !lease.LeptonicaVersion.StartsWith("leptonica-1.82.0", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected native OCR version.");
            TesseractEnviornment.CustomSearchPath = lease.DirectoryPath;
            return lease;
        }
        catch { lease.Dispose(); throw; }
    }

    private static string FindDirectory()
    {
        // CLR-provided paths support both compiled output and extracted single-file apps.
        // Never search PATH, the working directory or a caller's model directory.
        var roots = new List<string> { AppContext.BaseDirectory };
#pragma warning disable IL3000
        if (Path.GetDirectoryName(typeof(TesseractEngine).Assembly.Location) is { Length: > 0 } assemblyRoot) roots.Add(assemblyRoot);
#pragma warning restore IL3000
        if (AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") is string nativeRoots)
            roots.AddRange(nativeRoots.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Take(16));
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            if (File.Exists(Path.Combine(root, "x64", "leptonica-1.82.0.dll")) || File.Exists(Path.Combine(root, "x64", "tesseract50.dll"))) return root;
        throw new DllNotFoundException("Approved OCR native files unavailable.");
    }

    private void Verify(string name, long length, string hash)
    {
        var file = LocalFileBoundary.OpenFile(Path.Combine(DirectoryPath, "x64", name), writable: false, denyWrites: true);
        _files.Add(file);
        if (file.Length != length || Convert.ToHexStringLower(SHA256.HashData(file)) != hash)
            throw new InvalidDataException("Unapproved OCR native file.");
        file.Position = 0;
        using var pe = new PEReader(file, PEStreamOptions.LeaveOpen);
        if (pe.PEHeaders.CoffHeader.Machine != Machine.Amd64) throw new InvalidDataException("Incorrect OCR native architecture.");
    }

    private IntPtr Load(string name)
    {
        // Absolute approved DLL, with transitive dependencies restricted to System32.
        // Leptonica is preloaded before Tesseract. No current-directory/PATH fallback.
        var module = LoadLibraryExW(Path.Combine(DirectoryPath, "x64", name), IntPtr.Zero, 0x800);
        if (module == IntPtr.Zero) throw new DllNotFoundException("OCR native dependency unavailable.");
        _modules.Add(module);
        return module;
    }

    public void Dispose()
    {
        for (var index = _modules.Count - 1; index >= 0; index--) NativeLibrary.Free(_modules[index]);
        _modules.Clear();
        foreach (var file in _files) file.Dispose();
        _files.Clear();
        _boundary.Dispose();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr VersionFunction();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeFunction(IntPtr value);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryExW(string path, IntPtr reserved, uint flags);
}
