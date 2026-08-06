using System.Buffers.Binary;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using ScreenRecorderLib;
using SlashText.Models;

namespace SlashText.Services;

public sealed class ScreenRecordingService : IDisposable
{
    private const string EncoderPolicy = "H264 Media Foundation; hardware if available, software fallback";
    private static readonly TimeSpan DefaultStopTimeout = TimeSpan.FromSeconds(12);
    private readonly object _gate = new();
    private readonly IScreenRecorderBackendFactory _backendFactory;
    private readonly TimeSpan _stopTimeout;
    private IScreenRecorderBackend? _backend;
    private Task _nativeQueue = Task.CompletedTask;
    private Stopwatch? _clock;
    private TimeSpan _pausedAt;
    private TimeSpan _elapsed;
    private TaskCompletionSource<string>? _completion;
    private string _path = string.Empty;
    private string _workingPath = string.Empty;
    private ScreenRecordingState _state = ScreenRecordingState.Idle;
    private int _finalizationClaimed;
    private int _stopRequested;
    private int _pauseRequested;
    private int _timeoutReported;
    private bool _disposed;

    public ScreenRecordingService()
        : this(new ScreenRecorderBackendFactory(), DefaultStopTimeout)
    {
    }

    internal ScreenRecordingService(
        IScreenRecorderBackendFactory backendFactory,
        TimeSpan stopTimeout)
    {
        _backendFactory = backendFactory;
        _stopTimeout = stopTimeout;
    }

    public bool IsRecording
    {
        get
        {
            lock (_gate)
            {
                return _state is ScreenRecordingState.Starting or
                    ScreenRecordingState.Recording or
                    ScreenRecordingState.Paused or
                    ScreenRecordingState.Stopping or
                    ScreenRecordingState.Finalizing;
            }
        }
    }

    public bool IsPaused => Volatile.Read(ref _pauseRequested) == 1;

    public ScreenRecordingState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public TimeSpan Elapsed
    {
        get
        {
            lock (_gate)
            {
                return IsTerminal(_state)
                    ? _elapsed
                    : _pausedAt + (IsPaused ? TimeSpan.Zero : _clock?.Elapsed ?? TimeSpan.Zero);
            }
        }
    }

    public event EventHandler<RecordingProgress>? ProgressChanged;
    public event EventHandler<string>? RecordingFailed;

    public Task<string> StartAsync(
        RecordingTarget target,
        CaptureSettings captureSettings,
        RecordingSettings settings)
    {
        TaskCompletionSource<string> completion;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsRecording)
            {
                throw new InvalidOperationException("Já existe uma gravação em andamento.");
            }
            if (!Environment.Is64BitProcess)
            {
                throw new PlatformNotSupportedException(
                    "A gravação MP4 do SlashDesk requer o processo x64.");
            }

            _path = CreateMediaPath(captureSettings, target.Type, ".mp4");
            _workingPath = CreateWorkingPath(_path);
            _completion = completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _clock = Stopwatch.StartNew();
            _pausedAt = TimeSpan.Zero;
            _elapsed = TimeSpan.Zero;
            _state = ScreenRecordingState.Starting;
            Volatile.Write(ref _finalizationClaimed, 0);
            Volatile.Write(ref _stopRequested, 0);
            Volatile.Write(ref _pauseRequested, 0);
            Volatile.Write(ref _timeoutReported, 0);
        }

        AppDiagnosticLog.Write(
            "recording.start-requested",
            ("target", target.Kind.ToString()),
            ("width", target.Bounds.Width),
            ("height", target.Bounds.Height),
            ("fps", settings.VideoFps),
            ("cursor", settings.IncludeCursor),
            ("encoder", EncoderPolicy));
        AppDiagnosticLog.MarkRecordingActive(target, settings, EncoderPolicy);
        EnqueueNative("recording.start", () => StartCore(target, settings));
        PublishProgress("Inicializando MP4");
        return completion.Task;
    }

    public void Pause()
    {
        if (Interlocked.CompareExchange(ref _pauseRequested, 1, 0) != 0 ||
            Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }
        EnqueueNative("recording.pause", PauseCore);
    }

    public void Resume()
    {
        if (Interlocked.CompareExchange(ref _pauseRequested, 0, 1) != 1 ||
            Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }
        EnqueueNative("recording.resume", ResumeCore);
    }

    public void Stop()
    {
        if (Interlocked.CompareExchange(ref _stopRequested, 1, 0) != 0)
        {
            return;
        }

        lock (_gate)
        {
            if (!IsTerminal(_state))
            {
                _state = ScreenRecordingState.Stopping;
            }
        }
        AppDiagnosticLog.Write("recording.stop-requested");
        EnqueueNative("recording.stop", StopCore);
    }

    public void PublishTick() =>
        PublishProgress(IsPaused ? "Pausado" : State == ScreenRecordingState.Stopping
            ? "Finalizando MP4…"
            : "Gravando");

    private void StartCore(RecordingTarget target, RecordingSettings settings)
    {
        AppDiagnosticLog.Write("recording.backend-create-enter");
        var libraryLogPath = AppDiagnosticLog.CreateLibraryLogPath();
        var backend = _backendFactory.Create(BuildOptions(target, settings, libraryLogPath));
        backend.Completed += OnRecordingComplete;
        backend.Failed += OnRecordingFailed;
        backend.StatusChanged += OnStatusChanged;
        lock (_gate)
        {
            _backend = backend;
        }
        AppDiagnosticLog.Write(
            "recording.backend-create-return",
            ("libraryLog", Path.GetFileName(libraryLogPath)));
        AppDiagnosticLog.Write("recording.native-record-enter");
        backend.Record(_workingPath);
        AppDiagnosticLog.Write("recording.native-record-return");
        lock (_gate)
        {
            if (_state == ScreenRecordingState.Starting)
            {
                _state = ScreenRecordingState.Recording;
            }
        }
        PublishProgress("Gravando");
    }

    private void PauseCore()
    {
        var backend = BackendSnapshot();
        if (backend is null || Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }
        AppDiagnosticLog.Write("recording.native-pause-enter");
        backend.Pause();
        AppDiagnosticLog.Write("recording.native-pause-return");
        lock (_gate)
        {
            _pausedAt += _clock?.Elapsed ?? TimeSpan.Zero;
            _clock?.Reset();
            _state = ScreenRecordingState.Paused;
        }
        PublishProgress("Pausado");
    }

    private void ResumeCore()
    {
        var backend = BackendSnapshot();
        if (backend is null || Volatile.Read(ref _stopRequested) != 0)
        {
            return;
        }
        AppDiagnosticLog.Write("recording.native-resume-enter");
        backend.Resume();
        AppDiagnosticLog.Write("recording.native-resume-return");
        lock (_gate)
        {
            _clock = Stopwatch.StartNew();
            _state = ScreenRecordingState.Recording;
        }
        PublishProgress("Gravando");
    }

    private void StopCore()
    {
        var backend = BackendSnapshot();
        if (backend is null)
        {
            QueueFinalization(FinalizationRequest.Failed(
                "O gravador não foi inicializado."));
            return;
        }
        AppDiagnosticLog.Write("recording.native-stop-enter");
        backend.Stop();
        AppDiagnosticLog.Write("recording.native-stop-return");
        _ = MonitorStopTimeoutAsync();
    }

    private async Task MonitorStopTimeoutAsync()
    {
        await Task.Delay(_stopTimeout).ConfigureAwait(false);
        TaskCompletionSource<string>? completion;
        lock (_gate)
        {
            completion = _completion;
        }
        if (completion is null || completion.Task.IsCompleted ||
            Interlocked.CompareExchange(ref _timeoutReported, 1, 0) != 0)
        {
            return;
        }

        const string error =
            "O encoder nativo não confirmou a finalização dentro do prazo. " +
            "Consulte os logs antes de reiniciar a gravação.";
        AppDiagnosticLog.Write("recording.stop-timeout", ("timeoutMs", _stopTimeout.TotalMilliseconds));
        completion.TrySetException(new TimeoutException(error));
        RaiseRecordingFailed(error);
        // Do not dispose here: the native finalization callback may still be
        // running. It remains responsible for the one safe cleanup path.
    }

    private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        AppDiagnosticLog.Write("recording.callback-complete");
        QueueFinalization(FinalizationRequest.Completed(
            string.IsNullOrWhiteSpace(e.FilePath) ? _workingPath : e.FilePath));
    }

    private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        var error = string.IsNullOrWhiteSpace(e.Error)
            ? "O encoder de vídeo do Windows não conseguiu concluir a gravação."
            : e.Error;
        AppDiagnosticLog.Write("recording.callback-failed", ("error", error));
        QueueFinalization(FinalizationRequest.Failed(error));
    }

    private void OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        AppDiagnosticLog.Write("recording.callback-status", ("status", e.Status.ToString()));
        lock (_gate)
        {
            if (Volatile.Read(ref _stopRequested) != 0 || IsTerminal(_state))
            {
                return;
            }
            _state = e.Status switch
            {
                RecorderStatus.Recording => ScreenRecordingState.Recording,
                RecorderStatus.Paused => ScreenRecordingState.Paused,
                RecorderStatus.Finishing => ScreenRecordingState.Stopping,
                _ => _state
            };
        }
    }

    private void QueueFinalization(FinalizationRequest request)
    {
        if (Interlocked.CompareExchange(ref _finalizationClaimed, 1, 0) != 0)
        {
            AppDiagnosticLog.Write("recording.finalization-duplicate-ignored");
            return;
        }
        EnqueueNative("recording.finalize", () => FinalizeCore(request));
    }

    private void FinalizeCore(FinalizationRequest request)
    {
        IScreenRecorderBackend? backend;
        TaskCompletionSource<string>? completion;
        string finalPath;
        string workingPath;
        lock (_gate)
        {
            _state = ScreenRecordingState.Finalizing;
            backend = _backend;
            _backend = null;
            completion = _completion;
            finalPath = _path;
            workingPath = _workingPath;
            _elapsed = _pausedAt + (IsPaused ? TimeSpan.Zero : _clock?.Elapsed ?? TimeSpan.Zero);
            _clock?.Stop();
            _clock = null;
        }

        DetachBackend(backend);
        try
        {
            AppDiagnosticLog.Write("recording.backend-dispose-enter");
            backend?.Dispose();
            AppDiagnosticLog.Write("recording.backend-dispose-return");
        }
        catch (Exception exception)
        {
            AppDiagnosticLog.WriteException("recording.backend-dispose-exception", exception);
        }

        try
        {
            if (request.Error is not null)
            {
                throw new InvalidOperationException(request.Error);
            }
            var recordedPath = string.IsNullOrWhiteSpace(request.RecordedPath)
                ? workingPath
                : request.RecordedPath;
            ValidateMp4File(recordedPath);
            File.Move(recordedPath, finalPath);
            lock (_gate)
            {
                _state = ScreenRecordingState.Completed;
            }
            AppDiagnosticLog.Write(
                "recording.completed",
                ("durationMs", _elapsed.TotalMilliseconds),
                ("bytes", new FileInfo(finalPath).Length));
            completion?.TrySetResult(finalPath);
        }
        catch (Exception exception)
        {
            TryDelete(request.RecordedPath ?? workingPath);
            if (!string.Equals(request.RecordedPath, workingPath, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(workingPath);
            }
            lock (_gate)
            {
                _state = ScreenRecordingState.Failed;
            }
            AppDiagnosticLog.WriteException("recording.finalization-failed", exception);
            completion?.TrySetException(exception);
            RaiseRecordingFailed(exception.Message);
        }
        finally
        {
            AppDiagnosticLog.MarkRecordingEnded();
        }
    }

    private void EnqueueNative(string stage, Action operation)
    {
        lock (_gate)
        {
            _nativeQueue = _nativeQueue.ContinueWith(
                _ =>
                {
                    try
                    {
                        operation();
                    }
                    catch (Exception exception)
                    {
                        AppDiagnosticLog.WriteException(stage + ".exception", exception);
                        QueueFinalization(FinalizationRequest.Failed(exception.Message));
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    internal static RecorderOptions BuildOptions(
        RecordingTarget target,
        RecordingSettings settings,
        string libraryLogPath)
    {
        RecordingSourceBase source;
        if (target.Kind == RecordingTargetKind.Window && target.WindowHandle != IntPtr.Zero)
        {
            source = new WindowRecordingSource(target.WindowHandle);
        }
        else
        {
            var display = string.IsNullOrWhiteSpace(target.DisplayDeviceName)
                ? new DisplayRecordingSource(DisplayRecordingSource.MainMonitor)
                : new DisplayRecordingSource(target.DisplayDeviceName);
            if (target.Kind == RecordingTargetKind.Region)
            {
                var screen = System.Windows.Forms.Screen.FromRectangle(target.Bounds);
                display.SourceRect = new ScreenRect(
                    Math.Max(0, target.Bounds.Left - screen.Bounds.Left),
                    Math.Max(0, target.Bounds.Top - screen.Bounds.Top),
                    Even(target.Bounds.Width),
                    Even(target.Bounds.Height));
            }
            source = display;
        }
        source.IsCursorCaptureEnabled = settings.IncludeCursor;

        var (bitrate, quality) = settings.VideoQuality switch
        {
            "Baixa" => (2_500_000, 55),
            "Média" => (5_000_000, 70),
            "Máxima" => (16_000_000, 95),
            _ => (9_000_000, 85)
        };
        return new RecorderOptions
        {
            SourceOptions = new SourceOptions { RecordingSources = [source] },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                OutputFrameSize = target.Kind == RecordingTargetKind.Region
                    ? new ScreenSize(Even(target.Bounds.Width), Even(target.Bounds.Height))
                    : ScreenSize.Empty
            },
            AudioOptions = new AudioOptions { IsAudioEnabled = false },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Bitrate = bitrate,
                Quality = quality,
                Framerate = Math.Clamp(settings.VideoFps, 10, 60),
                IsFixedFramerate = false,
                IsThrottlingDisabled = false,
                Encoder = new H264VideoEncoder
                {
                    BitrateMode = H264BitrateControlMode.Quality,
                    EncoderProfile = H264Profile.Main
                },
                // ScreenRecorderLib defines this as "hardware if available".
                // Media Foundation can still select its software transform.
                IsHardwareEncodingEnabled = true,
                IsLowLatencyEnabled = false,
                IsMp4FastStartEnabled = true
            },
            MouseOptions = new MouseOptions
            {
                IsMousePointerEnabled = settings.IncludeCursor,
                IsMouseClicksDetected = false
            },
            LogOptions = new LogOptions
            {
                IsLogEnabled = true,
                LogFilePath = libraryLogPath,
                LogSeverityLevel = LogLevel.Trace
            }
        };
    }

    private static int Even(int value) => Math.Max(2, value - value % 2);

    public static string CreateMediaPath(CaptureSettings settings, string type, string extension)
    {
        var now = DateTimeOffset.Now;
        var directory = CaptureService.ResolveDirectoryTemplate(
            Environment.ExpandEnvironmentVariables(settings.OutputDirectoryTemplate),
            now);
        Directory.CreateDirectory(directory);
        var baseName = settings.FileNameTemplate
            .Replace("{date}", now.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", now.ToString("HH-mm-ss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{type}", type, StringComparison.OrdinalIgnoreCase)
            .Replace("{app}", "desktop", StringComparison.OrdinalIgnoreCase);
        baseName = CaptureService.SanitizeFileName(baseName);
        var path = Path.Combine(directory, baseName + extension);
        for (var index = 2; File.Exists(path); index++)
        {
            path = Path.Combine(directory, $"{baseName}_{index}{extension}");
        }
        return path;
    }

    public static void ValidateMp4File(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException("O encoder não criou o arquivo MP4.");
        }
        using var stream = File.OpenRead(path);
        if (stream.Length < 32)
        {
            throw new InvalidDataException("O encoder criou um arquivo MP4 vazio ou incompleto.");
        }

        var foundFtyp = false;
        var foundMoov = false;
        var foundMdat = false;
        Span<byte> header = stackalloc byte[8];
        while (stream.Position + header.Length <= stream.Length)
        {
            if (stream.Read(header) != header.Length)
            {
                break;
            }
            var size = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
            var type = System.Text.Encoding.ASCII.GetString(header[4..8]);
            long boxSize = size;
            if (size == 1)
            {
                Span<byte> extended = stackalloc byte[8];
                if (stream.Read(extended) != extended.Length)
                {
                    break;
                }
                boxSize = (long)BinaryPrimitives.ReadUInt64BigEndian(extended);
            }
            else if (size == 0)
            {
                boxSize = stream.Length - (stream.Position - 8);
            }
            var headerSize = size == 1 ? 16 : 8;
            if (boxSize < headerSize || stream.Position + boxSize - headerSize > stream.Length)
            {
                throw new InvalidDataException("O encoder criou uma estrutura MP4 truncada.");
            }
            foundFtyp |= type == "ftyp";
            foundMoov |= type == "moov";
            foundMdat |= type == "mdat";
            stream.Position += boxSize - headerSize;
        }
        if (!foundFtyp || !foundMoov || !foundMdat)
        {
            throw new InvalidDataException(
                "O encoder não concluiu a estrutura MP4 (ftyp/moov/mdat)." );
        }
    }

    private static string CreateWorkingPath(string finalPath)
    {
        var directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(finalPath);
        return Path.Combine(directory, $".{name}.{Guid.NewGuid():N}.recording.mp4");
    }

    private IScreenRecorderBackend? BackendSnapshot()
    {
        lock (_gate)
        {
            return _backend;
        }
    }

    private static bool IsTerminal(ScreenRecordingState state) => state is
        ScreenRecordingState.Completed or ScreenRecordingState.Failed or ScreenRecordingState.Disposed;

    private void PublishProgress(string status)
    {
        try
        {
            ProgressChanged?.Invoke(this, new RecordingProgress(Elapsed, IsPaused, status));
        }
        catch (Exception exception)
        {
            AppDiagnosticLog.WriteException("recording.progress-observer-exception", exception);
        }
    }

    private void RaiseRecordingFailed(string message)
    {
        try
        {
            RecordingFailed?.Invoke(this, message);
        }
        catch (Exception exception)
        {
            AppDiagnosticLog.WriteException("recording.failure-observer-exception", exception);
        }
    }

    private void DetachBackend(IScreenRecorderBackend? backend)
    {
        if (backend is null)
        {
            return;
        }
        backend.Completed -= OnRecordingComplete;
        backend.Failed -= OnRecordingFailed;
        backend.StatusChanged -= OnStatusChanged;
    }

    private static void TryDelete(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            if (_state == ScreenRecordingState.Idle)
            {
                _state = ScreenRecordingState.Disposed;
                return;
            }
        }

        Stop();
        Task<string>? completion;
        lock (_gate)
        {
            completion = _completion?.Task;
        }
        if (completion is null || completion.IsCompleted)
        {
            return;
        }
        try
        {
            completion.Wait(_stopTimeout + TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Timeout/failure is logged and native cleanup stays callback-owned.
        }
    }

    private sealed record FinalizationRequest(string? RecordedPath, string? Error)
    {
        public static FinalizationRequest Completed(string path) => new(path, null);
        public static FinalizationRequest Failed(string error) => new(null, error);
    }
}
