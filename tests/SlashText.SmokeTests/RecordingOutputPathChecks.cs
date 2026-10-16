using System.Buffers.Binary;
using System.Drawing;
using System.Text;
using System.Text.Json;
using ScreenRecorderLib;
using SlashText.Localization;
using SlashText.Models;
using SlashText.Services;

internal static class RecordingOutputPathChecks
{
    private const string Message = "O caminho da gravação MP4 é muito longo, incluindo o arquivo temporário. Escolha uma pasta mais próxima da raiz do disco ou um nome de arquivo mais curto e tente novamente.";

    internal static async Task RunAsync(string output)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var previous = AppPaths.Current;
        var language = UiText.Language;
        var checks = new List<string>();
        void Check(bool value, string label) { if (!value) throw new InvalidOperationException("MP4 path: " + label); checks.Add(label); }
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
            Path.Combine(output, "fixture"), output, isCapturePilot: true));
        var target = new RecordingTarget(RecordingTargetKind.Window, new Rectangle(0, 0, 100, 100), new IntPtr(1));
        try
        {
            foreach (var prefix in new[] { @"C:\fixture154\", @"\\server154\share\", @"\\?\C:\fixture154\", @"\\?\UNC\server154\share\" })
            foreach (var suffix in new[] { "", "áéç😀" })
            foreach (var length in new[] { 258, 259, 260, 261, 350 })
            {
                var path = prefix + new string('f', length - prefix.Length - suffix.Length - 4) + suffix + ".mp4";
                Check(path.Length == length, "UTF-16 fixture length");
                foreach (var final in new[] { true, false })
                {
                    var rejected = false;
                    try { ScreenRecordingService.ValidateOutputPaths(final ? path : @"C:\short.mp4", final ? @"C:\short.tmp.mp4" : path); }
                    catch (InvalidOperationException e) { rejected = e.Message == UiText.Get(Message); }
                    Check(rejected == (length >= 260), "absolute/UNC/extended/Unicode final and temporary boundary " + length);
                }
            }
            // Do not touch a network share: UNC/extended cases above are pure validation.
            var longDirectory = Path.Combine(output, new string('d', 70), new string('e', 70), new string('f', 70));
            var invalid = new CaptureSettings { OutputDirectoryTemplate = longDirectory, FileNameTemplate = "video" };
            var settings = new RecordingSettings { VideoFps = 9999, Audio = new() { InputVolume = 42 } };
            var captureBefore = JsonSerializer.Serialize(invalid);
            var settingsBefore = JsonSerializer.Serialize(settings);
            var factory = new Factory();
            using var service = new ScreenRecordingService(factory, TimeSpan.FromSeconds(2));
            foreach (var locale in new[] { "pt-BR", "en", "es" })
            {
                UiText.Apply(locale);
                var rejected = false;
                try { _ = service.StartAsync(target, invalid, settings); }
                catch (InvalidOperationException e)
                { rejected = e.Message == UiText.Get(Message) && e.InnerException is null && (locale == "pt-BR" || e.Message != Message); }
                Check(rejected, "localized preflight error " + locale);
                Check(factory.Created == 0 && service.State == ScreenRecordingState.Idle &&
                    !service.IsRecording && service.RecordingId == Guid.Empty && service.RecoveryFilePath == "",
                    "preflight precedes native creation, marker, clock and reservation " + locale);
                Check(!Directory.Exists(longDirectory), "preflight does not create rejected directories " + locale);
                Check(JsonSerializer.Serialize(invalid) == captureBefore && JsonSerializer.Serialize(settings) == settingsBefore,
                    "rejected preferences remain byte-identical " + locale);
            }
            UiText.Apply("pt-BR");
            var relative = new CaptureSettings { FileNameTemplate = invalid.FileNameTemplate,
                OutputDirectoryTemplate = Path.GetRelativePath(Environment.CurrentDirectory, longDirectory) };
            try { _ = service.StartAsync(target, relative, settings); throw new Exception("Relative long path accepted"); }
            catch (InvalidOperationException e) { Check(e.Message == Message && factory.Created == 0, "relative path uses absolute budget"); }

            var boundaryDirectory = Path.Combine(output, "boundary");
            Directory.CreateDirectory(boundaryDirectory);
            // The current private filename adds 44 UTF-16 units to the final name.
            // This fixture sets the final length to 215, making the temporary 259.
            var stem = new string('v', 215 - boundaryDirectory.Length - 1 - 4);
            var supported = new CaptureSettings { OutputDirectoryTemplate = boundaryDirectory, FileNameTemplate = stem };
            var existing = Path.Combine(boundaryDirectory, stem + ".mp4");
            var sentinel = Encoding.UTF8.GetBytes("fictitious existing file; must not change");
            File.WriteAllBytes(existing, sentinel);
            try { _ = service.StartAsync(target, supported, settings); throw new Exception("Collision suffix bypassed budget"); }
            catch (InvalidOperationException e)
            { Check(e.Message == Message && factory.Created == 0 && File.ReadAllBytes(existing).SequenceEqual(sentinel), "collision suffix included without overwriting existing file"); }

            // A different name has no collision, but the same 259-unit temporary.
            supported.FileNameTemplate = "w" + stem[1..];
            var completion = service.StartAsync(target, supported,
                new RecordingSettings { Audio = new() { CaptureComputer = false, CaptureMicrophone = false } });
            var until = DateTime.UtcNow.AddSeconds(5);
            while (service.State != ScreenRecordingState.Recording)
            { if (completion.IsCompleted) await completion; if (DateTime.UtcNow > until) throw new TimeoutException("Path fixture startup"); await Task.Delay(10); }
            Check(service.RecoveryFilePath.Length == 259 && factory.Created == 1, "same service retries at 259 after preflight rejection");
            service.Stop();
            var saved = await completion.WaitAsync(TimeSpan.FromSeconds(5));
            Check(saved.Length == 215 && File.Exists(saved) && !service.IsRecording && factory.Backend!.Disposed == 1,
                "supported finalization releases reservation once");
            Check(File.ReadAllBytes(existing).SequenceEqual(sentinel), "existing file preserved after successful retry");
            Check(!Directory.Exists(Path.Combine(AppPaths.DataDirectory, "RecordingRecovery-v1")) ||
                Directory.GetFiles(Path.Combine(AppPaths.DataDirectory, "RecordingRecovery-v1")).Length == 0,
                "successful retry settles private metadata");
            var relativeValid = new CaptureSettings { FileNameTemplate = "relative-ok",
                OutputDirectoryTemplate = Path.GetRelativePath(Environment.CurrentDirectory, Path.Combine(output, "relative-ok")) };
            var relativeBefore = JsonSerializer.Serialize(relativeValid);
            using (var next = new ScreenRecordingService(new Factory(), TimeSpan.FromSeconds(2)))
            {
                var result = next.StartAsync(target, relativeValid,
                    new RecordingSettings { Audio = new() { CaptureComputer = false, CaptureMicrophone = false } });
                Check(Path.IsPathFullyQualified(next.RecoveryFilePath), "native path resolved before queueing");
                next.Stop();
                var final = await result.WaitAsync(TimeSpan.FromSeconds(5));
                Check(final == Path.Combine(output, "relative-ok", "relative-ok.mp4") &&
                    JsonSerializer.Serialize(relativeValid) == relativeBefore, "relative preference preserved while runtime paths are absolute");
            }
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { status = "PASS", checks = checks.Count,
                native = false, limits = "Factory only; native video is separately validated on the final artifact", cases = checks },
                new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("MP4 output paths: " + checks.Count + " checks PASS; no real capture or network share accessed.");
        }
        finally { UiText.Apply(language); AppPaths.Initialize(previous); }
    }

    private sealed class Factory : IScreenRecorderBackendFactory
    {
        internal int Created;
        internal Backend? Backend;
        public IScreenRecorderBackend Create(RecorderOptions options) { Created++; return Backend = new(); }
    }
    private sealed class Backend : IScreenRecorderBackend
    {
        public event EventHandler<RecordingCompleteEventArgs>? Completed;
        public event EventHandler<RecordingFailedEventArgs>? Failed { add { } remove { } }
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
        internal int Disposed;
        private FileStream? _file;
        private string _path = "";
        public void Record(string path)
        {
            _path = path; _file = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            foreach (var (kind, length) in new[] { ("ftyp", 16), ("moov", 8), ("mdat", 24) })
            {
                var bytes = new byte[length]; BinaryPrimitives.WriteInt32BigEndian(bytes, length);
                Encoding.ASCII.GetBytes(kind).CopyTo(bytes, 4); _file.Write(bytes);
            }
            _file.Flush(); StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Recording));
        }
        public void Stop() => Completed?.Invoke(this, new RecordingCompleteEventArgs(_path, new List<FrameData>()));
        public void Pause() { }
        public void Resume() { }
        public bool SetAudioVolumes(float input, float output) => true;
        public void Dispose() { Disposed++; _file?.Dispose(); }
    }
}
