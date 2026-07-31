using System.Drawing;

namespace SlashText.Models;

public enum RecordingTargetKind
{
    Monitor,
    Region,
    Window
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

public sealed record GifRecordingResult(
    IReadOnlyList<System.Drawing.Bitmap> Frames,
    int Fps,
    Rectangle Bounds) : IDisposable
{
    public TimeSpan Duration => TimeSpan.FromSeconds(Frames.Count / (double)Math.Max(1, Fps));

    public void Dispose()
    {
        foreach (var frame in Frames)
        {
            frame.Dispose();
        }
    }
}
