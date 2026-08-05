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
        if (recording.Frames.Count == 0)
        {
            throw new InvalidOperationException("O GIF não contém quadros para salvar.");
        }

        var path = ScreenRecordingService.CreateMediaPath(
            captureSettings,
            type,
            ".gif");
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        var encoder = new GifBitmapEncoder();
        var delay = Math.Max(2, (int)Math.Round(100d / recording.Fps));
        for (var index = 0; index < recording.Frames.Count; index++)
        {
            var bitmap = recording.Frames[index];
            var source = ToBitmapSource(bitmap);
            var metadata = new BitmapMetadata("gif");
            metadata.SetQuery("/grctlext/Delay", (ushort)delay);
            metadata.SetQuery("/grctlext/Disposal", (byte)2);
            encoder.Frames.Add(BitmapFrame.Create(
                source,
                null,
                metadata,
                null));
        }

        try
        {
            using (var stream = File.Create(temporaryPath))
            {
                encoder.Save(stream);
                stream.Flush(flushToDisk: true);
            }
            EnsureLoopExtension(temporaryPath);
            File.Move(temporaryPath, path);
            return path;
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    private static void EnsureLoopExtension(string path)
    {
        var gif = File.ReadAllBytes(path);
        if (gif.Length < 13 ||
            gif[0] != (byte)'G' ||
            gif[1] != (byte)'I' ||
            gif[2] != (byte)'F')
        {
            throw new InvalidDataException("O encoder criou um contêiner GIF inválido.");
        }

        ReadOnlySpan<byte> applicationId = "NETSCAPE2.0"u8;
        if (gif.AsSpan().IndexOf(applicationId) >= 0)
        {
            return;
        }

        var packedFields = gif[10];
        var colorTableLength = (packedFields & 0x80) == 0
            ? 0
            : 3 * (1 << ((packedFields & 0x07) + 1));
        var insertionOffset = 13 + colorTableLength;
        if (insertionOffset > gif.Length)
        {
            throw new InvalidDataException("A tabela de cores do GIF está incompleta.");
        }

        ReadOnlySpan<byte> loopExtension =
        [
            0x21, 0xFF, 0x0B,
            (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A',
            (byte)'P', (byte)'E', (byte)'2', (byte)'.', (byte)'0',
            0x03, 0x01, 0x00, 0x00, 0x00
        ];
        var result = new byte[gif.Length + loopExtension.Length];
        gif.AsSpan(0, insertionOffset).CopyTo(result);
        loopExtension.CopyTo(result.AsSpan(insertionOffset));
        gif.AsSpan(insertionOffset).CopyTo(
            result.AsSpan(insertionOffset + loopExtension.Length));
        File.WriteAllBytes(path, result);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Um arquivo temporário bloqueado não deve ocultar o erro original.
        }
        catch (UnauthorizedAccessException)
        {
            // Um arquivo temporário bloqueado não deve ocultar o erro original.
        }
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
