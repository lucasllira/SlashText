using System.Drawing;

namespace SlashText.Models;

public enum RecordingTargetKind
{
    Monitor,
    Region,
    Window
}

public enum ScreenRecordingState
{
    Idle,
    Starting,
    Recording,
    Paused,
    Stopping,
    Finalizing,
    Completed,
    Failed,
    Disposed
}

public sealed record RecordingTarget(
    RecordingTargetKind Kind,
    Rectangle Bounds,
    IntPtr WindowHandle = default,
    string? DisplayDeviceName = null)
{
    public string Type => Kind switch
    {
        RecordingTargetKind.Monitor => "monitor",
        RecordingTargetKind.Region => "regiao",
        _ => "janela"
    };
}

public sealed record RecordingProgress(
    TimeSpan Elapsed,
    bool IsPaused,
    string Status);

public sealed record GifCaptureMetrics(
    int CapturedFrames,
    int StoredFrames,
    int DuplicateFrames,
    double CaptureMilliseconds,
    double ResizeMilliseconds,
    double QueueWaitMilliseconds);

public sealed class GifRecordingResult : IDisposable
{
    public GifRecordingResult(
        IReadOnlyList<System.Drawing.Bitmap> frames,
        int fps,
        Rectangle bounds,
        IReadOnlyList<int>? frameDelaysCentiseconds = null,
        GifCaptureMetrics? metrics = null)
    {
        Frames = frames;
        Fps = fps;
        Bounds = bounds;
        FrameDelaysCentiseconds = frameDelaysCentiseconds ??
            Enumerable.Repeat(Math.Max(2, (int)Math.Round(100d / Math.Max(1, fps))), frames.Count)
                .ToArray();
        Metrics = metrics;
    }

    public IReadOnlyList<System.Drawing.Bitmap> Frames { get; }
    public int Fps { get; }
    public Rectangle Bounds { get; }
    public IReadOnlyList<int> FrameDelaysCentiseconds { get; }
    public GifCaptureMetrics? Metrics { get; }
    public TimeSpan Duration => TimeSpan.FromMilliseconds(
        FrameDelaysCentiseconds.Sum() * 10d);

    public void Dispose()
    {
        foreach (var frame in Frames)
        {
            frame.Dispose();
        }
    }
}
