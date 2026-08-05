using System.Diagnostics;
using System.Drawing;
using System.IO;
using ScreenRecorderLib;
using SlashText.Models;

namespace SlashText.Services;

public sealed class ScreenRecordingService : IDisposable
{
    private readonly object _gate = new();
    private Recorder? _recorder;
    private Stopwatch? _clock;
    private TimeSpan _pausedAt;
    private bool _paused;
    private TimeSpan _elapsed;
    private TaskCompletionSource<string>? _completion;
    private string _path = string.Empty;
    private string _workingPath = string.Empty;

    public bool IsRecording => _recorder is not null;
    public bool IsPaused => _paused;
    public TimeSpan Elapsed => _recorder is null
        ? _elapsed
        : _pausedAt + (_paused ? TimeSpan.Zero : _clock?.Elapsed ?? TimeSpan.Zero);

    public event EventHandler<RecordingProgress>? ProgressChanged;
    public event EventHandler<string>? RecordingFailed;

    public Task<string> StartAsync(
        RecordingTarget target,
        CaptureSettings captureSettings,
        RecordingSettings settings)
    {
        lock (_gate)
        {
            if (_recorder is not null)
            {
                throw new InvalidOperationException("Já existe uma gravação em andamento.");
            }

            _path = CreateMediaPath(captureSettings, target.Type, ".mp4");
            _workingPath = CreateWorkingPath(_path);
            _completion = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _recorder = Recorder.CreateRecorder(BuildOptions(target, settings));
            _recorder.OnRecordingComplete += OnRecordingComplete;
            _recorder.OnRecordingFailed += OnRecordingFailed;
            _clock = Stopwatch.StartNew();
            _pausedAt = TimeSpan.Zero;
            _elapsed = TimeSpan.Zero;
            _paused = false;
            _recorder.Record(_workingPath);
            ProgressChanged?.Invoke(
                this,
                new RecordingProgress(TimeSpan.Zero, false, "Gravando"));
            return _completion.Task;
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            if (_recorder is null || _paused)
            {
                return;
            }
            _pausedAt += _clock?.Elapsed ?? TimeSpan.Zero;
            _clock?.Reset();
            _recorder.Pause();
            _paused = true;
            ProgressChanged?.Invoke(
                this,
                new RecordingProgress(_pausedAt, true, "Pausado"));
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            if (_recorder is null || !_paused)
            {
                return;
            }
            _recorder.Resume();
            _clock = Stopwatch.StartNew();
            _paused = false;
            ProgressChanged?.Invoke(
                this,
                new RecordingProgress(_pausedAt, false, "Gravando"));
        }
    }

    public void Stop()
    {
        Recorder? recorder;
        lock (_gate)
        {
            recorder = _recorder;
        }
        recorder?.Stop();
    }

    public void PublishTick()
    {
        if (_recorder is null)
        {
            return;
        }
        var elapsed = Elapsed;
        ProgressChanged?.Invoke(
            this,
            new RecordingProgress(elapsed, _paused, _paused ? "Pausado" : "Gravando"));
    }

    private static RecorderOptions BuildOptions(
        RecordingTarget target,
        RecordingSettings settings)
    {
        RecordingSourceBase source;
        if (target.Kind == RecordingTargetKind.Window &&
            target.WindowHandle != IntPtr.Zero)
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
        if (source is DisplayRecordingSource displaySource)
        {
            displaySource.IsCursorCaptureEnabled = settings.IncludeCursor;
        }
        else if (source is WindowRecordingSource windowSource)
        {
            windowSource.IsCursorCaptureEnabled = settings.IncludeCursor;
        }

        var bitrate = settings.VideoQuality switch
        {
            "Baixa" => 2_500_000,
            "Média" => 5_000_000,
            "Máxima" => 16_000_000,
            _ => 9_000_000
        };
        return new RecorderOptions
        {
            SourceOptions = new SourceOptions
            {
                RecordingSources = [source]
            },
            OutputOptions = new OutputOptions
            {
                RecorderMode = RecorderMode.Video,
                OutputFrameSize = target.Kind == RecordingTargetKind.Region
                    ? new ScreenSize(Even(target.Bounds.Width), Even(target.Bounds.Height))
                    : ScreenSize.Empty
            },
            AudioOptions = new AudioOptions
            {
                IsAudioEnabled = false
            },
            VideoEncoderOptions = new VideoEncoderOptions
            {
                Bitrate = bitrate,
                Framerate = Math.Clamp(settings.VideoFps, 10, 60),
                IsFixedFramerate = true,
                Encoder = new H264VideoEncoder
                {
                    BitrateMode = H264BitrateControlMode.UnconstrainedVBR,
                    EncoderProfile = H264Profile.Main
                },
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
                IsLogEnabled = false
            }
        };
    }

    private static int Even(int value) => Math.Max(2, value - value % 2);

    private void OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        var recordedPath = string.IsNullOrWhiteSpace(e.FilePath)
            ? _workingPath
            : e.FilePath;
        CleanupRecorder();
        try
        {
            ValidateMp4File(recordedPath);
            File.Move(recordedPath, _path);
            _completion?.TrySetResult(_path);
        }
        catch (Exception exception)
        {
            TryDelete(recordedPath);
            RecordingFailed?.Invoke(this, exception.Message);
            _completion?.TrySetException(exception);
        }
    }

    private void OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        var error = string.IsNullOrWhiteSpace(e.Error)
            ? "O encoder de vídeo do Windows não conseguiu concluir a gravação."
            : e.Error;
        CleanupRecorder();
        TryDelete(_workingPath);
        RecordingFailed?.Invoke(this, error);
        _completion?.TrySetException(new InvalidOperationException(error));
    }

    private void CleanupRecorder()
    {
        lock (_gate)
        {
            if (_recorder is not null)
            {
                _recorder.OnRecordingComplete -= OnRecordingComplete;
                _recorder.OnRecordingFailed -= OnRecordingFailed;
                _recorder.Dispose();
                _recorder = null;
            }
            _elapsed = _pausedAt + (_paused ? TimeSpan.Zero : _clock?.Elapsed ?? TimeSpan.Zero);
            _clock?.Stop();
            _clock = null;
            _paused = false;
        }
    }

    public static string CreateMediaPath(
        CaptureSettings settings,
        string type,
        string extension)
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
        if (stream.Length < 12)
        {
            throw new InvalidDataException("O encoder criou um arquivo MP4 vazio ou incompleto.");
        }

        Span<byte> header = stackalloc byte[12];
        if (stream.Read(header) != header.Length ||
            header[4] != (byte)'f' ||
            header[5] != (byte)'t' ||
            header[6] != (byte)'y' ||
            header[7] != (byte)'p')
        {
            throw new InvalidDataException("O encoder criou um contêiner MP4 inválido.");
        }
    }

    private static string CreateWorkingPath(string finalPath)
    {
        var directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(finalPath);
        return Path.Combine(
            directory,
            $".{name}.{Guid.NewGuid():N}.recording.mp4");
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
            // O arquivo incompleto será ignorado pelo histórico.
        }
        catch (UnauthorizedAccessException)
        {
            // O arquivo incompleto será ignorado pelo histórico.
        }
    }

    public void Dispose()
    {
        Task<string>? completion;
        lock (_gate)
        {
            completion = _recorder is null ? null : _completion?.Task;
        }

        try
        {
            Stop();
        }
        catch
        {
            // O encerramento do aplicativo não deve ficar preso no encoder.
        }

        if (completion is not null && !completion.IsCompleted)
        {
            try
            {
                completion.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // A limpeza abaixo remove qualquer MP4 incompleto.
            }
        }

        if (_recorder is not null)
        {
            CleanupRecorder();
            TryDelete(_workingPath);
        }
    }
}
