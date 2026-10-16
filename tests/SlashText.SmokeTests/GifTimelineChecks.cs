using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SlashText.Models;
using SlashText.Services;
using Color = System.Drawing.Color;

internal static class GifTimelineChecks
{
    private static readonly Rectangle Bounds = new(0, 0, 16, 12);
    private static RecordingSettings Settings(int fps = 10) => new()
        { GifFps = fps, GifQuality = 128, GifDurationSeconds = 1 };

    internal static async Task RunAsync(string output)
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output);
        var previous = AppPaths.Current;
        var rows = new List<object>();
        AppPaths.Initialize(new AppDataEnvironment(DistributionMode.Portable,
            Path.Combine(output, "fixture"), output, isCapturePilot: true));
        using var foreign = TemporarySession.Create("gif");
        var protectedFolders = Directory.GetDirectories(TemporarySession.Root).Order().ToArray();
        try
        {
            // Deterministic cadence/quantization check, independent of host speed.
            // 30 FPS has fractional centiseconds; rounding each period loses 10%.
            foreach (var fps in new[] { 10, 20, 30 })
            {
                var delays = Enumerable.Range(0, fps * 10).Select(i =>
                    GifRecordingService.DelayBetween(TimeSpan.FromSeconds(i / (double)fps),
                        TimeSpan.FromSeconds((i + 1d) / fps))).ToArray();
                Require(delays.Sum() == 1000 && delays.All(d => d >= 2),
                    $"{fps} FPS: ten-second timeline, centisecond boundaries, minimum delay", delays);
            }

            // No real wait: long static holds must survive the GIF 16-bit delay
            // limit, including a remainder of one centisecond.
            foreach (var hold in new[] { 65535, 65536, 131071 })
            {
                using var result = new GifRecordingResult([Frame(0)], 10, Bounds, [hold]);
                var path = new GifRecordingService().Save(result, new CaptureSettings
                    { OutputDirectoryTemplate = output, FileNameTemplate = $"long-hold-{hold}" }, "gif");
                using var stream = File.OpenRead(path);
                var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                var decoded = decoder.Frames.Select(f => Convert.ToInt32(((BitmapMetadata)f.Metadata).GetQuery("/grctlext/Delay"))).ToArray();
                Require(decoded.Sum() == hold && decoded.All(d => d is >= 2 and <= 65535),
                    "long hold preserves total decoded duration without a sub-minimum remainder", new { hold, decoded });
                foreach (var frame in decoder.Frames) RequirePixel(frame, Color.Red, "long hold repeats original pixels", decoded);
                rows.Add(new { name = "encoded-long-hold", hold, decoded });
            }

            var invalid = Path.Combine(output, "invalid-generated.gif");
            foreach (var bytes in new byte[][] { [0, 1, 2], "GIF89a\0\0\0\0\0\0\0\x2C"u8.ToArray(),
                         "GIF89a\0\0\0\0\0\0\0\x3B"u8.ToArray() })
            {
                File.WriteAllBytes(invalid, bytes);
                try { GifTimingMetadata.Write(invalid, [100]); throw new InvalidOperationException("Malformed GIF accepted"); }
                catch (InvalidDataException) { }
                Require(File.ReadAllBytes(invalid).SequenceEqual(bytes), "invalid generated structure rejected without partial rewrite", bytes);
            }

            foreach (var delay in new[] { 0, 250, 400 })
            {
                var service = new GifRecordingService((_, _) =>
                { if (delay > 0) Thread.Sleep(delay); return Frame(0); });
                using var result = await service.CaptureAsync(Bounds, Settings()).WaitAsync(TimeSpan.FromSeconds(15));
                Check($"bounded-static-{delay}ms", service, result, assessThroughput: delay == 0);
                Require(result.FrameCount == 1, "static capture deduplicates to one image", Snapshot(result));
            }

            // Hold the very first capture so startup latency is guaranteed rather
            // than inferred from how busy this machine happens to be.
            using (var release = new ManualResetEventSlim())
            {
                var entered = Signal(); var processed = Signal();
                var first = true;
                var service = new GifRecordingService((_, _) =>
                {
                    if (first) { first = false; entered.TrySetResult(); Gate(release); }
                    return Frame(0);
                });
                await using var session = service.StartRecording(Bounds, Settings());
                session.ProgressChanged += (_, p) => { if (IsFrame(p)) processed.TrySetResult(); };
                try
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await Task.Delay(350); release.Set();
                    await processed.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    session.Stop();
                    using var result = await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
                    Check("late-first-frame", service, result, session.Elapsed);
                    Require(result.Duration >= TimeSpan.FromMilliseconds(340),
                        "startup time belongs to the first available image", Snapshot(result));
                }
                finally { release.Set(); session.Stop(); }
            }

            foreach (var fps in new[] { 10, 20, 30 })
            {
                var captures = 0; var processed = 0; var enough = Signal();
                var service = new GifRecordingService((_, _) => Frame(Interlocked.Increment(ref captures) - 1));
                await using var session = service.StartRecording(Bounds, Settings(fps));
                session.ProgressChanged += (_, p) =>
                { if (IsFrame(p) && Interlocked.Increment(ref processed) >= fps + 1) enough.TrySetResult(); };
                try
                {
                    await enough.Task.WaitAsync(TimeSpan.FromSeconds(15)); session.Stop();
                    using var result = await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
                    Check($"changing-{fps}fps", service, result, session.Elapsed, changing: true, assessThroughput: true, expectedFps: fps);
                    Require(result.FrameCount >= fps, "changing images remain distinct", Snapshot(result));
                }
                finally { session.Stop(); }
            }

            // Pause while a capture is in flight; the gate represents latency,
            // not CPU load. Paused wall time must never enter the saved timeline.
            using (var release = new ManualResetEventSlim())
            {
                var entered = Signal(); var afterPause = Signal(); var calls = 0;
                var service = new GifRecordingService((_, _) =>
                {
                    var index = Interlocked.Increment(ref calls) - 1;
                    if (index == 1) { entered.TrySetResult(); Gate(release); }
                    return Frame(index);
                });
                var wall = Stopwatch.StartNew();
                await using var session = service.StartRecording(Bounds, Settings());
                session.ProgressChanged += (_, p) =>
                { if (IsFrame(p) && Volatile.Read(ref calls) >= 3) afterPause.TrySetResult(); };
                try
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); session.Pause();
                    var pausedAt = session.Elapsed;
                    await Task.Delay(300);
                    Require(session.Elapsed == pausedAt, "paused clock is frozen", new { pausedAt, elapsed = session.Elapsed });
                    release.Set(); session.Resume();
                    await afterPause.Task.WaitAsync(TimeSpan.FromSeconds(10)); session.Stop();
                    using var result = await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
                    Check("pause-in-flight", service, result, session.Elapsed, changing: true);
                    Require(wall.Elapsed - session.Elapsed >= TimeSpan.FromMilliseconds(290),
                        "paused wall time excluded", new { wall = wall.Elapsed, active = session.Elapsed });
                }
                finally { release.Set(); session.Stop(); }
            }

            using (var release = new ManualResetEventSlim())
            {
                var entered = Signal(); var calls = 0; Bitmap? discarded = null;
                var service = new GifRecordingService((_, _) =>
                {
                    if (Interlocked.Increment(ref calls) == 1) return Frame(0);
                    entered.TrySetResult(); Gate(release); return discarded = Frame(1);
                });
                await using var session = service.StartRecording(Bounds, Settings());
                try
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); session.Stop();
                    var stoppedAt = session.Elapsed;
                    await Task.Delay(300); release.Set();
                    using var result = await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));
                    Check("stop-in-flight", service, result, stoppedAt);
                    Require(session.Elapsed == stoppedAt, "draining does not extend stopped clock", Snapshot(result));
                    Require(discarded is not null && IsDisposed(discarded), "unpublished late bitmap disposed", Snapshot(result));
                }
                finally { release.Set(); session.Stop(); }
            }

            Require(Directory.GetDirectories(TemporarySession.Root).Order().SequenceEqual(protectedFolders),
                "owned temporary sessions removed; unrelated active session preserved", rows);
            Require(!Directory.EnumerateFiles(output, "*.timing", SearchOption.AllDirectories).Any(),
                "metadata staging files cleaned after success and rejection", rows);
            File.WriteAllText(Path.Combine(output, "result.txt"),
                $"PASS: GIF timing, deduplication, decoded delays/pixels, 10/20/30 FPS, slow/late capture, pause/stop and cleanup; {rows.Count} recorded cases (9 pipeline and 3 encoded long holds). Synthetic frames only.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(output, "failure.txt"), exception.ToString());
            throw;
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "metrics.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            AppPaths.Initialize(previous);
        }

        void Check(string name, GifRecordingService service, GifRecordingResult result,
            TimeSpan? elapsed = null, bool changing = false, bool assessThroughput = false, int expectedFps = 10)
        {
            var snapshot = new { name, durationMs = result.Duration.TotalMilliseconds,
                activeClockMs = elapsed?.TotalMilliseconds, result.Metrics, delays = result.FrameDelaysCentiseconds };
            rows.Add(snapshot);
            Console.WriteLine("GIF timeline " + JsonSerializer.Serialize(snapshot));
            ValidateMetrics(result, name, assessThroughput);
            Require(result.Fps == expectedFps, name + ": configured FPS preserved", snapshot);
            ValidateDuration(result, name);
            if (elapsed is not null)
                Require(Math.Abs(result.Duration.TotalMilliseconds - elapsed.Value.TotalMilliseconds) <= 20,
                    name + ": duration follows the actual active session clock (20 ms GIF minimum/rounding tolerance)", snapshot);
            var path = service.Save(result, new CaptureSettings
                { OutputDirectoryTemplate = output, FileNameTemplate = name }, "gif", 128);
            GifTimingMetadata.Write(path, result.FrameDelaysCentiseconds); // Idempotent timing/loop replacement.
            var encoded = File.ReadAllBytes(path);
            Require(System.Text.Encoding.ASCII.GetString(encoded).Split("NETSCAPE2.0").Length == 2,
                name + ": exactly one loop extension after metadata replacement", snapshot);
            using var stream = File.OpenRead(path);
            var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Require(decoder.Frames.Count == result.FrameCount, name + ": encoded frame count", snapshot);
            var decoded = decoder.Frames.Select(f => Convert.ToInt32(((BitmapMetadata)f.Metadata).GetQuery("/grctlext/Delay"))).ToArray();
            Require(decoded.SequenceEqual(result.FrameDelaysCentiseconds), name + ": decoded delays equal timeline exactly", new { snapshot, decoded });
            for (var i = 0; i < decoder.Frames.Count; i++)
            {
                var color = changing && i % 2 == 1 ? Color.Blue : Color.Red;
                RequirePixel(decoder.Frames[i], color, name + $": frame {i} pixel/order preserved", snapshot);
            }
        }
    }

    internal static void ValidateMetrics(GifRecordingResult result, string name, bool assessThroughput)
    {
        var m = result.Metrics;
        Require(m is not null, name + ": metrics present", Snapshot(result));
        Require(m!.RequestedFps == result.Fps, name + ": requested FPS preserved", Snapshot(result));
        Require(m.StoredFrames == result.FrameCount && m.StoredFrames > 0, name + ": stored count", Snapshot(result));
        Require(m.ProcessedFrames <= m.CapturedFrames, name + ": captured/processed accounting", Snapshot(result));
        Require(m.DuplicateFrames == m.ProcessedFrames - m.StoredFrames, name + ": duplicate accounting", Snapshot(result));
        Require(m.EffectiveCapturedFps > 0 && double.IsFinite(m.EffectiveCapturedFps), name + ": effective FPS finite/positive", Snapshot(result));
        Require(m.TemporaryBytes > 0 && m.TemporaryBytes <= GifRecordingService.MaximumTemporaryBytes &&
            m.StoredFrames <= GifRecordingService.MaximumStoredFrames && GifRecordingService.QueueCapacity == 2,
            name + ": spool/queue limits unchanged", Snapshot(result));
        if (!assessThroughput) return;
        // The old >=8 check was a throughput gate hidden inside a dedup assertion.
        // Keep that gate for runs with scheduling/capture/spool budget available;
        // report overload explicitly instead of blaming dedup for a busy runner.
        var budgetAvailable = m.DroppedFrames == 0 &&
            m.CaptureMilliseconds + m.ResizeMilliseconds + m.QueueWaitMilliseconds < m.ActiveDurationMilliseconds * 0.2;
        var minimum = Math.Max(1, (int)Math.Floor(m.ActiveDurationMilliseconds * m.RequestedFps / 1000d) - 2);
        Console.WriteLine($"GIF throughput {name}: budgetAvailable={budgetAvailable}; minimum={minimum}; " + Snapshot(result));
        if (budgetAvailable)
            Require(m.CapturedFrames >= minimum, name + ": throughput with available scheduler budget", Snapshot(result));
    }

    internal static void ValidateDuration(GifRecordingResult result, string name) =>
        Require(result.Metrics is { ActiveDurationMilliseconds: > 0 } m &&
            Math.Abs(result.Duration.TotalMilliseconds - m.ActiveDurationMilliseconds) <= 20,
            name + ": timeline matches stopped active duration", Snapshot(result));

    internal static string Snapshot(GifRecordingResult result) => JsonSerializer.Serialize(new
        { durationMs = result.Duration.TotalMilliseconds, result.Metrics, delays = result.FrameDelaysCentiseconds });
    private static void Require(bool value, string label, object details)
    { if (!value) throw new InvalidOperationException("GIF timeline: " + label + " | " + JsonSerializer.Serialize(details)); }
    private static void RequirePixel(BitmapSource frame, Color color, string label, object details)
    {
        var source = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[source.PixelWidth * source.PixelHeight * 4];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        Require(pixels[0] == color.B && pixels[1] == color.G && pixels[2] == color.R, label, details);
    }
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static bool IsFrame(RecordingProgress p) => p.Status.StartsWith("Gravando GIF ·", StringComparison.Ordinal);
    private static void Gate(ManualResetEventSlim gate)
    { if (!gate.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Synthetic GIF capture gate"); }
    private static Bitmap Frame(int index)
    {
        var frame = new Bitmap(Bounds.Width, Bounds.Height);
        using var graphics = Graphics.FromImage(frame);
        graphics.Clear(index % 2 == 0 ? Color.Red : Color.Blue);
        return frame;
    }
    private static bool IsDisposed(Bitmap bitmap)
    { try { _ = bitmap.Width; return false; } catch (ArgumentException) { return true; } }
}
