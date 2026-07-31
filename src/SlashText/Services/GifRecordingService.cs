using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;
using SlashText.Models;

namespace SlashText.Services;

public sealed class GifRecordingService
{
    public async Task<GifRecordingResult> CaptureAsync(
        Rectangle bounds,
        RecordingSettings settings,
        IProgress<RecordingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fps = Math.Clamp(settings.GifFps, 2, 20);
        var duration = Math.Clamp(settings.GifDurationSeconds, 1, 30);
        var frameCount = fps * duration;
        var delay = TimeSpan.FromMilliseconds(1000d / fps);
        var frames = new List<Bitmap>(frameCount);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            for (var index = 0; index < frameCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = CaptureService.CaptureBitmap(
                    bounds,
                    settings.IncludeCursor);
                frames.Add(Resize(source, settings.GifWidth, settings.GifQuality));
                progress?.Report(new RecordingProgress(
                    clock.Elapsed,
                    false,
                    $"Capturando GIF {index + 1}/{frameCount}"));
                var target = TimeSpan.FromTicks(delay.Ticks * (index + 1));
                var wait = target - clock.Elapsed;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, cancellationToken);
                }
            }
            return new GifRecordingResult(frames, fps, bounds);
        }
        catch
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
            throw;
        }
    }

    public string Save(
        GifRecordingResult recording,
        CaptureSettings captureSettings,
        string type)
    {
        var path = ScreenRecordingService.CreateMediaPath(
            captureSettings,
            type,
            ".gif");
        var encoder = new GifBitmapEncoder();
        var delay = Math.Max(2, (int)Math.Round(100d / recording.Fps));
        for (var index = 0; index < recording.Frames.Count; index++)
        {
            var bitmap = recording.Frames[index];
            var source = ToBitmapSource(bitmap);
            var metadata = new BitmapMetadata("gif");
            metadata.SetQuery("/grctlext/Delay", (ushort)delay);
            metadata.SetQuery("/grctlext/Disposal", (byte)2);
            if (index == 0)
            {
                metadata.SetQuery("/appext/application", "NETSCAPE2.0");
                metadata.SetQuery("/appext/data", new byte[] { 3, 1, 0, 0, 0 });
            }
            encoder.Frames.Add(BitmapFrame.Create(
                source,
                null,
                metadata,
                null));
        }
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static Bitmap Resize(Bitmap source, int requestedWidth, int quality)
    {
        var width = requestedWidth <= 0
            ? source.Width
            : Math.Min(source.Width, Math.Clamp(requestedWidth, 240, 1920));
        var height = Math.Max(1, (int)Math.Round(source.Height * width / (double)source.Width));
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.CompositingQuality = quality >= 70
            ? CompositingQuality.HighQuality
            : CompositingQuality.HighSpeed;
        graphics.InterpolationMode = quality >= 70
            ? InterpolationMode.HighQualityBicubic
            : InterpolationMode.Bilinear;
        graphics.DrawImage(source, 0, 0, width, height);
        return result;
    }

    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
