using SlashText.Models;
using SlashText.Services;
using System.Diagnostics;

internal static partial class SmokeSuite
{
    private static async Task RunRecordingChecksAsync(string root)
    {
        var fakeFactory = new FakeRecorderBackendFactory();
        using (var lifecycle = new ScreenRecordingService(fakeFactory, TimeSpan.FromSeconds(1)))
        {
            var lifecycleTask = lifecycle.StartAsync(
                new RecordingTarget(
                    RecordingTargetKind.Window,
                    new System.Drawing.Rectangle(0, 0, 640, 480),
                    new IntPtr(1)),
                new CaptureSettings
                {
                    OutputDirectoryTemplate = root,
                    FileNameTemplate = "lifecycle"
                },
                new RecordingSettings { VideoFps = 30, Audio = new() { CaptureComputer = true, CaptureMicrophone = true,
                    InputVolume = .4f, OutputVolume = .7f } });
            Require(fakeFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "MP4 inicia backend");
            await WaitUntilAsync(
                () => lifecycle.State == ScreenRecordingState.Recording,
                "estado Recording");
            await Task.Delay(100);
            Require(lifecycle.Elapsed >= TimeSpan.FromMilliseconds(60), "contador MP4 avança");
            Require(await lifecycle.SetAudioMutedAsync(true, false) && lifecycle.IsMicrophoneMuted &&
                    fakeFactory.Backend.InputVolume == 0 && fakeFactory.Backend.OutputVolume == .7f,
                "silenciar microfone mantém o áudio do PC");
            fakeFactory.Backend.RejectAudioChanges = true;
            Require(!await lifecycle.SetAudioMutedAsync(false, true) && lifecycle.IsMicrophoneMuted && !lifecycle.IsComputerMuted &&
                    lifecycle.State == ScreenRecordingState.Recording,
                "falha ao silenciar mantém estado real e não interrompe vídeo");
            fakeFactory.Backend.RejectAudioChanges = false;
            lifecycle.Pause();
            await WaitUntilAsync(
                () => lifecycle.State == ScreenRecordingState.Paused,
                "estado Paused");
            var pausedElapsed = lifecycle.Elapsed;
            Require(!await lifecycle.SetAudioMutedAsync(false, true) && lifecycle.IsMicrophoneMuted && !lifecycle.IsComputerMuted,
                "pausa preserva áudio e impede uma alteração que o encoder não aplica");
            await Task.Delay(80);
            Require(
                lifecycle.Elapsed - pausedElapsed < TimeSpan.FromMilliseconds(30),
                "contador MP4 não inclui tempo pausado");
            lifecycle.Resume();
            await WaitUntilAsync(
                () => lifecycle.State == ScreenRecordingState.Recording,
                "retorno ao estado Recording");
            Require(await lifecycle.SetAudioMutedAsync(false, true) && !lifecycle.IsMicrophoneMuted && lifecycle.IsComputerMuted &&
                    fakeFactory.Backend.InputVolume == .4f && fakeFactory.Backend.OutputVolume == 0,
                "retomar permite alterar áudio e restaura o volume escolhido");
            await Task.Delay(80);
            Require(lifecycle.Elapsed > pausedElapsed, "contador MP4 retoma do acumulado");
            lifecycle.Stop();
            var stoppedElapsed = lifecycle.Elapsed;
            var lifecyclePath = await lifecycleTask.WaitAsync(TimeSpan.FromSeconds(3));
            Require(File.Exists(lifecyclePath), "MP4 finaliza e publica arquivo");
            Require(lifecycle.State == ScreenRecordingState.Completed, "estado Completed");
            Require(!await lifecycle.SetAudioMutedAsync(false, false), "não muda áudio após finalizar");
            Require(fakeFactory.Backend.DisposeCalls == 1, "Recorder descartado uma única vez");
            Require(fakeFactory.Backend.MaximumConcurrentCalls == 1, "chamadas nativas serializadas");
            Require(
                lifecycle.Elapsed - stoppedElapsed < TimeSpan.FromMilliseconds(30),
                "contador MP4 para no pedido de finalização");
        }

        var duplicateFactory = new FakeRecorderBackendFactory { SendDuplicateCallbacks = true };
        using (var lifecycle = new ScreenRecordingService(duplicateFactory, TimeSpan.FromSeconds(1)))
        {
            var task = lifecycle.StartAsync(
                new RecordingTarget(
                    RecordingTargetKind.Window,
                    new System.Drawing.Rectangle(0, 0, 320, 240),
                    new IntPtr(1)),
                new CaptureSettings
                {
                    OutputDirectoryTemplate = root,
                    FileNameTemplate = "duplicate-callback"
                },
                new RecordingSettings());
            Require(duplicateFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para callback duplicado");
            lifecycle.Stop();
            lifecycle.Stop();
            await task.WaitAsync(TimeSpan.FromSeconds(3));
            Require(duplicateFactory.Backend.DisposeCalls == 1, "callback duplicado não duplica descarte");
            Require(duplicateFactory.Backend.StopCalls == 1, "dois cliques em finalizar chamam Stop uma vez");
        }

        var failureFactory = new FakeRecorderBackendFactory { FailOnStop = true };
        using (var lifecycle = new ScreenRecordingService(failureFactory, TimeSpan.FromSeconds(1)))
        {
            var task = lifecycle.StartAsync(
                new RecordingTarget(RecordingTargetKind.Window,
                    new System.Drawing.Rectangle(0, 0, 320, 240), new IntPtr(1)),
                new CaptureSettings { OutputDirectoryTemplate = root, FileNameTemplate = "failed-callback" },
                new RecordingSettings());
            Require(failureFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para callback de falha");
            lifecycle.Stop();
            await RequireThrowsAsync<InvalidOperationException>(() => task, "callback de falha conclui task");
            Require(lifecycle.State == ScreenRecordingState.Failed, "callback de falha restaura estado terminal");
            Require(failureFactory.Backend.DisposeCalls == 1, "falha descarta Recorder uma vez");
        }

        var delayedFactory = new FakeRecorderBackendFactory { CallbackDelayMs = 120 };
        using (var lifecycle = new ScreenRecordingService(delayedFactory, TimeSpan.FromSeconds(1)))
        {
            var task = lifecycle.StartAsync(
                new RecordingTarget(RecordingTargetKind.Window,
                    new System.Drawing.Rectangle(0, 0, 320, 240), new IntPtr(1)),
                new CaptureSettings { OutputDirectoryTemplate = root, FileNameTemplate = "delayed-callback" },
                new RecordingSettings());
            Require(delayedFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para callback atrasado");
            lifecycle.Stop();
            await task.WaitAsync(TimeSpan.FromSeconds(3));
            Require(delayedFactory.Backend.DisposeCalls == 1, "callback atrasado finaliza uma vez");
        }

        var blockingFactory = new FakeRecorderBackendFactory { BlockStop = true };
        using (var lifecycle = new ScreenRecordingService(blockingFactory, TimeSpan.FromSeconds(2)))
        {
            var task = lifecycle.StartAsync(
                new RecordingTarget(RecordingTargetKind.Window,
                    new System.Drawing.Rectangle(0, 0, 320, 240), new IntPtr(1)),
                new CaptureSettings { OutputDirectoryTemplate = root, FileNameTemplate = "closing-finalization" },
                new RecordingSettings());
            Require(blockingFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para Stop bloqueante");
            var uiClock = Stopwatch.StartNew();
            lifecycle.Stop();
            lifecycle.Dispose();
            Require(uiClock.Elapsed < TimeSpan.FromMilliseconds(100), "Stop e Dispose não bloqueiam a UI");
            Require(blockingFactory.Backend.StopEntered.Wait(TimeSpan.FromSeconds(2)), "Stop nativo entrou");
            blockingFactory.Backend.ReleaseStop();
            await task.WaitAsync(TimeSpan.FromSeconds(3));
            Require(blockingFactory.Backend.DisposeCalls == 1, "fechamento durante finalização descarta uma vez");
        }

        var timeoutFactory = new FakeRecorderBackendFactory { CompleteOnStop = false };
        using (var lifecycle = new ScreenRecordingService(timeoutFactory, TimeSpan.FromMilliseconds(80)))
        {
            var task = lifecycle.StartAsync(
                new RecordingTarget(
                    RecordingTargetKind.Window,
                    new System.Drawing.Rectangle(0, 0, 320, 240),
                    new IntPtr(1)),
                new CaptureSettings
                {
                    OutputDirectoryTemplate = root,
                    FileNameTemplate = "late-callback"
                },
                new RecordingSettings());
            Require(timeoutFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)), "backend para timeout");
            lifecycle.Stop();
            await WaitUntilAsync(() => lifecycle.IsStopUnconfirmed, "timeout nativo visível");
            Require(!task.IsCompleted && lifecycle.IsRecording, "timeout mantém vínculo com o gravador");
            Require(timeoutFactory.Backend.DisposeCalls == 0, "timeout não descarta Recorder durante callback nativo");
            timeoutFactory.Backend.CompleteLater();
            await RequireThrowsAsync<TimeoutException>(() => task, "callback tardio libera antes de completar");
            await WaitUntilAsync(
                () => lifecycle.State == ScreenRecordingState.Failed && timeoutFactory.Backend.DisposeCalls >= 1,
                "callback tardio conclui limpeza");
            Require(timeoutFactory.Backend.DisposeCalls == 1, "callback tardio executa descarte único");
        }
    }

    private static async Task RunUnconfirmedStopChecksAsync(string root)
    {
        var noCallbackFactory = new FakeRecorderBackendFactory { CompleteOnStop = false };
        using (var lifecycle = new ScreenRecordingService(
                   noCallbackFactory,
                   TimeSpan.FromMilliseconds(60)))
        {
            var task = lifecycle.StartAsync(
                new RecordingTarget(RecordingTargetKind.Window,
                    new System.Drawing.Rectangle(0, 0, 320, 240), new IntPtr(1)),
                new CaptureSettings { OutputDirectoryTemplate = root, FileNameTemplate = "no-callback" },
                new RecordingSettings());
            Require(noCallbackFactory.Backend.RecordCalled.Wait(TimeSpan.FromSeconds(2)),
                "backend para timeout sem callback");
            lifecycle.Stop();
            await WaitUntilAsync(() => lifecycle.IsStopUnconfirmed, "timeout sem callback visível");
            var disposeClock = Stopwatch.StartNew();
            lifecycle.Dispose();
            Require(disposeClock.Elapsed < TimeSpan.FromMilliseconds(100),
                "Dispose após timeout não bloqueia UI");
            Require(noCallbackFactory.Backend.DisposeCalls == 0,
                "timeout sem callback não concorre Dispose com código nativo");
            Require(lifecycle.IsStopUnconfirmed && lifecycle.IsRecording && !task.IsCompleted,
                "timeout sem callback mantém estado não confirmado");
            noCallbackFactory.Backend.CompleteLater();
            await RequireThrowsAsync<TimeoutException>(() => task, "callback posterior libera recursos");
        }
    }
}
