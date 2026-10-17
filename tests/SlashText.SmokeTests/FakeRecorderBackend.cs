using SlashText.Services;
using ScreenRecorderLib;

sealed class FakeRecorderBackendFactory : IScreenRecorderBackendFactory
{
    public FakeRecorderBackend Backend { get; } = new();
    public bool SendDuplicateCallbacks
    {
        get => Backend.SendDuplicateCallbacks;
        init => Backend.SendDuplicateCallbacks = value;
    }
    public bool CompleteOnStop
    {
        get => Backend.CompleteOnStop;
        init => Backend.CompleteOnStop = value;
    }
    public bool FailOnStop
    {
        get => Backend.FailOnStop;
        init => Backend.FailOnStop = value;
    }
    public int CallbackDelayMs
    {
        get => Backend.CallbackDelayMs;
        init => Backend.CallbackDelayMs = value;
    }
    public bool BlockStop
    {
        get => Backend.BlockStop;
        init => Backend.BlockStop = value;
    }

    public IScreenRecorderBackend Create(RecorderOptions options) => Backend;
}

sealed class FakeRecorderBackend : IScreenRecorderBackend
{
    private int _activeCalls;
    private int _maximumConcurrentCalls;
    private int _disposeCalls;
    private int _stopCalls;
    private string _path = string.Empty;
    private readonly ManualResetEventSlim _stopRelease = new(false);

    public event EventHandler<RecordingCompleteEventArgs>? Completed;
    public event EventHandler<RecordingFailedEventArgs>? Failed;
    public event EventHandler<RecordingStatusEventArgs>? StatusChanged;
    public ManualResetEventSlim RecordCalled { get; } = new(false);
    public ManualResetEventSlim StopEntered { get; } = new(false);
    public bool SendDuplicateCallbacks { get; set; }
    public bool CompleteOnStop { get; set; } = true;
    public bool FailOnStop { get; set; }
    public int CallbackDelayMs { get; set; }
    public bool BlockStop { get; set; }
    public int DisposeCalls => Volatile.Read(ref _disposeCalls);
    public int StopCalls => Volatile.Read(ref _stopCalls);
    public int MaximumConcurrentCalls => Volatile.Read(ref _maximumConcurrentCalls);
    public float InputVolume { get; private set; }
    public float OutputVolume { get; private set; }
    public bool RejectAudioChanges { get; set; }
    public bool SetAudioVolumes(float inputVolume, float outputVolume)
    {
        NativeCall(() => { if (!RejectAudioChanges) { InputVolume = inputVolume; OutputVolume = outputVolume; } });
        return !RejectAudioChanges;
    }

    public void Record(string path)
    {
        NativeCall(() =>
        {
            _path = path;
            RecordCalled.Set();
            StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Recording));
        });
    }

    public void Pause() => NativeCall(() =>
        StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Paused)));

    public void Resume() => NativeCall(() =>
        StatusChanged?.Invoke(this, new RecordingStatusEventArgs(RecorderStatus.Recording)));

    public void Stop()
    {
        NativeCall(() =>
        {
            Interlocked.Increment(ref _stopCalls);
            StopEntered.Set();
            if (BlockStop)
            {
                _stopRelease.Wait(TimeSpan.FromSeconds(3));
            }
            if (FailOnStop)
            {
                Failed?.Invoke(this, new RecordingFailedEventArgs("falha simulada", _path));
                return;
            }
            if (CompleteOnStop)
            {
                if (CallbackDelayMs > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(CallbackDelayMs);
                        CompleteLater();
                    });
                }
                else
                {
                    CompleteLater();
                }
            }
        });
    }

    public void ReleaseStop() => _stopRelease.Set();

    public void CompleteLater()
    {
        WriteValidMp4(_path);
        Completed?.Invoke(
            this,
            new RecordingCompleteEventArgs(_path, new List<FrameData>()));
        if (SendDuplicateCallbacks)
        {
            Failed?.Invoke(
                this,
                new RecordingFailedEventArgs("callback tardio", _path));
        }
    }

    public void Dispose()
    {
        NativeCall(() => Interlocked.Increment(ref _disposeCalls));
    }

    private void NativeCall(Action action)
    {
        var concurrent = Interlocked.Increment(ref _activeCalls);
        UpdateMaximum(concurrent);
        try
        {
            Thread.Sleep(15);
            action();
        }
        finally
        {
            Interlocked.Decrement(ref _activeCalls);
        }
    }

    private void UpdateMaximum(int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref _maximumConcurrentCalls);
            if (current >= value ||
                Interlocked.CompareExchange(ref _maximumConcurrentCalls, value, current) == current)
            {
                return;
            }
        }
    }

    private static void WriteValidMp4(string path) => File.WriteAllBytes(
        path,
        [
            0, 0, 0, 12, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0, 0, 0, 0,
            0, 0, 0, 12, (byte)'m', (byte)'d', (byte)'a', (byte)'t', 1, 2, 3, 4,
            0, 0, 0, 8, (byte)'m', (byte)'o', (byte)'o', (byte)'v'
        ]);
}
