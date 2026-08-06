using ScreenRecorderLib;

namespace SlashText.Services;

internal interface IScreenRecorderBackend : IDisposable
{
    event EventHandler<RecordingCompleteEventArgs>? Completed;
    event EventHandler<RecordingFailedEventArgs>? Failed;
    event EventHandler<RecordingStatusEventArgs>? StatusChanged;

    void Record(string path);
    void Pause();
    void Resume();
    void Stop();
}

internal interface IScreenRecorderBackendFactory
{
    IScreenRecorderBackend Create(RecorderOptions options);
}

internal sealed class ScreenRecorderBackendFactory : IScreenRecorderBackendFactory
{
    public IScreenRecorderBackend Create(RecorderOptions options) =>
        new ScreenRecorderBackend(Recorder.CreateRecorder(options));
}

internal sealed class ScreenRecorderBackend : IScreenRecorderBackend
{
    private Recorder? _recorder;

    public ScreenRecorderBackend(Recorder recorder)
    {
        _recorder = recorder;
        recorder.OnRecordingComplete += OnCompleted;
        recorder.OnRecordingFailed += OnFailed;
        recorder.OnStatusChanged += OnStatusChanged;
    }

    public event EventHandler<RecordingCompleteEventArgs>? Completed;
    public event EventHandler<RecordingFailedEventArgs>? Failed;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;

    public void Record(string path) => Recorder.Record(path);
    public void Pause() => Recorder.Pause();
    public void Resume() => Recorder.Resume();
    public void Stop() => Recorder.Stop();

    public void Dispose()
    {
        var recorder = Interlocked.Exchange(ref _recorder, null);
        if (recorder is null)
        {
            return;
        }
        recorder.OnRecordingComplete -= OnCompleted;
        recorder.OnRecordingFailed -= OnFailed;
        recorder.OnStatusChanged -= OnStatusChanged;
        recorder.Dispose();
    }

    private Recorder Recorder => _recorder ??
        throw new ObjectDisposedException(nameof(ScreenRecorderBackend));

    private void OnCompleted(object? sender, RecordingCompleteEventArgs e) =>
        Completed?.Invoke(this, e);

    private void OnFailed(object? sender, RecordingFailedEventArgs e) =>
        Failed?.Invoke(this, e);

    private void OnStatusChanged(object? sender, RecordingStatusEventArgs e) =>
        StatusChanged?.Invoke(this, e);
}
