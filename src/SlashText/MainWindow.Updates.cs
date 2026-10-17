using System.Diagnostics;
using System.Reflection;
using System.Windows;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;
using Forms = System.Windows.Forms;

namespace SlashText;

public partial class MainWindow
{
    private async void CheckUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        if (AppPaths.IsCapturePilot || _manualUpdateCheckInProgress) return;
        _manualUpdateCheckInProgress = true;
        CheckUpdatesButton.IsEnabled = false;
        SettingsCheckUpdatesButton.IsEnabled = false;
        try
        {
            var result = await CheckUpdatesAndRefreshAsync();
            if (result.UpdateAvailable && result.Release is not null)
            {
                _updateNotificationGate.Mark(result.Release.Version);
                await OfferUpdateAsync(result);
            }
            else
            {
                ShowSettingsDialog(
                    UiText.UpdateStatus(result.Message),
                    UiText.Get("Atualizações do SlashDesk"),
                    MessageBoxButton.OK,
                    result.Status == UpdateCheckStatus.Offline
                        ? MessageBoxImage.Information
                        : MessageBoxImage.None);
            }
        }
        catch (Exception exception)
        {
            ShowSettingsDialog(
                UiText.Format($"Não foi possível consultar o GitHub.\n\n{exception.Message}"),
                UiText.Get("Atualizações do SlashDesk"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _manualUpdateCheckInProgress = false;
            CheckUpdatesButton.IsEnabled = true;
            SettingsCheckUpdatesButton.IsEnabled = true;
        }
    }

    private void StartUpdateMonitor()
    {
        StopUpdateMonitor();
        if (_settingsStore.IsProtected || AppPaths.IsCapturePilot || !_settings.CheckUpdatesOnStartup || _servicesDisposed)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _updateMonitorCancellation = cancellation;
        _updateMonitorTask = MonitorUpdatesAsync(cancellation);
    }

    private void StopUpdateMonitor()
    {
        Interlocked.Exchange(ref _updateMonitorCancellation, null)?.Cancel();
        _updateMonitorTask = null;
    }

    private async Task MonitorUpdatesAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await CheckUpdatesSilentlyAsync(cancellation.Token);
            using var timer = new PeriodicTimer(UpdateMonitorInterval);
            while (await timer.WaitForNextTickAsync(cancellation.Token))
            {
                await CheckUpdatesSilentlyAsync(cancellation.Token);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Encerramento normal ao desativar a opção ou sair do aplicativo.
        }
        catch (Exception exception)
        {
            SafeDiagnosticLog.Write("update.monitor.failed", new Dictionary<string, object?>
            {
                ["exceptionType"] = exception.GetType().Name
            });
        }
        finally
        {
            Interlocked.CompareExchange(ref _updateMonitorCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private async Task CheckUpdatesSilentlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _updateService.CheckAsync(cancellationToken: cancellationToken);
            UpdateUpdateDisplay(await _updateService.LoadStateAsync(cancellationToken));
            if (result.UpdateAvailable &&
                result.Release is not null &&
                _updateNotificationGate.TryMark(result.Release.Version))
            {
                StatusText.Text = UiText.UpdateStatus(result.Message);
                ShowTrayBalloon(
                    2500,
                    UiText.Get("Atualização disponível"),
                    UiText.UpdateStatus(result.Message),
                    Forms.ToolTipIcon.Info);
                await OfferUpdateAsync(result);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Falhas de rede permanecem silenciosas no monitor automático.
        }
    }

    private async Task OfferUpdateAsync(UpdateCheckResult result)
    {
        if (AppPaths.IsCapturePilot) return;
        if (result.Release is null || Interlocked.Exchange(ref _updateOfferActive, 1) != 0)
        {
            return;
        }
        try
        {
            var dialog = new UpdateAvailableWindow(result) { Owner = this };
            dialog.ShowDialog();
            switch (dialog.Decision)
            {
                case UpdateDecision.UpdateNow:
                    if (AppPaths.IsPortable)
                    {
                        await DownloadAndApplyUpdateAsync(result.Release);
                    }
                    else
                    {
                        ShowAppDialog(
                            UiText.Get("Esta compilação instalada ainda não possui um instalador transacional. ") +
                            UiText.Get("A página oficial será aberta para uma atualização manual que preserva ") +
                            "%LocalAppData%\\SlashDesk.",
                            UiText.Get("Atualização da versão instalada"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        Process.Start(new ProcessStartInfo(result.Release.PageUrl) { UseShellExecute = true });
                    }
                    break;
                case UpdateDecision.RemindLater:
                    await _updateService.RemindLaterAsync(result.Release.Version);
                    break;
                case UpdateDecision.IgnoreVersion:
                    await _updateService.IgnoreVersionAsync(result.Release.Version);
                    break;
            }
            UpdateUpdateDisplay(await _updateService.LoadStateAsync());
        }
        finally
        {
            Interlocked.Exchange(ref _updateOfferActive, 0);
        }
    }

    private async Task DownloadAndApplyUpdateAsync(ReleaseInfo release)
    {
        using var lease = TryReserveCaptureCommand();
        if (lease is null) return;
        var progressWindow = new UpdateProgressWindow { Owner = this };
        _activeUpdateCancellation = progressWindow.Cancellation;
        progressWindow.Show();
        try
        {
            var prepared = await _portableUpdateService.PrepareAsync(
                release,
                progressWindow,
                progressWindow.Cancellation.Token);
            progressWindow.Report(new UpdateProgress(
                UiText.Get("Download validado. Encerrando para substituir somente SlashDesk.exe..."),
                0,
                null,
                IsApplying: true));
            PortableUpdateService.LaunchHelper(prepared);
            progressWindow.AllowClose();
            _exitRequested = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            progressWindow.AllowClose();
            StatusText.Text = UiText.Get("Atualização cancelada; nenhum arquivo do aplicativo foi alterado");
        }
        catch (Exception exception)
        {
            progressWindow.AllowClose();
            AppDiagnosticLog.WriteException("update.prepare.failed", exception);
            ShowAppDialog(
                UiText.Get("A atualização não foi aplicada. O executável atual e SlashDeskData foram ") +
                "preservados.\n\n" + exception.Message,
                UiText.Get("Falha na atualização"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _activeUpdateCancellation = null;
        }
    }

    private async Task RefreshUpdateStatusAsync() =>
        UpdateUpdateDisplay(await _updateService.LoadStateAsync());

    private void UpdateUpdateDisplay(UpdateState state)
    {
        LastUpdateCheckText.Text = state.LastCheckedUtc is null
            ? UiText.Get("Última verificação: ainda não verificado")
            : UiText.Format($"Última verificação: {state.LastCheckedUtc.Value.ToLocalTime():g}");
        LastUpdateResultText.Text = UiText.Format($"Resultado: {UiText.UpdateStatus(state.LastResult)}");
        SettingsLastUpdateCheckText.Text = LastUpdateCheckText.Text;
        SettingsLastUpdateResultText.Text = LastUpdateResultText.Text;
        _lastReleaseUrl = state.LastReleaseUrl;
        ReleaseNotesButton.Visibility = string.IsNullOrWhiteSpace(_lastReleaseUrl)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ReleaseNotes_OnClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastReleaseUrl))
        {
            OpenAboutLink(_lastReleaseUrl);
        }
    }

    private static string ProductVersion() =>
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetName().Version?.ToString(3) ?? "0.0.0";

    private void OpenGitHub_OnClick(object sender, RoutedEventArgs e) => OpenAboutLink(ProjectUrl);
}
