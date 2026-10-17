using SlashText.Models;
using SlashText.Services;

internal static partial class SmokeSuite
{
    private static async Task RunGifPipelineChecksAsync(string root)
    {
        var callerThread = 0;
        var gifCaptureThreads = new System.Collections.Concurrent.ConcurrentBag<int>();
        var pipeline = new GifRecordingService((_, _) =>
        {
            gifCaptureThreads.Add(Environment.CurrentManagedThreadId);
            var bitmap = new System.Drawing.Bitmap(16, 12);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            graphics.Clear(System.Drawing.Color.CornflowerBlue);
            return bitmap;
        });
        GifRecordingResult? pipelineResult = null;
        Exception? pipelineFailure = null;
        var caller = new Thread(() =>
        {
            callerThread = Environment.CurrentManagedThreadId;
            try
            {
                pipelineResult = pipeline.CaptureAsync(
                    new System.Drawing.Rectangle(0, 0, 16, 12),
                    new RecordingSettings
                    {
                        GifFps = 10,
                        GifDurationSeconds = 1,
                        GifWidth = 240,
                        GifQuality = 128
                    }).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                pipelineFailure = exception;
            }
        });
        caller.Start();
        caller.Join();
        if (pipelineFailure is not null)
        {
            throw new InvalidOperationException("Pipeline GIF falhou.", pipelineFailure);
        }

        using (pipelineResult ?? throw new InvalidOperationException("Pipeline GIF sem resultado."))
        {
            GifTimelineChecks.ValidateMetrics(pipelineResult, "bounded/off-UI", assessThroughput: true);
            GifTimelineChecks.ValidateDuration(pipelineResult, "bounded/off-UI");
            Require(pipelineResult.Metrics?.RequestedFps == 10 && pipelineResult.Fps == 10,
                "preset GIF de 10 FPS preservado: " + GifTimelineChecks.Snapshot(pipelineResult));
            Require(pipelineResult.FrameCount == 1,
                "deduplicação GIF armazena uma imagem: " + GifTimelineChecks.Snapshot(pipelineResult));
            Require(
                gifCaptureThreads.All(thread => thread != callerThread),
                "captura e redimensionamento GIF fora da UI thread");
        }

        foreach (var fpsPreset in RecordingPresetCatalog.GifFps)
        {
            var firstGifFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var session = pipeline.StartRecording(
                new System.Drawing.Rectangle(0, 0, 16, 12),
                new RecordingSettings { GifFps = fpsPreset.Value, GifQuality = 128 });
            IRecordingController counter = session;
            Require(counter.Elapsed < TimeSpan.FromMilliseconds(80),
                $"contador GIF inicia em zero a {fpsPreset.Value} FPS");
            session.ProgressChanged += (_, progress) =>
            {
                if (progress.Status.StartsWith("Gravando GIF ·", StringComparison.Ordinal)) firstGifFrame.TrySetResult();
            };
            // Exercise clock controls after the synthetic pipeline is ready. A busy
            // runner may not schedule its first frame within the old 280 ms sleeps;
            // stopping then correctly produces no GIF, rather than a clock failure.
            try { await firstGifFrame.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException exception)
            { throw new InvalidOperationException($"GIF fixture did not persist its first frame at {fpsPreset.Value} FPS before clock checks.", exception); }
            await Task.Delay(120);
            Require(counter.Elapsed >= TimeSpan.FromMilliseconds(70),
                $"contador GIF avança a {fpsPreset.Value} FPS");
            counter.Pause();
            var gifPausedAt = counter.Elapsed;
            await Task.Delay(80);
            Require(counter.Elapsed - gifPausedAt < TimeSpan.FromMilliseconds(30),
                $"contador GIF pausa a {fpsPreset.Value} FPS");
            counter.Resume();
            await Task.Delay(80);
            Require(counter.Elapsed > gifPausedAt,
                $"contador GIF retoma a {fpsPreset.Value} FPS");
            counter.Stop();
            var gifStoppedAt = counter.Elapsed;
            using var presetResult = await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
            Require(presetResult.Fps == fpsPreset.Value,
                $"pipeline mantÃ©m preset de {fpsPreset.Value} FPS");
            Require(counter.Elapsed - gifStoppedAt < TimeSpan.FromMilliseconds(30),
                $"contador GIF para ao finalizar a {fpsPreset.Value} FPS");
            Require(GifRecordingService.QueueCapacity == 2,
                $"fila GIF limitada no preset de {fpsPreset.Value} FPS");
        }
    }
}
