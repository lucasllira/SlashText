using System.Diagnostics;
using ScreenRecorderLib;
using SlashText.Models;
using SlashText.Services;

internal static class RecordingStopSafetyChecks
{
    internal static async Task RunAsync(string root)
    {
        foreach (var mode in new[] { "normal", "duplicate", "missing", "blocked-stop", "blocked-start", "blocked-dispose", "blocked-pause", "blocked-resume", "callback-without-stop", "dispose-failed", "failed-callback", "emergency" })
        {
            var home = Path.Combine(root, "recording-safety", mode);
            Directory.CreateDirectory(home);
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(typeof(RecordingStopSafetyChecks).Assembly.Location);
            start.ArgumentList.Add("--recording-recovery-probe");
            start.ArgumentList.Add(home);
            start.ArgumentList.Add(mode);
            using var child = Process.Start(start)!;
            var stderr = child.StandardError.ReadToEndAsync();
            try { await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { child.Kill(); await child.WaitForExitAsync(); throw; }
            Check(File.Exists(Path.Combine(home, "passed.txt")), mode + ": assertions in isolated process: " + await stderr);
            var forced = mode is "dispose-failed" or "emergency";
            Check(forced ? child.ExitCode != 0 : child.ExitCode == 0, mode + ": expected exit");
            var preserved = File.ReadAllText(Path.Combine(home, "file.txt"));
            using var file = new FileStream(preserved, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            Check(file.Length == 48, mode + ": file preserved and handle released after process exit");
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
        await Until(() => factory.Backend.RecordEntered.IsSet);
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
        }
        else
        {
            await Until(() => service.IsStopUnconfirmed);
            Check(service.IsRecording && !task.IsCompleted, "uncertain stop keeps session alive");
            Check(!await service.SetAudioMutedAsync(true, true), "mute cannot claim success during uncertainty");
            Check(File.Exists(Path.Combine(AppPaths.LogsDirectory, "recording-active.json")), "active marker retained");
            Check(File.ReadAllText(Path.Combine(AppPaths.LogsDirectory, "recording-active.json")).Contains("StopUnconfirmed"), "persistent marker identifies uncertainty");
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
        }
        Check(factory.Backend.MaxConcurrentCalls == 1, "all native calls serialized");
        Check(!File.Exists(Path.Combine(AppPaths.LogsDirectory, "recording-active.json")), "marker cleared only after native release");
        File.WriteAllText(Path.Combine(home, "file.txt"), task.Status == TaskStatus.RanToCompletion ? task.Result : service.RecoveryFilePath);
        // Only confirmed cleanup releases the cross-instance reservation.
        using var successor = new ScreenRecordingService(new Factory("normal"), TimeSpan.FromSeconds(2));
        var next = successor.StartAsync(target, capture, new RecordingSettings());
        successor.Stop();
        await next.WaitAsync(TimeSpan.FromSeconds(3));
        File.WriteAllText(Path.Combine(home, "passed.txt"), mode + ": passed");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
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
