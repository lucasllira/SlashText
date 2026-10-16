using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ScreenRecorderLib;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;

namespace SlashText.Design;

/// <summary>Real reservation/MP4 command/control, inert backend and explicit fixture paths only.</summary>
internal static class RecordingRecoveryOperationSmoke
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static async Task RunAsync(string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        var previousLanguage = UiText.Language;
        var unexpectedDialogs = 0;
        var expectedDialogs = 0;
        var dismiss = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        dismiss.Tick += (_, _) =>
        {
            foreach (var dialog in Application.Current.Windows.OfType<SettingsDialogWindow>().Where(item => item.IsVisible).ToArray())
            {
                unexpectedDialogs++;
                dialog.DefaultButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        };
        dismiss.Start();
        try
        {
            foreach (var language in new[] { "pt-BR", "en", "es" })
            foreach (var theme in new[] { "Light", "Dark" })
            {
                var label = language + "-" + theme;
                var root = Path.Combine(output, label);
                AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
                    root, Path.Combine(root, "unused-installed"), isCapturePilot: true));
                AppPaths.EnsureDataLayout();
                UiText.Apply(language); ThemeService.Apply(theme);
                var settings = new AppSettings { CloseToTray = false, OnboardingCompleted = true, CheckUpdatesOnStartup = false };
                settings.Capture.OutputDirectoryTemplate = Path.Combine(root, "inert-media");
                settings.Capture.FileNameTemplate = "recovery-operation-fixture";
                settings.Capture.CopyToClipboard = false;
                settings.Capture.Recording.Audio = new() { CaptureComputer = false, CaptureMicrophone = false };
                var store = new JsonFileStore<AppSettings>(AppPaths.SettingsFile);
                await store.SaveAsync(settings);
                var window = new MainWindow(captureEvidence: true);
                var backend = new Backend();
                using var recorder = new ScreenRecordingService(backend, TimeSpan.FromMilliseconds(300));
                RecordingControlWindow? control = null;
                Task<string>? completion = null;
                Task? command = null;
                var lateTimeoutObserved = false;
                var pipelineCalls = 0;
                var captureSignals = new List<string>();
                var historyBefore = File.Exists(AppPaths.CaptureHistoryFile) ? File.ReadAllBytes(AppPaths.CaptureHistoryFile) : null;
                void Require(bool condition, string message)
                {
                    if (!condition) throw new InvalidOperationException("Recording recovery operation smoke: " + label + ": " + message);
                    checks.Add(label + ": " + message + ": PASS");
                }
                RecordingOperation? Current() => Field<RecordingOperation?>(window, "_recordingOperation");
                try
                {
                    Set(window, "_settings", await store.LoadAsync());
                    Invoke(window, "LoadCaptureSettings");
                    LabMotion.SetReduced(window, true);
                    window.CapturePreparationForEvidence = captureSignals.Add;
                    window.Show();
                    var gate = Field<SingleFlightGate>(window, "_captureGate");
                    var captureService = Field<CaptureService>(window, "_captureService");
                    var initialHistoryCount = captureService.History.Count;
                    window.RecordingPipelineForEvidence = async (media, operation) =>
                    {
                        pipelineCalls++;
                        if (pipelineCalls != 1 || media != "MP4") throw new InvalidOperationException("A concurrent command bypassed the existing reservation.");
                        Set(window, "_recordingService", recorder);
                        // Omit the audio panel: this fixture must not enumerate/open devices.
                        control = new RecordingControlWindow(recorder, "MP4");
                        Set(window, "_recordingControl", control);
                        control.Show();
                        operation.MarkRecording();
                        control.BeginRecording();
                        completion = recorder.StartAsync(new RecordingTarget(RecordingTargetKind.Window,
                            new System.Drawing.Rectangle(0, 0, 100, 100), new IntPtr(1)), settings.Capture, settings.Capture.Recording);
                        try { await completion; throw new InvalidOperationException("A timed-out recording was promoted to success."); }
                        catch (TimeoutException) when (completion.IsCompleted)
                        {
                            lateTimeoutObserved = true;
                            operation.MarkFinalizing();
                        }
                    };
                    command = (Task)Invoke(window, "RunRecordingOperationAsync", "MP4",
                        (Func<RecordingOperation, Task>)(operation => (Task)Invoke(window, "RunMp4RecordingAsync", operation)!))!;
                    await backend.Recorded.Task.WaitAsync(TimeSpan.FromSeconds(8));
                    var owner = Current() ?? throw new InvalidOperationException("No recording operation owns the fixture.");
                    var partial = recorder.RecoveryFilePath;
                    byte[] originalBytes;
                    using (var reading = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        originalBytes = new byte[reading.Length];
                        reading.ReadExactly(originalBytes);
                    }
                    recorder.Stop();
                    await UntilAsync(() => recorder.IsStopUnconfirmed && control?.RecoveryVisible == true);
                    Require(recorder.IsRecording && completion is { IsCompleted: false } && !command.IsCompleted
                        && !owner.Completion.IsCompleted && gate.IsActive && ReferenceEquals(Current(), owner),
                        "uncertain stop retains native completion, operation and capture reservation");
                    Require(owner.Phase == RecordingOperationPhase.Recording && control!.IsVisible
                        && ReferenceEquals(Field<ScreenRecordingService?>(window, "_recordingService"), recorder)
                        && ReferenceEquals(Field<RecordingControlWindow?>(window, "_recordingControl"), control),
                        "uncertainty preserves the live service/control identities");
                    control!.Close();
                    Require(control.IsVisible && gate.IsActive && backend.DisposeCalls == 0,
                        "system close cannot hide the warning or dispose an unresolved backend");
                    // Initial configuration legitimately reaches the shortcuts hook.
                    // Busy commands must add no new preparation signals to that baseline.
                    var captureSignalsBeforeBlockedCommands = captureSignals.ToArray();

                    void RecordBlockedState(string stage)
                    {
                        File.AppendAllText(Path.Combine(output, "recovery-operation-state.jsonl"), JsonSerializer.Serialize(new
                        {
                            label, stage, pipelineCalls, captureSignals = captureSignals.ToArray(),
                            gateActive = gate.IsActive, sameOperation = ReferenceEquals(Current(), owner),
                            controlVisible = control.IsVisible, recorderState = recorder.State.ToString(),
                            nativeCompletionPending = completion is { IsCompleted: false }, commandPending = !command.IsCompleted,
                            settingsOperation = Field<bool>(window, "_settingsOperationInProgress"),
                            servicesDisposed = Field<bool>(window, "_servicesDisposed"),
                            exitRequested = Field<bool>(window, "_exitRequested")
                        }) + Environment.NewLine);
                    }
                    foreach (var method in new[] { "StartMp4Recording_OnClick", "StartGifRecording_OnClick" })
                    {
                        Invoke(window, method, window, new RoutedEventArgs());
                        RecordBlockedState(method);
                        Require(gate.IsActive && ReferenceEquals(Current(), owner), method + " retains the original reservation");
                    }
                    await (Task)Invoke(window, "RunCaptureAsync", CaptureShortcutAction.Region, false)!;
                    RecordBlockedState("RunCaptureAsync");
                    Require(gate.IsActive && ReferenceEquals(Current(), owner), "blocked region capture retains the original reservation");
                    await (Task)Invoke(window, "RunScrollingCaptureAsync", false)!;
                    RecordBlockedState("RunScrollingCaptureAsync");
                    Require(gate.IsActive && ReferenceEquals(Current(), owner), "blocked scrolling capture retains the original reservation");
                    var maintenanceRan = false;
                    var maintenanceBlocked = false;
                    try
                    {
                        await (Task)Invoke(window, "RunSettingsMaintenanceAsync", "Inert recovery fixture maintenance",
                            (Func<Task>)(() => { maintenanceRan = true; return Task.CompletedTask; }), false)!;
                    }
                    catch (InvalidOperationException) { maintenanceBlocked = true; }
                    await Dispatcher.Yield(DispatcherPriority.Background);
                    RecordBlockedState("maintenance-and-dispatcher");
                    Require(pipelineCalls == 1, "blocked commands never enter another recording pipeline; actual calls=" + pipelineCalls);
                    Require(captureSignals.SequenceEqual(captureSignalsBeforeBlockedCommands),
                        "blocked commands add no capture preparation signals; baseline=" + string.Join(",", captureSignalsBeforeBlockedCommands)
                        + "; actual=" + string.Join(",", captureSignals));
                    Require(maintenanceBlocked, "maintenance reports the recording reservation as unavailable");
                    Require(!maintenanceRan, "the maintenance body never executes during uncertainty");
                    Require(ReferenceEquals(Current(), owner), "blocked commands preserve recording operation identity");
                    Require(gate.IsActive, "blocked commands preserve the capture gate");
                    Require(control.IsVisible, "blocked commands preserve the visible recording control");
                    Require(!((Button)window.FindName("CaptureNewButton")).IsEnabled
                        && !Field<bool>(window, "_settingsOperationInProgress"), "busy controls and maintenance state remain coherent");
                    Set(window, "_exitRequested", true);
                    var close = new CancelEventArgs();
                    Invoke(window, "MainWindow_OnClosing", window, close);
                    Require(close.Cancel && !Field<bool>(window, "_exitRequested") && !Field<bool>(window, "_servicesDisposed")
                        && gate.IsActive && control.IsVisible && !owner.Completion.IsCompleted,
                        "normal application exit cannot release or hide an uncertain recording");

                    backend.Complete();
                    backend.ReplayCompletion();
                    await command.WaitAsync(TimeSpan.FromSeconds(8));
                    Require(lateTimeoutObserved && completion is { IsFaulted: true } && !recorder.IsRecording
                        && backend.DisposeCalls == 1 && owner.Completion.IsCompleted,
                        "late native release completes as timeout with exactly one backend disposal");
                    Require(!gate.IsActive && Current() is null && !control.IsVisible
                        && Field<ScreenRecordingService?>(window, "_recordingService") is null
                        && Field<RecordingControlWindow?>(window, "_recordingControl") is null,
                        "the wrapper releases its reservation and references only after confirmed native cleanup");
                    using (var exclusive = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        var bytes = new byte[exclusive.Length];
                        exclusive.ReadExactly(bytes);
                        Require(bytes.SequenceEqual(originalBytes), "late timeout preserves the partial bytes and releases their handle");
                    }
                    Require(Directory.EnumerateFiles(settings.Capture.OutputDirectoryTemplate, "*.mp4").Single() == partial,
                        "a late completion does not promote the partial to a successful MP4");
                    Require(captureService.History.Count == initialHistoryCount &&
                        (historyBefore is null ? !File.Exists(AppPaths.CaptureHistoryFile)
                            : File.ReadAllBytes(AppPaths.CaptureHistoryFile).SequenceEqual(historyBefore)),
                        "uncertain and late completion never append successful capture history");

                    var prepared = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    window.RecordingPipelineForEvidence = null;
                    window.RecordingPreparationForEvidence = async (_, _) => { prepared.TrySetResult(); await release.Task; };
                    var next = (Task)Invoke(window, "RunRecordingOperationAsync", "GIF",
                        (Func<RecordingOperation, Task>)(_ => Task.CompletedTask))!;
                    await prepared.Task.WaitAsync(TimeSpan.FromSeconds(8));
                    var nextOwner = Current();
                    try
                    {
                        backend.ReplayCompletion();
                        await Dispatcher.Yield(DispatcherPriority.Background);
                        Require(nextOwner is not null && !ReferenceEquals(nextOwner, owner) && ReferenceEquals(Current(), nextOwner)
                            && gate.IsActive && !next.IsCompleted && backend.DisposeCalls == 1,
                            "a new preparation is accepted and stale callbacks cannot release its reservation");
                    }
                    finally { release.TrySetResult(); await next.WaitAsync(TimeSpan.FromSeconds(8)); }
                    Require(!gate.IsActive && Current() is null, "the subsequent preparation also releases exactly its own reservation");

                    // A caller failure must not let the wrapper abandon an active native owner.
                    window.RecordingPreparationForEvidence = null;
                    Require(unexpectedDialogs == expectedDialogs, "normal uncertainty and blocked commands do not open failure dialogs");
                    foreach (var throughMp4Pipeline in new[] { false, true })
                    {
                        var failingBackend = new Backend();
                        using var failingRecorder = new ScreenRecordingService(failingBackend, TimeSpan.FromMilliseconds(300));
                        var failingControl = new RecordingControlWindow(failingRecorder, "MP4");
                        Task? failedCommand = null;
                        expectedDialogs++;
                        try
                        {
                            Func<RecordingOperation, Task> failingAction = async operation =>
                            {
                                Set(window, "_recordingService", failingRecorder);
                                Set(window, "_recordingControl", failingControl);
                                failingControl.Show(); operation.MarkRecording(); failingControl.BeginRecording();
                                _ = failingRecorder.StartAsync(new RecordingTarget(RecordingTargetKind.Window,
                                    new System.Drawing.Rectangle(0, 0, 100, 100), new IntPtr(1)), settings.Capture, settings.Capture.Recording);
                                await failingBackend.Recorded.Task;
                                throw new IOException("Injected caller failure after inert native startup.");
                            };
                            window.RecordingPipelineForEvidence = throughMp4Pipeline ? (_, operation) => failingAction(operation) : null;
                            Func<RecordingOperation, Task> entry = throughMp4Pipeline
                                ? operation => (Task)Invoke(window, "RunMp4RecordingAsync", operation)!
                                : failingAction;
                            failedCommand = (Task)Invoke(window, "RunRecordingOperationAsync", "MP4", entry)!;
                            await UntilAsync(() => failingRecorder.IsStopUnconfirmed && failingControl.RecoveryVisible);
                            var failingOwner = Current();
                            Require(failingOwner is not null && !failingOwner.Completion.IsCompleted && !failedCommand.IsCompleted
                                && gate.IsActive && failingControl.IsVisible && failingBackend.DisposeCalls == 0
                                && ReferenceEquals(Field<ScreenRecordingService?>(window, "_recordingService"), failingRecorder),
                                "a caller exception retains the reservation, service and visible recovery control; MP4 catch=" + throughMp4Pipeline);
                            failingBackend.Complete();
                            await failedCommand.WaitAsync(TimeSpan.FromSeconds(8));
                            Require(!failingRecorder.IsRecording && failingBackend.DisposeCalls == 1 && !gate.IsActive
                                && Current() is null && !failingControl.IsVisible && failingOwner!.Completion.IsCompleted
                                && Field<ScreenRecordingService?>(window, "_recordingService") is null
                                && captureService.History.Count == initialHistoryCount,
                                "a caller exception releases resources only after native cleanup, without success history; MP4 catch=" + throughMp4Pipeline);
                            Require(unexpectedDialogs == expectedDialogs, "the caller failure is presented exactly once; MP4 catch=" + throughMp4Pipeline);
                        }
                        finally
                        {
                            failingRecorder.Stop(); failingBackend.Complete();
                            if (failedCommand is not null) await failedCommand.WaitAsync(TimeSpan.FromSeconds(8));
                            if (!failingRecorder.IsRecording) failingControl.Close();
                        }
                    }
                    window.RecordingPipelineForEvidence = null;
                }
                finally
                {
                    // Release only this inert backend; never leave an active singleton after a failed assertion.
                    recorder.Stop(); backend.Complete();
                    if (command is not null)
                    {
                        try { await command.WaitAsync(TimeSpan.FromSeconds(8)); }
                        catch (TimeoutException) when (command.IsCompleted) { }
                    }
                    if (!recorder.IsRecording) control?.Close();
                    window.DisposeCaptureEvidence();
                    window.Close();
                }
            }
            if (unexpectedDialogs != expectedDialogs)
                throw new InvalidOperationException($"Fixture dialogs: expected {expectedDialogs}, observed {unexpectedDialogs}.");
            checks.Add("Limits: injected backend and generated bytes; no real recording, audio devices, clipboard, driver failure, emergency acceptance or process termination. Audio panel/device enumeration intentionally omitted.");
            File.WriteAllLines(Path.Combine(output, "recovery-operation-result.txt"), checks);
        }
        finally { dismiss.Stop(); UiText.Apply(previousLanguage); }
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags)!.GetValue(target)!;
    private static void Set(object target, string name, object? value) => target.GetType().GetField(name, Flags)!.SetValue(target, value);
    private static object? Invoke(object target, string name, params object?[] args) => target.GetType().GetMethod(name, Flags)!.Invoke(target, args);
    private static async Task UntilAsync(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready() && watch.Elapsed < TimeSpan.FromSeconds(8)) await Task.Delay(15);
        if (!ready()) throw new TimeoutException("The inert recording recovery fixture did not reach its expected state.");
    }

    private sealed class Backend : IScreenRecorderBackendFactory, IScreenRecorderBackend
    {
        public event EventHandler<RecordingCompleteEventArgs>? Completed;
        public event EventHandler<RecordingFailedEventArgs>? Failed { add { } remove { } }
        public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
        private EventHandler<RecordingCompleteEventArgs>? _completionSnapshot;
        private FileStream? _file;
        private string _path = "";
        private int _disposeCalls;
        internal int DisposeCalls => Volatile.Read(ref _disposeCalls);
        internal TaskCompletionSource Recorded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IScreenRecorderBackend Create(RecorderOptions options)
        {
            if (options.AudioOptions.IsAudioEnabled == true) throw new InvalidOperationException("The inert fixture must disable every audio source.");
            return this;
        }
        public void Record(string path)
        {
            _path = path;
            _file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            _file.Write(Enumerable.Range(0, 48).Select(value => (byte)value).ToArray());
            _file.Flush(true);
            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Recording));
            Recorded.TrySetResult();
        }
        internal void Complete()
        {
            _completionSnapshot = Completed;
            _completionSnapshot?.Invoke(this, new RecordingCompleteEventArgs(_path, new List<FrameData>()));
        }
        internal void ReplayCompletion() => _completionSnapshot?.Invoke(this, new RecordingCompleteEventArgs(_path, new List<FrameData>()));
        public void Stop() { }
        public void Pause() { }
        public void Resume() { }
        public bool SetAudioVolumes(float inputVolume, float outputVolume) => false;
        public void Dispose() { Interlocked.Increment(ref _disposeCalls); _file?.Dispose(); _file = null; }
    }
}
