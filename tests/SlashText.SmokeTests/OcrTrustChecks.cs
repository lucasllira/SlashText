using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using SlashText.Models;
using SlashText.Services;

internal static class OcrTrustChecks
{
    private static int _checks;
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("OCR trust: " + message); _checks++; }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { _checks++; return; }
        throw new InvalidOperationException("OCR trust accepted: " + message);
    }

    internal static async Task RunAsync(string root, bool native, string? baseline = null)
    {
        Directory.CreateDirectory(root);
        var previous = AppPaths.Current;
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, Path.Combine(root, "data"), root, true));
        try
        {
            foreach (var model in new[] { "Best", "Fast" })
            {
                string cache;
                using (var trusted = OcrModelLease.Acquire(model))
                {
                    cache = trusted.DirectoryPath;
                    Check(cache.StartsWith(AppPaths.DataDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "cache is private to selected data root");
                    using var concurrent = OcrModelLease.Acquire(model);
                    Check(concurrent.DirectoryPath == cache, "concurrent readers share approved models");
                    var file = Path.Combine(cache, "eng.traineddata");
                    Reject(() => { using var write = File.OpenWrite(file); }, "in-place write during native lifetime");
                    Reject(() => File.Delete(file), "delete during native lifetime");
                    Reject(() => File.Move(file, file + ".moved"), "rename during native lifetime");
                    Reject(() => Directory.Move(cache, cache + ".moved"), "replace parent during native lifetime");
                }
                var original = File.ReadAllBytes(Path.Combine(cache, "eng.traineddata"));
                File.WriteAllText(Path.Combine(cache, "eng.traineddata"), "inert corrupt fixture");
                using (var restored = OcrModelLease.Acquire(model))
                    Check(File.ReadAllBytes(Path.Combine(cache, "eng.traineddata")).SequenceEqual(original), "corrupt default cache restores the official model");

                var alternate = Path.Combine(root, "alternate-" + model);
                Directory.CreateDirectory(alternate);
                foreach (var language in new[] { "eng", "por" })
                    File.Copy(Path.Combine(cache, language + ".traineddata"), Path.Combine(alternate, language + ".traineddata"));
                using (OcrModelLease.Acquire(model, alternate)) Check(true, "identical alternate models accepted");
                var altered = Path.Combine(alternate, "eng.traineddata");
                var bytes = File.ReadAllBytes(altered); bytes[bytes.Length / 2] ^= 1; File.WriteAllBytes(altered, bytes);
                Reject(() => { using var invalid = OcrModelLease.Acquire(model, alternate); }, "same-size benign alteration rejected");
                File.WriteAllBytes(altered, [0, 1, 2]);
                Reject(() => { using var invalid = OcrModelLease.Acquire(model, alternate); }, "truncated model rejected");
                File.Delete(altered);
                Reject(() => { using var invalid = OcrModelLease.Acquire(model, alternate); }, "missing model rejected");
                File.Copy(Path.Combine(cache, "eng.traineddata"), altered);
                Reject(() => { using var invalid = OcrModelLease.Acquire(model == "Best" ? "Fast" : "Best", alternate); }, "wrong model family rejected");
                var alias = Path.Combine(root, "alias-" + model);
                Directory.CreateDirectory(alias);
                Check(CreateHardLinkW(Path.Combine(alias, "eng.traineddata"), altered, IntPtr.Zero), "fixture hardlink created");
                File.Copy(Path.Combine(cache, "por.traineddata"), Path.Combine(alias, "por.traineddata"));
                Reject(() => { using var invalid = OcrModelLease.Acquire(model, alias); }, "preexisting hardlink rejected");
                File.Delete(Path.Combine(alias, "eng.traineddata"));
                using (OcrModelLease.Acquire(model, alternate)) Check(true, "failed acquisition releases its pins");
                var junction = Path.GetFullPath(Path.Combine(root, "junction-" + model));
                var ownRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
                Check(junction.StartsWith(ownRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFullPath(alternate).StartsWith(ownRoot, StringComparison.OrdinalIgnoreCase), "junction fixture stays within its own root");
                var linking = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var argument in new[] { "/d", "/c", "mklink", "/J", junction, Path.GetFullPath(alternate) }) linking.ArgumentList.Add(argument);
                using (var linker = Process.Start(linking)!)
                {
                    await linker.WaitForExitAsync();
                    Check(linker.ExitCode == 0, "owned NTFS junction created");
                }
                try { Reject(() => { using var invalid = OcrModelLease.Acquire(model, junction); }, "model directory junction rejected"); }
                finally { Directory.Delete(junction, recursive: false); }
            }
            // Cold concurrent writers must not publish a partial model or mix Best/Fast.
            var cacheRoot = Path.Combine(AppPaths.DataDirectory, "OcrModels-v1");
            var freshData = Path.Combine(root, "concurrent-data");
            AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, freshData, root, true));
            await Task.WhenAll(Enumerable.Range(0, 8).Select(index => Task.Run(() =>
            { using var lease = OcrModelLease.Acquire(index % 2 == 0 ? "Best" : "Fast"); })));
            Check(Directory.EnumerateFiles(Path.Combine(AppPaths.DataDirectory, "OcrModels-v1"), "*.tmp", SearchOption.AllDirectories).Count() == 0, "cold concurrent restoration leaves no fragments");
            Check(Directory.Exists(cacheRoot), "previous data root preserved");

            var invalidNative = Path.Combine(root, "invalid-native");
            Directory.CreateDirectory(Path.Combine(invalidNative, "x64"));
            File.WriteAllText(Path.Combine(invalidNative, "x64", "leptonica-1.82.0.dll"), "inert DLL fixture; never loaded");
            File.WriteAllText(Path.Combine(invalidNative, "x64", "tesseract50.dll"), "inert DLL fixture; never loaded");
            Reject(() => { using var invalid = OcrNativeAssets.Acquire(invalidNative); }, "unapproved native rejected before load");
            Check(!Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Any(module => module.ModuleName.Equals("tesseract50.dll", StringComparison.OrdinalIgnoreCase)), "inert rejection did not load Tesseract");
            var originalNative = Path.Combine(AppContext.BaseDirectory, "x64");
            File.Copy(Path.Combine(originalNative, "leptonica-1.82.0.dll"), Path.Combine(invalidNative, "x64", "leptonica-1.82.0.dll"), overwrite: true);
            var swapped = File.ReadAllBytes(Path.Combine(originalNative, "tesseract50.dll")); swapped[swapped.Length / 2] ^= 1;
            File.WriteAllBytes(Path.Combine(invalidNative, "x64", "tesseract50.dll"), swapped);
            Reject(() => { using var invalid = OcrNativeAssets.Acquire(invalidNative); }, "same-sized native alteration rejected");
            Check(!Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Any(module => module.ModuleName.Equals("leptonica-1.82.0.dll", StringComparison.OrdinalIgnoreCase)), "all DLL hashes verified before loading even the first library");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "x86", "tesseract50.dll"), Path.Combine(invalidNative, "x64", "tesseract50.dll"), overwrite: true);
            Reject(() => { using var invalid = OcrNativeAssets.Acquire(invalidNative); }, "x86 binary placed under x64 rejected before execution");
            if (native) await RunNativeAsync(root, baseline);
            var summary = new { status = "PASS", checks = _checks, native, baselineCompared = baseline is not null,
                limits = "Approved native binaries only; generated images and inert alterations. No exploit payloads, physical screen/audio, personal clipboard or claim of an OS sandbox/clean VM." };
            File.WriteAllText(Path.Combine(root, "result.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"OCR trust: {_checks} checks PASS; native={native}; baseline={baseline is not null}.");
        }
        finally { AppPaths.Initialize(previous); }
    }

    private static async Task RunNativeAsync(string root, string? baseline)
    {
        using (var native = OcrNativeAssets.Acquire())
        {
            Check(native.TesseractVersion == "5.0.0" && native.LeptonicaVersion.StartsWith("leptonica-1.82.0"), "actual native exports and ABI verified");
            var loaded = Process.GetCurrentProcess().Modules.Cast<ProcessModule>().Where(module =>
                module.ModuleName.Equals("tesseract50.dll", StringComparison.OrdinalIgnoreCase) || module.ModuleName.Equals("leptonica-1.82.0.dll", StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(loaded.Length == 2, "both approved x64 modules are actually loaded");
            foreach (var module in loaded)
                Check(Path.GetFullPath(module.FileName!).StartsWith(Path.Combine(native.DirectoryPath, "x64") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "loaded module comes from verified x64 root");
        }
        var metrics = new List<object>();
        foreach (var model in new[] { "Best", "Fast" })
        foreach (var language in new[] { "eng", "por", "por+eng" })
        {
            var text = language == "eng" ? "Local text capture\nConnection successful 12345" : "Organização financeira\nAção, seleção e informação 12345";
            using var bitmap = Fixture(text);
            var options = new CaptureOcrSettings { Model = model, Languages = language, Layout = "Block" };
            var service = new CaptureOcrService(options: options);
            var result = await service.RecognizeAsync(bitmap);
            var error = ErrorRate(text, result.Text);
            Check(error < .10 && result.Confidence > .60f, "offline OCR preserves readable text and confidence " + model + "/" + language);
            CaptureOcrResult? old = baseline is null ? null : await new CaptureOcrService(executable: baseline, options: options).RecognizeAsync(bitmap);
            if (old is not null) Check(error <= ErrorRate(text, old.Text) + .01 && result.Confidence >= old.Confidence - .05f, "quality/confidence match approved baseline " + model + "/" + language);
            metrics.Add(new { model, language, characterErrorRate = error, result.Confidence, result.ElapsedMilliseconds,
                result.PeakWorkingSetBytes, baselineErrorRate = old is null ? (double?)null : ErrorRate(text, old.Text), baselineConfidence = old?.Confidence });
        }
        // Direct argv must not bypass the parent-side validation.
        using (var session = TemporarySession.Create("ocr"))
        {
            var input = Path.Combine(session.DirectoryPath, "region.png");
            var output = Path.Combine(session.DirectoryPath, "result.json");
            using (var image = Fixture("Only synthetic text")) image.Save(input, System.Drawing.Imaging.ImageFormat.Png);
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = session.DirectoryPath };
            foreach (var argument in new[] { "--capture-ocr-worker", input, output, Path.Combine(root, "invalid-worker-models"), "{}" }) start.ArgumentList.Add(argument);
            using var worker = Process.Start(start)!;
            try { await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
            catch { if (!worker.HasExited) worker.Kill(); await worker.WaitForExitAsync(); throw; }
            Check(worker.ExitCode == 5 && !File.Exists(output), "direct worker arguments reject an unauthorized model root");
        }
        using var blank = Fixture("");
        Check(string.IsNullOrWhiteSpace((await new CaptureOcrService(options: new() { Model = "Fast" }).RecognizeAsync(blank)).Text), "blank image remains empty");
        string fastCache;
        using (var modelLease = OcrModelLease.Acquire("Fast")) fastCache = modelLease.DirectoryPath;
        using (var writer = new FileStream(Path.Combine(fastCache, "models-v1.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var waiting = Stopwatch.StartNew();
            try { await new CaptureOcrService(null, null, new() { Model = "Fast" }, TimeSpan.FromMilliseconds(100)).RecognizeAsync(blank); throw new Exception("Cache wait ignored timeout"); }
            catch (TimeoutException) { Check(waiting.Elapsed < TimeSpan.FromSeconds(2), "cache writer wait observes timeout before launching a worker"); }
        }
        var previousMode = Environment.GetEnvironmentVariable("SLASHDESK_TEST_OCR_MODE");
        try
        {
            Environment.SetEnvironmentVariable("SLASHDESK_TEST_OCR_MODE", "wait");
            try { await new CaptureOcrService(null, null, new() { Model = "Fast" }, TimeSpan.FromSeconds(2)).RecognizeAsync(blank); throw new Exception("Timeout ignored"); }
            catch (TimeoutException) { Check(true, "owned inert worker timeout kills and joins child"); }
            using var cancellation = new CancellationTokenSource();
            var pending = new CaptureOcrService(options: new() { Model = "Fast" }).RecognizeAsync(blank, cancellation.Token);
            var timer = Stopwatch.StartNew();
            while (!Directory.EnumerateFiles(TemporarySession.Root, "result.json", SearchOption.AllDirectories).Any())
            { if (timer.Elapsed > TimeSpan.FromSeconds(8)) throw new TimeoutException("Inert child readiness"); await Task.Delay(15); }
            cancellation.Cancel();
            try { await pending; throw new Exception("Cancellation ignored"); }
            catch (OperationCanceledException) { Check(true, "active owned worker cancellation joined"); }
        }
        finally { Environment.SetEnvironmentVariable("SLASHDESK_TEST_OCR_MODE", previousMode); }
        Check(!Directory.EnumerateDirectories(TemporarySession.Root, "ocr-*").Any(), "all OCR sessions removed after success/timeout/cancel");
        File.WriteAllText(Path.Combine(root, "metrics.json"), JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Bitmap Fixture(string text)
    {
        var bitmap = new Bitmap(900, 190);
        using var graphics = Graphics.FromImage(bitmap); graphics.Clear(Color.White);
        using var font = new Font("Segoe UI", 24);
        graphics.DrawString(text, font, Brushes.Black, 24, 24);
        return bitmap;
    }
    private static double ErrorRate(string expected, string actual)
    {
        expected = string.Join(' ', expected.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        actual = string.Join(' ', actual.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var row = Enumerable.Range(0, actual.Length + 1).ToArray();
        for (var i = 1; i <= expected.Length; i++)
        {
            var diagonal = row[0]; row[0] = i;
            for (var j = 1; j <= actual.Length; j++)
            { var previous = row[j]; row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (expected[i - 1] == actual[j - 1] ? 0 : 1)); diagonal = previous; }
        }
        return (double)row[actual.Length] / Math.Max(1, expected.Length);
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLinkW(string link, string existing, IntPtr reserved);
}
