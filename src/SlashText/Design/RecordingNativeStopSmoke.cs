using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ScreenRecorderLib;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

// Explicit helper mode only. The native recorder captures this helper's own opaque
// window. Audio is disabled unless the caller explicitly supplies --audio-both.
internal static class RecordingNativeStopSmoke
{
    internal static async Task<int> RunAsync(string output, string mode, bool audio)
    {
        if (mode is not ("late-stop" or "emergency" or "normal"))
            throw new ArgumentException("Unknown native recovery scenario");
        Directory.CreateDirectory(output);
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
            Path.Combine(output, "fixture-data"), output, isCapturePilot: true));
        UiText.Apply("pt-BR"); ThemeService.Apply("Dark");
        var settings = new RecordingSettings { IncludeCursor = false,
            Audio = new() { CaptureComputer = audio, CaptureMicrophone = audio } };
        if (audio && (await Task.Run(RecordingAudioDevices.Load)).Validate(settings.Audio) is not null)
        {
            Write(output, new { status = "INCOMPLETE", mode, audio,
                reason = "Both input and output endpoints are required" });
            return 2;
        }
        var label = new TextBlock { Text = "SlashDesk #106 · janela fictícia", FontSize = 22,
            Foreground = Brushes.White, Margin = new Thickness(20) };
        var window = new Window { Content = label, Width = 480, Height = 300,
            Left = 30, Top = 30, Background = Brushes.DarkSlateBlue,
            Title = "SlashDesk #106 · teste isolado", ShowInTaskbar = false };
        window.Show();
        var frames = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) => label.Text = "SlashDesk #106 · janela fictícia\nQuadro " + ++frames;
        timer.Start();
        var factory = new GatedFactory(mode != "normal");
        using var service = new ScreenRecordingService(factory, TimeSpan.FromSeconds(1));
        var toolbar = new RecordingControlWindow(service, "MP4", settings);
        toolbar.Show(); toolbar.BeginRecording();
        var target = new RecordingTarget(RecordingTargetKind.Window,
            new System.Drawing.Rectangle(30, 30, 480, 300), new WindowInteropHelper(window).Handle);
        var completion = service.StartAsync(target,
            new CaptureSettings { OutputDirectoryTemplate = output, FileNameTemplate = "native-recovery" }, settings);
        try
        {
            await Until(() => service.State == ScreenRecordingState.Recording || completion.IsCompleted);
            if (completion.IsCompleted) await completion;
            await Task.Delay(2000);
            Require(frames > 5 && factory.Backend is not null, "real native recording started");
            service.Stop();
            if (mode == "normal")
            {
                var file = await completion.WaitAsync(TimeSpan.FromSeconds(20));
                CheckTracks(file, audio);
                using var released = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Require(released.Length > 32 && factory.Backend!.DisposeCalls == 1 && !service.IsRecording,
                    "successor records, stops and releases native file");
                Write(output, new { status = "PASS", mode, audio, partial = Path.GetFileName(file),
                    bytes = released.Length, nativeDisposed = true });
                return 0;
            }
            await Until(() => service.IsStopUnconfirmed && toolbar.RecoveryVisible);
            Require(factory.Backend!.StopEntered.IsSet && factory.Backend.DisposeCalls == 0 &&
                service.IsRecording && !completion.IsCompleted, "uncertain native stop retains lifetime");
            toolbar.Close();
            Require(toolbar.IsVisible, "system close cannot conceal native uncertainty");
            using (var successor = new ScreenRecordingService())
            {
                var blocked = false;
                try { _ = successor.StartAsync(target, new CaptureSettings { OutputDirectoryTemplate = output }, settings); }
                catch (InvalidOperationException) { blocked = true; }
                Require(blocked, "second native recording rejected before creation");
            }
            var partial = service.RecoveryFilePath;
            Require(File.Exists(partial) && new FileInfo(partial).Length > 32, "real partial MP4 exists");
            var locked = false;
            try { using var file = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException e) when ((e.HResult & 0xffff) is 32 or 33) { locked = true; }
            Require(locked, "native process still owns partial before emergency exit");
            if (mode == "emergency")
            {
                // The parent checks the exact owned child identity, its exit and file
                // release. Production confirmation/cancel are tested by WPF fixtures.
                Write(output, new { status = "READY_FOR_SELF_TERMINATION", mode, audio,
                    pid = Environment.ProcessId, startedTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks,
                    partial = Path.GetFileName(partial), bytes = new FileInfo(partial).Length,
                    recordingId = service.RecordingId, uncertain = true, nativeFileLocked = true });
                RecordingEmergencyExit.TerminateCurrentProcess();
                throw new InvalidOperationException("Emergency exit returned");
            }
            factory.Backend.Release.Set();
            try { await completion.WaitAsync(TimeSpan.FromSeconds(20)); throw new InvalidOperationException("Late stop reported success"); }
            catch (TimeoutException) { Require(completion.IsFaulted, "late cleanup completed before test timeout"); }
            Require(!service.IsRecording && factory.Backend.DisposeCalls == 1, "late native release occurs exactly once");
            CheckTracks(partial, audio);
            using (var released = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                Require(released.Length > 32, "late release preserves actual MP4 bytes");
                Write(output, new { status = "PASS", mode, audio, partial = Path.GetFileName(partial),
                    bytes = released.Length, nativeDisposed = true });
            }
            return 0;
        }
        finally
        {
            factory.Backend?.Release.Set(); timer.Stop(); service.Stop();
            // Never report successful cleanup because this bounded wait expired.
            try { await completion.WaitAsync(TimeSpan.FromSeconds(20)); } catch { }
            toolbar.Close(); window.Close();
        }
    }

    private static void Write(string output, object value) => File.WriteAllText(
        Path.Combine(output, "native-stop.json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void CheckTracks(string path, bool audio)
    {
        var tracks = RecordingAudioSmoke.Tracks(File.ReadAllBytes(path));
        Require(tracks.Count(track => track.Kind == "vide") == 1 &&
            tracks.Single(track => track.Kind == "vide").Seconds > 1 &&
            tracks.Any(track => track.Kind == "soun") == audio,
            "real native video/audio tracks match the selected sources after release");
    }
    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Native recovery state"); await Task.Delay(30); }
    }
    private sealed class GatedFactory(bool block) : IScreenRecorderBackendFactory
    {
        internal GatedBackend? Backend;
        public IScreenRecorderBackend Create(RecorderOptions options) => Backend = new(
            new ScreenRecorderBackendFactory().Create(options), block);
    }
    private sealed class GatedBackend(IScreenRecorderBackend inner, bool block) : IScreenRecorderBackend
    {
        internal readonly ManualResetEventSlim StopEntered = new(false), Release = new(false);
        internal int DisposeCalls;
        public event EventHandler<RecordingCompleteEventArgs>? Completed { add => inner.Completed += value; remove => inner.Completed -= value; }
        public event EventHandler<RecordingFailedEventArgs>? Failed { add => inner.Failed += value; remove => inner.Failed -= value; }
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged { add => inner.StatusChanged += value; remove => inner.StatusChanged -= value; }
        public void Record(string path) => inner.Record(path);
        public void Pause() => inner.Pause();
        public void Resume() => inner.Resume();
        public bool SetAudioVolumes(float input, float output) => inner.SetAudioVolumes(input, output);
        public void Stop() { StopEntered.Set(); if (block) Release.Wait(); inner.Stop(); }
        public void Dispose() { Interlocked.Increment(ref DisposeCalls); inner.Dispose(); }
    }
}
