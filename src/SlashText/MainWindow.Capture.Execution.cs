using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;
using Forms = System.Windows.Forms;

namespace SlashText;

public partial class MainWindow
{
    private async Task RunScrollingCaptureAsync(bool invokedByShortcut)
    {
        if (!CanUseRecordingInterface || _settingsOperationInProgress) return;
        using var captureLease = _captureGate.TryEnter();
        if (captureLease is null)
        {
            SafeDiagnosticLog.Write("capture.ignored_busy");
            StatusText.Text = UiText.Get("Já existe uma captura ou gravação em andamento.");
            return;
        }

        var wasVisible = IsVisible;
        try
        {
            SafeDiagnosticLog.Write("capture.started", new Dictionary<string, object?>
            {
                ["action"] = CaptureShortcutAction.Scrolling.ToString(),
                ["invokedByShortcut"] = invokedByShortcut,
                ["windowWasVisible"] = wasVisible
            });

            if (invokedByShortcut)
            {
                await WaitForCaptureDelayAsync();
                if (_settings.Capture.ShouldHideSlashDesk(wasVisible))
                {
                    Hide();
                    await Task.Delay(180);
                }
            }
            else
            {
                if (wasVisible)
                {
                    Hide();
                }
                var selectionDelaySeconds = Math.Max(
                    3,
                    _settings.Capture.DelaySeconds);
                ShowTrayBalloon(
                    Math.Min(selectionDelaySeconds * 1000, 10000),
                    UiText.Format($"Captura longa em {selectionDelaySeconds}s"),
                    UiText.Get("Posicione o cursor sobre a página que será capturada."),
                    Forms.ToolTipIcon.Info);
                await Task.Delay(TimeSpan.FromSeconds(selectionDelaySeconds));
            }

            var target = _captureService.WindowUnderCursorTarget();
            if (target is null)
            {
                if (wasVisible)
                {
                    ShowFromTray();
                    ShowAppDialog(
                        UiText.Get("Posicione o cursor sobre a janela que será capturada."),
                        UiText.Get("Captura longa"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    ShowTrayBalloon(
                        2500,
                        UiText.Get("Captura longa não iniciada"),
                        UiText.Get("Não foi possível identificar a janela sob o cursor."),
                        Forms.ToolTipIcon.Warning);
                }
                return;
            }

            var result = await _captureService.CaptureScrollingAsync(
                target.WindowHandle,
                target.Bounds,
                _settings.Capture,
                openEditor: _settings.Capture.OpenEditorForMonitorAndWindow,
                owner: IsVisible ? this : null);
            if (wasVisible)
            {
                ShowFromTray();
            }
            if (result is not null)
            {
                StatusText.Text = string.IsNullOrWhiteSpace(result.FilePath)
                    ? UiText.Get("Captura longa copiada")
                    : UiText.Format($"Captura longa salva: {Path.GetFileName(result.FilePath)}");
                AcceptCompletedCapture(result);
                RefreshCaptureHistory();
                ShowTrayBalloon(
                    3500,
                    UiText.Get("Captura longa concluída"),
                    string.IsNullOrWhiteSpace(result.FilePath)
                        ? UiText.Get("Imagem copiada para o clipboard.")
                        : UiText.Get("Clique para abrir a imagem."),
                    Forms.ToolTipIcon.Info,
                    result.FilePath);
            }

            SafeDiagnosticLog.Write("capture.completed", new Dictionary<string, object?>
            {
                ["action"] = CaptureShortcutAction.Scrolling.ToString(),
                ["resultCreated"] = result is not null
            });
        }
        catch (Exception exception)
        {
            SafeDiagnosticLog.Write("capture.failed", new Dictionary<string, object?>
            {
                ["action"] = CaptureShortcutAction.Scrolling.ToString(),
                ["exceptionType"] = exception.GetType().Name
            });
            if (wasVisible)
            {
                ShowFromTray();
                ShowAppDialog(
                    exception.Message,
                    UiText.Get("Captura longa"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                ShowTrayBalloon(
                    2500,
                    UiText.Get("Não foi possível fazer a captura longa"),
                    exception.Message,
                    Forms.ToolTipIcon.Warning);
            }
        }
        finally
        {
            if (wasVisible && !IsVisible)
            {
                ShowFromTray();
            }
            _captureCommitInProgress = false;
            CaptureEditorCard.IsEnabled = true;
            _completedCaptureDocument = null;
            CaptureInlineEditor_OnStateChanged(this, EventArgs.Empty);
        }
    }

    private async Task RunCaptureAsync(
        CaptureShortcutAction action,
        bool invokedByShortcut)
    {
        if (!CanUseRecordingInterface || _settingsOperationInProgress) return;
        using var captureLease = _captureGate.TryEnter();
        if (captureLease is null)
        {
            StatusText.Text = UiText.Get("Já existe uma captura ou gravação em andamento.");
            return;
        }

        if (action == CaptureShortcutAction.Region && AppPaths.IsCapturePilot && !CanReplaceCaptureSession()) return;
        CaptureRegionSession? regionSession = null;

        var wasVisible = IsVisible;
        var shouldHide = _settings.Capture.ShouldHideSlashDesk(wasVisible);
        try
        {
            SafeDiagnosticLog.Write("capture.started", new Dictionary<string, object?>
            {
                ["action"] = action.ToString(),
                ["invokedByShortcut"] = invokedByShortcut,
                ["windowWasVisible"] = wasVisible
            });
            await WaitForCaptureDelayAsync();
            System.Drawing.Rectangle? bounds = null;
            System.Drawing.Bitmap? editedRegion = null;
            var requestedRegionOutput = CaptureEditorOutput.Default;
            var type = action switch
            {
                CaptureShortcutAction.ActiveMonitor => "monitor",
                CaptureShortcutAction.Region => "regiao",
                _ => "janela"
            };

            if (action == CaptureShortcutAction.ActiveMonitor)
            {
                bounds = _captureService.ActiveMonitorBounds();
            }
            else if (action == CaptureShortcutAction.Window)
            {
                bounds = _captureService.WindowUnderCursorBounds();
            }

            if (shouldHide)
            {
                Hide();
                await Task.Delay(180);
            }

            if (action == CaptureShortcutAction.Region)
            {
                editedRegion = _captureService.SelectAndEditRegion(
                    null,
                    _settings.Capture.IncludeCursor,
                    out requestedRegionOutput, out regionSession, _settings.Capture.Ocr);
            }
            CaptureRecord? result = null;
            if (editedRegion is not null)
            {
                using (editedRegion)
                {
                    result = await _captureService.ProcessEditedRegionAsync(
                        editedRegion,
                        type,
                        _settings.Capture,
                        requestedRegionOutput);
                }
            }
            else if (bounds is not null)
            {
                result = await _captureService.CaptureAndProcessAsync(
                    bounds.Value,
                    type,
                    _settings.Capture,
                    openEditor: _settings.Capture.OpenEditorForMonitorAndWindow,
                    owner: shouldHide ? null : this);
            }

            if (result is not null)
            {
                StatusText.Text = string.IsNullOrWhiteSpace(result.FilePath)
                    ? UiText.Format($"Captura de {type} copiada")
                    : UiText.Format($"Captura salva: {Path.GetFileName(result.FilePath)}");
                if (regionSession is not null) LoadCompletedRegionSession(regionSession);
                AcceptCompletedCapture(result);
                RefreshCaptureHistory();
                ShowTrayBalloon(
                    3500,
                    UiText.Get("Captura concluída"),
                    string.IsNullOrWhiteSpace(result.FilePath)
                        ? UiText.Get("Imagem copiada para o clipboard.")
                        : UiText.Get("Clique para abrir a imagem."),
                    Forms.ToolTipIcon.Info,
                    result.FilePath);
            }

            if (shouldHide)
            {
                ShowFromTray();
            }
            SafeDiagnosticLog.Write("capture.completed", new Dictionary<string, object?>
            {
                ["action"] = action.ToString(),
                ["resultCreated"] = result is not null
            });
        }
        catch (Exception exception)
        {
            SafeDiagnosticLog.Write("capture.failed", new Dictionary<string, object?>
            {
                ["action"] = action.ToString(),
                ["exceptionType"] = exception.GetType().Name
            });
            if (wasVisible)
            {
                ShowFromTray();
                ShowAppDialog(
                    exception.Message,
                    UiText.Get("Não foi possível capturar"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                ShowTrayBalloon(
                    2500,
                    UiText.Get("Não foi possível capturar"),
                    exception.Message,
                    Forms.ToolTipIcon.Warning);
            }
        }
        finally
        {
            regionSession?.Dispose();
            if (shouldHide && wasVisible && !IsVisible)
            {
                ShowFromTray();
            }
            _captureCommitInProgress = false;
            CaptureEditorCard.IsEnabled = true;
            _completedCaptureDocument = null;
            CaptureInlineEditor_OnStateChanged(this, EventArgs.Empty);
        }
    }

    private async Task WaitForCaptureDelayAsync()
    {
        var seconds = _settings.Capture.DelaySeconds;
        if (seconds <= 0)
        {
            return;
        }
        for (var remaining = seconds; remaining > 0; remaining--)
        {
            StatusText.Text = UiText.Format($"Captura em {remaining}s…");
            await Task.Delay(1000);
        }
    }

    private async void StartMp4Recording_OnClick(object sender, RoutedEventArgs e)
        => await RunRecordingOperationAsync("MP4", RunMp4RecordingAsync);

    private async Task RunMp4RecordingAsync(RecordingOperation operation)
    {
        try
        {
            var targetKind = SelectedVideoTargetKind;
            if (!await PrepareCapturePreferencesAsync(UiText.Get("Gravação"), operation.Token)) return;
            CheckRecordingOperation(operation);
            var capture = _settings.Capture;
            ConfigureCaptureShortcuts();
            if (RecordingPipelineForEvidence is { } pipeline) { await pipeline("MP4", operation); CheckRecordingOperation(operation); return; }
            if (CapturePreparationForEvidence is { } evidence) { evidence("mp4"); return; }
            operation.InterfaceHidden = true;
            Hide();
            await Task.Delay(250, operation.Token);
            CheckRecordingOperation(operation);
            var target = ResolveRecordingTarget(targetKind, operation.Token);
            if (target is null)
            {
                operation.Cancel();
                ShowFromTray();
                return;
            }

            if (IsVisible)
            {
                Hide();
            }
            await Task.Delay(180, operation.Token);
            CheckRecordingOperation(operation);
            var recorder = new ScreenRecordingService();
            _recordingService = recorder;
            recorder.RecordingFailed += (_, message) =>
            {
                if (!Dispatcher.HasShutdownStarted)
                    _ = Dispatcher.BeginInvoke(() => { if (CanUseRecordingInterface && ReferenceEquals(_recordingOperation, operation)) StatusText.Text = message; });
            };
            var previousAudio = capture.Recording.Audio.Copy();
            var control = new RecordingControlWindow(recorder, "MP4", capture.Recording);
            _recordingControl = control;
            using var cancellation = WatchPreparedRecordingControl(control, operation);
            control.EmergencyExitRequested += (_, _) => RequestRecordingEmergencyExit();
            control.Show();
            var shouldStart = await control.WaitForStartAsync();
            CheckRecordingOperation(operation);
            if (shouldStart)
            {
                shouldStart = await TrySaveCapturePreferencesCoreAsync(capture, previousAudio, ClosePreparedMp4Recording, operation.Token);
                CheckRecordingOperation(operation);
                shouldStart = shouldStart && control.IsVisible;
            }
            if (!shouldStart || _recordingControl is null)
            {
                if (_recordingControl is not null || _recordingService is not null) ClosePreparedMp4Recording();
                return;
            }
            CheckRecordingOperation(operation);
            operation.MarkRecording();
            control.BeginRecording();
            var completion = recorder.StartAsync(
                target,
                capture,
                capture.Recording);
            var path = await completion;
            CheckRecordingOperation(operation);
            operation.MarkFinalizing();
            var elapsed = recorder.Elapsed;
            var completedRecordingId = recorder.RecordingId.ToString("N");
            CleanupRecordingResources();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", completedRecordingId), ("media", "MP4"), ("result", "success"));
            await _captureService.AddMediaRecordAsync(new CaptureRecord
            {
                CreatedAt = DateTimeOffset.Now,
                Type = target.Type,
                MediaKind = "video",
                FilePath = path,
                Width = target.Bounds.Width,
                Height = target.Bounds.Height,
                DurationSeconds = elapsed.TotalSeconds
            });
            CheckRecordingOperation(operation);
            ShowFromTray();
            StatusText.Text = UiText.Format($"Vídeo salvo: {Path.GetFileName(path)}");
            RefreshCaptureHistory();
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested || !CanUseRecordingInterface) { throw; }
        catch (Exception exception)
        {
            if (!CanUseRecordingInterface) return;
            operation.Token.ThrowIfCancellationRequested();
            var failedRecordingId = _recordingService?.RecordingId.ToString("N") ?? "unknown";
            var recoverablePath = _recordingService?.RecoveryFilePath;
            await WaitForNativeRecordingReleaseAsync();
            CleanupRecordingResources();
            ShowFromTray();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", failedRecordingId), ("media", "MP4"), ("result", "failure"));
            ShowAppDialog(
                exception.Message +
                (exception is TimeoutException ? "\n\n" + recoverablePath :
                    UiText.Get("\n\nO SlashDesk usa H.264/Media Foundation. Em edições N/KN, instale o Media Feature Pack do Windows.")) +
                $"\nLogs: {AppPaths.LogsDirectory}",
                UiText.Get("Não foi possível gravar"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void StartGifRecording_OnClick(object sender, RoutedEventArgs e)
        => await RunRecordingOperationAsync("GIF", RunGifRecordingAsync);

    private async Task RunGifRecordingAsync(RecordingOperation operation)
    {
        try
        {
            if (!await PrepareCapturePreferencesAsync("GIF", operation.Token)) return;
            CheckRecordingOperation(operation);
            var capture = _settings.Capture;
            ConfigureCaptureShortcuts();
            if (RecordingPipelineForEvidence is { } pipeline) { await pipeline("GIF", operation); CheckRecordingOperation(operation); return; }
            if (CapturePreparationForEvidence is { } evidence) { evidence("gif"); return; }
            var target = _captureService.SelectRecordingRegion(this, UiText.Get("Selecione a região do GIF"), operation.Token);
            CheckRecordingOperation(operation);
            if (target is null) { operation.Cancel(); return; }
            operation.InterfaceHidden = true;
            Hide();
            await Task.Delay(180, operation.Token);
            CheckRecordingOperation(operation);
            operation.MarkRecording();
            var session = _gifRecordingService.StartRecording(
                target.Bounds,
                capture.Recording);
            _gifRecordingSession = session;
            _recordingControl = new RecordingControlWindow(session, "GIF");
            _recordingControl.Show();
            using var recording = await session.Completion;
            CheckRecordingOperation(operation);
            operation.MarkFinalizing();
            var completedRecordingId = session.RecordingId.ToString("N");
            CleanupRecordingResources();
            ShowFromTray();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", completedRecordingId), ("media", "GIF"), ("result", "captured"));
            if (recording.Metrics?.DroppedFrames > 0)
            {
                ShowAppDialog(
                    UiText.Format($"O pipeline descartou {recording.Metrics.DroppedFrames:N0} quadro(s) por sobrecarga. ") +
                    UiText.Get("O tempo foi preservado no GIF e o evento foi registrado nos logs."),
                    UiText.Get("GIF finalizado com sobrecarga"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            var preview = new GifPreviewWindow(recording) { Owner = this };
            using var previewCancellation = new RecordingWindowCancellation(preview, operation.Token);
            if (preview.ShowDialog() != true)
            {
                CheckRecordingOperation(operation);
                StatusText.Text = UiText.Get("GIF descartado");
                return;
            }
            CheckRecordingOperation(operation);
            var path = await _gifRecordingService.SaveAsync(
                recording,
                capture,
                target.Type,
                capture.Recording.GifQuality,
                operation.Token);
            CheckRecordingOperation(operation);
            await _captureService.AddMediaRecordAsync(new CaptureRecord
            {
                CreatedAt = DateTimeOffset.Now,
                Type = target.Type,
                MediaKind = "gif",
                FilePath = path,
                Width = recording.Width,
                Height = recording.Height,
                DurationSeconds = recording.Duration.TotalSeconds
            });
            CheckRecordingOperation(operation);
            StatusText.Text = UiText.Format($"GIF salvo: {Path.GetFileName(path)}");
            RefreshCaptureHistory();
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested || !CanUseRecordingInterface) { throw; }
        catch (OperationCanceledException)
        {
            var cancelledRecordingId = _gifRecordingSession?.RecordingId.ToString("N") ?? "unknown";
            CleanupRecordingResources();
            ShowFromTray();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", cancelledRecordingId), ("media", "GIF"), ("result", "cancelled"));
            StatusText.Text = UiText.Get("Gravação de GIF cancelada");
        }
        catch (Exception exception)
        {
            if (!CanUseRecordingInterface) return;
            operation.Token.ThrowIfCancellationRequested();
            var failedRecordingId = _gifRecordingSession?.RecordingId.ToString("N") ?? "unknown";
            CleanupRecordingResources();
            ShowFromTray();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", failedRecordingId), ("media", "GIF"), ("result", "failure"));
            ShowAppDialog(
                exception.Message,
                UiText.Get("Não foi possível criar o GIF"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private RecordingTarget? ResolveRecordingTarget(RecordingTargetKind kind, CancellationToken cancellationToken = default) => kind switch
    {
        RecordingTargetKind.Monitor => _captureService.ActiveMonitorTarget(),
        RecordingTargetKind.Region => _captureService.SelectRecordingRegion(this, UiText.Get("Selecione a região do vídeo"), cancellationToken),
        RecordingTargetKind.Window => _captureService.SelectRecordingWindow(this, UiText.Get("Clique na janela que deseja gravar"), cancellationToken),
        _ => throw new InvalidOperationException(UiText.Get("O destino do vídeo não é válido."))
    };

    private static int ParseSelectedInt(ComboBox box, int fallback) =>
        int.TryParse(SelectedTag(box, fallback.ToString()), out var value)
            ? value
            : fallback;
}
