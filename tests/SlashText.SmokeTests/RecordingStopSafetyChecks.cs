using System.Diagnostics;
using System.Text.Json;
using ScreenRecorderLib;
using SlashText.Models;
using SlashText.Services;

internal static class RecordingStopSafetyChecks
{
    internal static async Task RunAsync(string root)
    {
        foreach (var mode in new[] { "normal", "duplicate", "missing", "blocked-stop", "blocked-start", "blocked-dispose", "blocked-pause", "blocked-resume", "callback-without-stop", "dispose-failed", "failed-callback", "emergency" })
        {
            var home = Path.GetFullPath(Path.Combine(root, "recording-safety", mode));
            Directory.CreateDirectory(home);
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(typeof(RecordingStopSafetyChecks).Assembly.Location);
            start.ArgumentList.Add("--recording-recovery-probe");
            start.ArgumentList.Add(home);
            start.ArgumentList.Add(mode);
            using var child = Process.Start(start)!;
            var childStartedTicks = child.StartTime.ToUniversalTime().Ticks;
            var stderr = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { child.Kill(); await child.WaitForExitAsync(); throw; }
            Check(File.Exists(Path.Combine(home, "passed.txt")), mode + ": assertions in isolated process: " + await stderr);
            var forced = mode is "dispose-failed" or "emergency";
            Check(forced ? child.ExitCode != 0 : child.ExitCode == 0, mode + ": expected exit");
            var preserved = File.ReadAllText(Path.Combine(home, "file.txt"));
            using var file = new FileStream(preserved, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Check(file.Length == 48, mode + ": file preserved and handle released after process exit");
            if (forced)
            {
                var marker = Path.GetFullPath(File.ReadAllText(Path.Combine(home, "private-marker.txt")));
                Check(marker.StartsWith(home + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "fixture marker is inside its own isolated home");
                CheckMarker(marker, Guid.ParseExact(Path.GetFileNameWithoutExtension(marker), "N"), child.Id,
                    childStartedTicks, preserved, interrupted: false, stopUnconfirmed: true);
            }
        }
        Console.WriteLine("Recording stop safety: 12 isolated scenarios passed (including real emergency self-termination).");
    }

    internal static async Task ProbeAsync(string home, string mode)
    {
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable, home, home, isCapturePilot: true));
        var factory = new Factory(mode);
        using var service = new ScreenRecordingService(factory, TimeSpan.FromMilliseconds(300));
        var target = new RecordingTarget(RecordingTargetKind.Window, new System.Drawing.Rectangle(0, 0, 100, 100), new IntPtr(1));
        var capture = new CaptureSettings { OutputDirectoryTemplate = home, FileNameTemplate = "fixture" };
        var task = service.StartAsync(target, capture, new RecordingSettings());
        var privateMarker = MarkerPath(service.RecordingId);
        var markerAtCompletion = task.ContinueWith(_ => File.Exists(privateMarker), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await Until(() => factory.Backend.RecordEntered.IsSet);
        CheckMarker(privateMarker, service.RecordingId, Environment.ProcessId,
            Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks, service.RecoveryFilePath,
            interrupted: false, stopUnconfirmed: false);
        if (mode is "blocked-pause" or "blocked-resume")
        {
            await Until(() => service.State == ScreenRecordingState.Recording);
            service.Pause();
            if (mode == "blocked-resume")
            {
                await Until(() => service.State == ScreenRecordingState.Paused);
                service.Resume();
            }
            await Until(() => factory.Backend.ControlEntered.IsSet);
        }
        // Stop must return promptly, even while startup or a native call is blocked.
        var timer = Stopwatch.StartNew();
        if (mode == "callback-without-stop") factory.Backend.Complete();
        else { service.Stop(); service.Stop(); service.Dispose(); }
        Check(timer.Elapsed < TimeSpan.FromMilliseconds(250), "stop and dispose do not block caller");
        if (mode is "normal" or "duplicate" or "failed-callback")
        {
            try { await task.WaitAsync(TimeSpan.FromSeconds(3)); Check(mode != "failed-callback", "expected native failure"); }
            catch (InvalidOperationException) when (mode == "failed-callback") { }
            Check(!service.IsRecording && !service.IsStopUnconfirmed, "released native resources reach terminal state");
            Check(service.State == (mode == "failed-callback" ? ScreenRecordingState.Failed : ScreenRecordingState.Completed), "correct terminal state");
            Check(factory.Backend.DisposeCalls == 1 && factory.Backend.StopCalls == 1, "single stop/disposal");
            if (mode != "failed-callback")
                Check(!await markerAtCompletion && !File.Exists(privateMarker), "successful completion is observed only after its private marker is removed");
            else
            {
                Check(await markerAtCompletion, "failed native callback keeps private recovery metadata at completion");
                CheckCurrentMarker(service, privateMarker, interrupted: true, stopUnconfirmed: false);
            }
        }
        else
        {
            await Until(() => service.IsStopUnconfirmed);
            Check(service.IsRecording && !task.IsCompleted, "uncertain stop keeps session alive");
            Check(!await service.SetAudioMutedAsync(true, true), "mute cannot claim success during uncertainty");
            CheckCurrentMarker(service, privateMarker, interrupted: false, stopUnconfirmed: true);
            Check(RecordingRecovery.InterruptedCount() == 0, "live uncertain recording is not classified as an already released interrupted recording");
            CheckNoSensitiveLogs(home, service.RecoveryFilePath);
            using var other = new ScreenRecordingService(new Factory("normal"), TimeSpan.FromSeconds(1));
            var blocked = false;
            try { _ = other.StartAsync(target, capture, new RecordingSettings()); }
            catch (InvalidOperationException) { blocked = true; }
            Check(blocked, "new recording blocked across instances");
            Check(factory.Backend.DisposeCalls == (mode is "blocked-dispose" or "dispose-failed" or "callback-without-stop" ? 1 : 0), "no concurrent disposal");
            if (mode is "emergency" or "dispose-failed")
            {
                Check(factory.Backend.FileLocked, "fixture still owned by unresolved backend");
                File.WriteAllText(Path.Combine(home, "file.txt"), service.RecoveryFilePath);
                File.WriteAllText(Path.Combine(home, "private-marker.txt"), privateMarker);
                File.WriteAllText(Path.Combine(home, "passed.txt"), "uncertainty retained; now terminate the fixture process");
                RecordingEmergencyExit.TerminateCurrentProcess();
                throw new InvalidOperationException("Emergency exit returned unexpectedly");
            }
            if (mode == "missing") factory.Backend.Complete();
            else factory.Backend.Release.Set();
            if (mode is "blocked-pause" or "blocked-resume")
            {
                await Until(() => factory.Backend.StopCalls == 1);
                Check(service.IsStopUnconfirmed, "late pause/resume cannot erase uncertainty");
                factory.Backend.Complete();
            }
            try { await task.WaitAsync(TimeSpan.FromSeconds(3)); throw new Exception("late completion must preserve temporary file"); }
            catch (TimeoutException) { Check(task.IsCompleted, "native cleanup completed, not test timeout"); }
            Check(!service.IsRecording && service.State == ScreenRecordingState.Failed, "late release clears uncertainty");
            Check(factory.Backend.DisposeCalls == 1, "late callback disposes exactly once");
            Check(File.Exists(service.RecoveryFilePath), "late callback preserves recoverable bytes");
            Check(await markerAtCompletion, "late failure completion retains private recovery metadata");
            CheckCurrentMarker(service, privateMarker, interrupted: true, stopUnconfirmed: true);
        }
        Check(factory.Backend.MaxConcurrentCalls == 1, "all native calls serialized");
        CheckNoSensitiveLogs(home, service.RecoveryFilePath);
        File.WriteAllText(Path.Combine(home, "file.txt"), task.Status == TaskStatus.RanToCompletion ? task.Result : service.RecoveryFilePath);
        // Only confirmed cleanup releases the cross-instance reservation.
        using var successor = new ScreenRecordingService(new Factory("normal"), TimeSpan.FromSeconds(2));
        var next = successor.StartAsync(target, capture, new RecordingSettings());
        var successorMarker = MarkerPath(successor.RecordingId);
        var successorMarkerAtCompletion = next.ContinueWith(_ => File.Exists(successorMarker), CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        successor.Stop();
        await next.WaitAsync(TimeSpan.FromSeconds(3));
        Check(!await successorMarkerAtCompletion && !File.Exists(successorMarker), "successor completion does not publish while its private marker exists");
        CheckNoSensitiveLogs(home, successor.RecoveryFilePath);
        File.WriteAllText(Path.Combine(home, "passed.txt"), mode + ": passed");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static string MarkerPath(Guid id) => Path.Combine(AppPaths.DataDirectory, "RecordingRecovery-v1", id.ToString("N") + ".json");

    private static void CheckCurrentMarker(ScreenRecordingService service, string marker, bool interrupted, bool stopUnconfirmed) =>
        CheckMarker(marker, service.RecordingId, Environment.ProcessId, Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
            service.RecoveryFilePath, interrupted, stopUnconfirmed);

    private static void CheckMarker(string path, Guid id, int pid, long startedTicks, string partialPath, bool interrupted, bool stopUnconfirmed)
    {
        Check(File.Exists(path), "private recording marker exists");
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var marker = document.RootElement;
        Check(marker.GetProperty("Schema").GetInt32() == 1 && marker.GetProperty("Id").GetGuid() == id
            && Path.GetFileName(path) == id.ToString("N") + ".json", "private marker schema and file identity match the session");
        Check(marker.GetProperty("Pid").GetInt32() == pid && marker.GetProperty("StartedUtcTicks").GetInt64() == startedTicks,
            "private marker identifies the exact owning process lifetime");
        Check(string.Equals(marker.GetProperty("PartialPath").GetString(), partialPath, StringComparison.OrdinalIgnoreCase),
            "private marker retains the exact recoverable partial path");
        Check(marker.GetProperty("Interrupted").GetBoolean() == interrupted
            && marker.GetProperty("StopUnconfirmed").GetBoolean() == stopUnconfirmed,
            "private marker distinguishes live uncertainty, confirmed release and historical timeout cause");
    }

    private static void CheckNoSensitiveLogs(string home, string partialPath)
    {
        if (!Directory.Exists(AppPaths.LogsDirectory)) return;
        var needles = new[] { Path.GetFullPath(home), partialPath, Path.GetFileName(partialPath), "fixture failure" }
            .Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        foreach (var path in Directory.EnumerateFiles(AppPaths.LogsDirectory).Where(path => !path.EndsWith(".lock", StringComparison.OrdinalIgnoreCase)))
        {
            string text;
            var attempts = 0;
            while (true)
            {
                try { text = File.ReadAllText(path); break; }
                catch (IOException exception) when (exception.HResult == unchecked((int)0x80070020) && attempts++ < 20)
                { Thread.Sleep(5); } // Logging owns a brief exclusive append; never silently skip a diagnostic file.
            }
            foreach (var needle in needles)
            {
                var jsonEscaped = JsonSerializer.Serialize(needle)[1..^1];
                Check(!text.Contains(needle, StringComparison.OrdinalIgnoreCase) && !text.Contains(jsonEscaped, StringComparison.OrdinalIgnoreCase),
                    "diagnostics contain neither private paths, filenames nor free-form backend error messages");
            }
        }
    }
    private static async Task Until(Func<bool> condition)
    {
        var until = Stopwatch.StartNew();
        while (!condition()) { if (until.Elapsed > TimeSpan.FromSeconds(3)) throw new TimeoutException("Probe state"); await Task.Delay(10); }
    }

    private sealed class Factory(string mode) : IScreenRecorderBackendFactory
    {
        internal Backend Backend { get; } = new(mode);
        public IScreenRecorderBackend Create(RecorderOptions options) => Backend;
    }

    private sealed class Backend(string mode) : IScreenRecorderBackend
    {
        public event EventHandler<RecordingCompleteEventArgs>? Completed;
        public event EventHandler<RecordingFailedEventArgs>? Failed;
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
        internal ManualResetEventSlim RecordEntered { get; } = new(false);
        internal ManualResetEventSlim Release { get; } = new(false);
        internal ManualResetEventSlim ControlEntered { get; } = new(false);
        internal int DisposeCalls, StopCalls, MaxConcurrentCalls;
        private int _active;
        private FileStream? _file;
        private string _path = "";
        internal bool FileLocked => _file is not null;
        private void Native(Action action)
        {
            var count = Interlocked.Increment(ref _active);
            MaxConcurrentCalls = Math.Max(MaxConcurrentCalls, count);
            try { action(); } finally { Interlocked.Decrement(ref _active); }
        }
        public void Record(string path) => Native(() =>
        {
            _path = path;
            _file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            foreach (var (name, size) in new[] { ("ftyp", 16), ("moov", 8), ("mdat", 24) })
            {
                var bytes = new byte[size];
                System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes, size);
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(bytes, 4);
                _file.Write(bytes);
            }
            _file.Flush();
            RecordEntered.Set();
            if (mode == "blocked-start") Release.Wait();
            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Recording));
        });
        public void Stop() => Native(() =>
        {
            StopCalls++;
            if (mode == "blocked-stop") Release.Wait();
            if (mode is "missing" or "emergency" or "blocked-pause" or "blocked-resume") return;
            if (mode == "failed-callback") Failed?.Invoke(this, new RecordingFailedEventArgs("fixture failure", _path));
            else Complete();
        });
        internal void Complete()
        {
            Completed?.Invoke(this, new RecordingCompleteEventArgs(_path, new List<FrameData>()));
            if (mode == "duplicate") Failed?.Invoke(this, new RecordingFailedEventArgs("duplicate", _path));
        }
        public void Dispose() => Native(() =>
        {
            DisposeCalls++;
            if (mode is "blocked-dispose" or "callback-without-stop") Release.Wait();
            if (mode == "dispose-failed") throw new InvalidOperationException("fixture Dispose failure");
            _file?.Dispose(); _file = null;
        });
        public void Pause() => Native(() => { if (mode == "blocked-pause") { ControlEntered.Set(); Release.Wait(); } });
        public void Resume() => Native(() => { if (mode == "blocked-resume") { ControlEntered.Set(); Release.Wait(); } });
        public bool SetAudioVolumes(float inputVolume, float outputVolume) => false;
    }
}
