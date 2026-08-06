using System.Buffers;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using SlashText.Models;

namespace SlashText.Services;

public sealed class GifRecordingService
{
    private const int QueueCapacity = 2;
    private readonly Func<Rectangle, bool, Bitmap> _captureFrame;

    public GifRecordingService()
        : this(CaptureService.CaptureBitmap)
    {
    }

    internal GifRecordingService(Func<Rectangle, bool, Bitmap> captureFrame)
    {
        _captureFrame = captureFrame;
    }

    public Task<GifRecordingResult> CaptureAsync(
        Rectangle bounds,
        RecordingSettings settings,
        IProgress<RecordingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateSettings(settings);
        return Task.Run(
            () => CapturePipelineAsync(bounds, settings, progress, cancellationToken),
            cancellationToken);
    }

    private async Task<GifRecordingResult> CapturePipelineAsync(
        Rectangle bounds,
        RecordingSettings settings,
        IProgress<RecordingProgress>? progress,
        CancellationToken cancellationToken)
    {
        var frameCount = settings.GifFps * settings.GifDurationSeconds;
        var delay = TimeSpan.FromMilliseconds(1000d / settings.GifFps);
        var delayCentiseconds = Math.Max(2, (int)Math.Round(100d / settings.GifFps));
        var channel = Channel.CreateBounded<CapturedFrame>(new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        var frames = new List<Bitmap>(frameCount);
        var delays = new List<int>(frameCount);
        var clock = Stopwatch.StartNew();
        long captureTicks = 0;
        long resizeTicks = 0;
        long queueWaitTicks = 0;
        var duplicates = 0;

        AppDiagnosticLog.Write(
            "gif.capture-start",
            ("width", bounds.Width),
            ("height", bounds.Height),
            ("fps", settings.GifFps),
            ("durationSeconds", settings.GifDurationSeconds),
            ("outputWidth", settings.GifWidth),
            ("queueCapacity", QueueCapacity));

        var producer = Task.Run(async () =>
        {
            Exception? failure = null;
            try
            {
                for (var index = 0; index < frameCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target = TimeSpan.FromTicks(delay.Ticks * index);
                    var wait = target - clock.Elapsed;
                    if (wait > TimeSpan.Zero)
                    {
                        await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                    }

                    var captureStarted = Stopwatch.GetTimestamp();
                    var bitmap = _captureFrame(bounds, settings.IncludeCursor);
                    Interlocked.Add(ref captureTicks, Stopwatch.GetTimestamp() - captureStarted);
                    try
                    {
                        var queueStarted = Stopwatch.GetTimestamp();
                        await channel.Writer.WriteAsync(
                            new CapturedFrame(index, bitmap),
                            cancellationToken).ConfigureAwait(false);
                        Interlocked.Add(ref queueWaitTicks, Stopwatch.GetTimestamp() - queueStarted);
                    }
                    catch
                    {
                        bitmap.Dispose();
                        throw;
                    }
                }
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }
            finally
            {
                channel.Writer.TryComplete(failure);
            }
        }, cancellationToken);

        byte[]? previousPixels = null;
        var previousLength = 0;
        var consumer = Task.Run(async () =>
        {
            await foreach (var captured in channel.Reader.ReadAllAsync(cancellationToken)
                               .ConfigureAwait(false))
            {
                using (captured.Bitmap)
                {
                    var resizeStarted = Stopwatch.GetTimestamp();
                    var resized = Resize(captured.Bitmap, settings.GifWidth, settings.GifQuality);
                    Interlocked.Add(ref resizeTicks, Stopwatch.GetTimestamp() - resizeStarted);
                    if (IsDuplicate(resized, ref previousPixels, ref previousLength))
                    {
                        resized.Dispose();
                        delays[^1] += delayCentiseconds;
                        duplicates++;
                    }
                    else
                    {
                        frames.Add(resized);
                        delays.Add(delayCentiseconds);
                    }
                }
                progress?.Report(new RecordingProgress(
                    clock.Elapsed,
                    false,
                    $"Capturando GIF {captured.Index + 1}/{frameCount}"));
            }
        }, cancellationToken);

        try
        {
            await Task.WhenAll(producer, consumer).ConfigureAwait(false);
            var metrics = new GifCaptureMetrics(
                frameCount,
                frames.Count,
                duplicates,
                TicksToMilliseconds(captureTicks),
                TicksToMilliseconds(resizeTicks),
                TicksToMilliseconds(queueWaitTicks));
            AppDiagnosticLog.Write(
                "gif.capture-complete",
                ("capturedFrames", metrics.CapturedFrames),
                ("storedFrames", metrics.StoredFrames),
                ("duplicateFrames", metrics.DuplicateFrames),
                ("captureMs", metrics.CaptureMilliseconds),
                ("resizeMs", metrics.ResizeMilliseconds),
                ("queueWaitMs", metrics.QueueWaitMilliseconds));
            return new GifRecordingResult(frames, settings.GifFps, bounds, delays, metrics);
        }
        catch (Exception exception)
        {
            foreach (var frame in frames)
            {
                frame.Dispose();
            }
            while (channel.Reader.TryRead(out var remaining))
            {
                remaining.Bitmap.Dispose();
            }
            AppDiagnosticLog.WriteException("gif.capture-failed", exception);
            throw;
        }
        finally
        {
            if (previousPixels is not null)
            {
                ArrayPool<byte>.Shared.Return(previousPixels);
            }
        }
    }

    public Task<string> SaveAsync(
        GifRecordingResult recording,
        CaptureSettings captureSettings,
        string type,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Save(recording, captureSettings, type), cancellationToken);

    public string Save(
        GifRecordingResult recording,
        CaptureSettings captureSettings,
        string type)
    {
        if (recording.Frames.Count == 0)
        {
            throw new InvalidOperationException("O GIF não contém quadros para salvar.");
        }

        var encodeClock = Stopwatch.StartNew();
        var path = ScreenRecordingService.CreateMediaPath(captureSettings, type, ".gif");
        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        var encoder = new GifBitmapEncoder();
        for (var index = 0; index < recording.Frames.Count; index++)
        {
            var source = ToBitmapSource(recording.Frames[index]);
            var metadata = new BitmapMetadata("gif");
            metadata.SetQuery(
                "/grctlext/Delay",
                (ushort)Math.Clamp(recording.FrameDelaysCentiseconds[index], 2, ushort.MaxValue));
            metadata.SetQuery("/grctlext/Disposal", (byte)2);
            encoder.Frames.Add(BitmapFrame.Create(source, null, metadata, null));
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
            AppDiagnosticLog.Write(
                "gif.encode-complete",
                ("frames", recording.Frames.Count),
                ("encodeMs", encodeClock.Elapsed.TotalMilliseconds),
                ("bytes", new FileInfo(path).Length));
            return path;
        }
        catch (Exception exception)
        {
            TryDelete(temporaryPath);
            AppDiagnosticLog.WriteException("gif.encode-failed", exception);
            throw;
        }
    }

    internal static void ValidateSettings(RecordingSettings settings)
    {
        if (settings.GifFps is < 2 or > 20 ||
            settings.GifDurationSeconds is < 1 or > 30 ||
            settings.GifWidth is < 240 or > 1920 ||
            settings.GifQuality is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "GIF: FPS 2–20, duração 1–30 s, largura 240–1920 e qualidade 1–100.");
        }
    }

    private static bool IsDuplicate(
        Bitmap bitmap,
        ref byte[]? previousPixels,
        ref int previousLength)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        byte[]? current = null;
        try
        {
            var length = Math.Abs(data.Stride) * data.Height;
            current = ArrayPool<byte>.Shared.Rent(length);
            Marshal.Copy(data.Scan0, current, 0, length);
            var duplicate = previousPixels is not null &&
                previousLength == length &&
                current.AsSpan(0, length).SequenceEqual(previousPixels.AsSpan(0, length));
            if (duplicate)
            {
                ArrayPool<byte>.Shared.Return(current);
                current = null;
                return true;
            }

            if (previousPixels is not null)
            {
                ArrayPool<byte>.Shared.Return(previousPixels);
            }
            previousPixels = current;
            previousLength = length;
            current = null;
            return false;
        }
        finally
        {
            bitmap.UnlockBits(data);
            if (current is not null)
            {
                ArrayPool<byte>.Shared.Return(current);
            }
        }
    }

    private static double TicksToMilliseconds(long ticks) =>
        ticks * 1000d / Stopwatch.Frequency;

    private static void EnsureLoopExtension(string path)
    {
        var gif = File.ReadAllBytes(path);
        if (gif.Length < 13 || gif[0] != 'G' || gif[1] != 'I' || gif[2] != 'F')
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
        gif.AsSpan(insertionOffset).CopyTo(result.AsSpan(insertionOffset + loopExtension.Length));
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static Bitmap Resize(Bitmap source, int requestedWidth, int quality)
    {
        var width = requestedWidth <= 0 ? source.Width : Math.Min(source.Width, requestedWidth);
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
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);

    private sealed record CapturedFrame(int Index, Bitmap Bitmap);
}
