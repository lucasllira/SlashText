using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SlashText.Design;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;
using Button = System.Windows.Controls.Button;

namespace SlashText;

public partial class MainWindow
{
    private void CaptureShortcuts_OnTriggered(
        object? sender,
        CaptureShortcutEventArgs e)
    {
        if (_restoreRecoveryBlocked || _servicesDisposed) return;
        if (e.Action == CaptureShortcutAction.Scrolling)
        {
            _ = RunScrollingCaptureAsync(invokedByShortcut: true);
            return;
        }

        _ = RunCaptureAsync(e.Action, invokedByShortcut: true);
    }

    private void CaptureActiveMonitor_OnClick(object sender, RoutedEventArgs e) =>
        _ = RunCaptureAsync(CaptureShortcutAction.ActiveMonitor, invokedByShortcut: false);

    private void CaptureRegion_OnClick(object sender, RoutedEventArgs e) =>
        _ = RunCaptureAsync(CaptureShortcutAction.Region, invokedByShortcut: false);

    private void CaptureWindow_OnClick(object sender, RoutedEventArgs e) =>
        _ = RunCaptureAsync(CaptureShortcutAction.Window, invokedByShortcut: false);

    private void CaptureScrolling_OnClick(object sender, RoutedEventArgs e) =>
        _ = RunScrollingCaptureAsync(invokedByShortcut: false);

    private void CaptureLauncherMode_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag } ||
            !Enum.TryParse(tag, ignoreCase: true, out CaptureLauncherMode mode))
        {
            return;
        }

        if (_captureMediaMode == CaptureMediaMode.Video && mode == CaptureLauncherMode.Scrolling) return;
        _captureLauncherMode = mode;
        if (mode == CaptureLauncherMode.Gif)
        {
            _captureMediaMode = CaptureMediaMode.Gif;
            CaptureGifMediaButton.IsChecked = true;
        }
        else if (_captureMediaMode == CaptureMediaMode.Gif)
        {
            _captureMediaMode = CaptureMediaMode.Image;
            CaptureImageMediaButton.IsChecked = true;
        }
        UpdateCaptureLauncherSummary();
    }

    private void CaptureMediaMode_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { Tag: string tag } ||
            !Enum.TryParse(tag, ignoreCase: true, out CaptureMediaMode mode))
        {
            return;
        }

        _captureMediaMode = mode;
        if (mode == CaptureMediaMode.Video && _captureLauncherMode == CaptureLauncherMode.Scrolling)
        {
            _captureLauncherMode = CaptureLauncherMode.Region;
            CaptureRegionModeButton.IsChecked = true;
        }
        if (mode == CaptureMediaMode.Gif)
        {
            _captureLauncherMode = CaptureLauncherMode.Gif;
            CaptureGifModeButton.IsChecked = true;
        }
        else if (_captureLauncherMode == CaptureLauncherMode.Gif)
        {
            _captureLauncherMode = CaptureLauncherMode.Region;
            CaptureRegionModeButton.IsChecked = true;
        }
        UpdateCaptureLauncherSummary();
    }

    private async void StartSelectedCapture_OnClick(object sender, RoutedEventArgs e)
    {
        // Video/GIF own their preparation, including the preference save.
        switch (_captureMediaMode)
        {
            case CaptureMediaMode.Video:
                StartMp4Recording_OnClick(sender, e);
                return;
            case CaptureMediaMode.Gif:
                StartGifRecording_OnClick(sender, e);
                return;
        }

        using var lease = TryReserveCaptureCommand();
        if (lease is null) return;
        if (!await PrepareCapturePreferencesAsync(UiText.Get("Regra de captura"))) return;
        if (!CanUseRecordingInterface) return;
        ConfigureCaptureShortcuts();
        if (CapturePreparationForEvidence is { } evidence) { evidence("image"); return; }
        // Existing image handlers reserve synchronously before their first await.
        // Transfer on the UI thread without an await or modal call between leases.
        lease.Dispose();
        switch (_captureLauncherMode)
        {
            case CaptureLauncherMode.ActiveMonitor:
                CaptureActiveMonitor_OnClick(sender, e);
                break;
            case CaptureLauncherMode.Window:
                CaptureWindow_OnClick(sender, e);
                break;
            case CaptureLauncherMode.Scrolling:
                CaptureScrolling_OnClick(sender, e);
                break;
            default:
                CaptureRegion_OnClick(sender, e);
                break;
        }
    }

    private void UpdateCaptureLauncherSummary()
    {
        if (CaptureEditorCard is not null)
        {
            var image = _captureMediaMode == CaptureMediaMode.Image;
            CaptureEditorCard.Visibility = image ? Visibility.Visible : Visibility.Collapsed;
            CaptureMediaPreviewPanel.Visibility = image ? Visibility.Collapsed : Visibility.Visible;
        }
        CaptureScrollingModeButton.IsEnabled = _captureMediaMode != CaptureMediaMode.Video;
        CaptureScrollingModeButton.Opacity = CaptureScrollingModeButton.IsEnabled ? 1 : 0.45;
        if (_captureMediaMode == CaptureMediaMode.Video) SyncVideoTargetControls();
        UpdateCaptureSelectionSummary();
        CaptureRecordingCard.Visibility = Visibility.Collapsed;
        CaptureRecordingCompactCard.Visibility = _captureMediaMode == CaptureMediaMode.Image
            ? Visibility.Collapsed
            : Visibility.Visible;
        CaptureRecordingTopGapRow.Height = new GridLength(20);
        CaptureRecordingBottomGapRow.Height = new GridLength(
            _captureMediaMode == CaptureMediaMode.Image ? 0 : 20);
        CaptureVideoConfigurationPanel.Visibility = _captureMediaMode == CaptureMediaMode.Video
            ? Visibility.Visible
            : Visibility.Collapsed;
        CaptureGifConfigurationPanel.Visibility = _captureMediaMode == CaptureMediaMode.Gif
            ? Visibility.Visible
            : Visibility.Collapsed;
        PopulateCompactRecordingControls();
        if (CaptureRecordingCompactCard.IsVisible && CaptureRecordingCompactCard.IsLoaded)
        {
            LabMotion.PlayEntrance(CaptureRecordingCompactCard);
        }
    }

    // The visible launcher mode is the source of truth. Hidden legacy controls
    // are mirrors; they must never silently override the destination on New.
    internal RecordingTargetKind SelectedVideoTargetKind => _captureLauncherMode switch
    {
        CaptureLauncherMode.ActiveMonitor => RecordingTargetKind.Monitor,
        CaptureLauncherMode.Region => RecordingTargetKind.Region,
        CaptureLauncherMode.Window => RecordingTargetKind.Window,
        _ => throw new InvalidOperationException(UiText.Get("O destino do vídeo não é válido."))
    };

    private void SyncVideoTargetControls()
    {
        var kind = SelectedVideoTargetKind;
        var updating = _updatingCaptureRecordingCompact;
        _updatingCaptureRecordingCompact = true;
        try
        {
            SelectComboByTag(RecordingTargetBox, kind.ToString());
            SelectComboByTag(CaptureCompactTargetBox, kind.ToString());
            CaptureMonitorModeButton.IsChecked = kind == RecordingTargetKind.Monitor;
            CaptureRegionModeButton.IsChecked = kind == RecordingTargetKind.Region;
            CaptureWindowModeButton.IsChecked = kind == RecordingTargetKind.Window;
        }
        finally { _updatingCaptureRecordingCompact = updating; }
    }

    private void UpdateCaptureSelectionSummary()
    {
        var area = _captureLauncherMode switch
        {
            CaptureLauncherMode.ActiveMonitor => UiText.Get("Monitor"),
            CaptureLauncherMode.Window => UiText.Get("Janela"),
            CaptureLauncherMode.Scrolling => UiText.Get("Captura longa"),
            _ => UiText.Get("Região")
        };
        var selection = _captureMediaMode switch
        {
            CaptureMediaMode.Video => UiText.Get("Vídeo MP4") + " · " + area,
            CaptureMediaMode.Gif => UiText.Get("GIF · região"),
            _ => area
        };
        CaptureNewButtonText.Text = UiText.Get("Novo");
        CaptureSelectionSummaryText.Text = UiText.Format($"{selection} · pronta");
    }

    private ScreenHelpHighlighter? _captureHelpHighlight;

    private void OpenCaptureHelp_OnClick(object sender, RoutedEventArgs e)
    {
        _captureHelpHighlight?.Remove(); _captureHelpHighlight = null;
        var guide = new ScreenHelpWindow(CaptureHelpContent.Create(_settings.Capture)) { Owner = this };
        LabMotion.SetReduced(guide, LabMotion.GetReduced(this));
        if (sender is Button button && button != CaptureHelpButton) guide.OpenTopic("zoom");
        guide.EnableBackdrop();
        ShowCaptureDialog(guide);
        if (guide.RequestedTarget is not { } name) return;
        if (FindName(name) is not FrameworkElement target) return;
        // Do not switch media, select tools or discard a session just to reveal help.
        if (!target.IsVisible)
        {
            StatusText.Text = UiText.Get("Este recurso aparece no painel de imagem. Sua sessão foi preservada.");
            target = _captureEditorExpanded ? CaptureExpandEditorButton : CaptureImageMediaButton;
        }
        else StatusText.Text = UiText.Get("Recurso destacado: ") + (System.Windows.Automation.AutomationProperties.GetName(target) is { Length: > 0 } label ? label : guide.Title);
        target.BringIntoView();
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!target.IsVisible) return;
            target.Focus(); _captureHelpHighlight = ScreenHelpHighlighter.Show(target);
        }), DispatcherPriority.Loaded);
    }

    private async void FocusCaptureRule_OnClick(object sender, RoutedEventArgs e)
    {
        using var lease = TryReserveCaptureCommand();
        if (lease is null) return;
        if (_settingsOperationInProgress || _servicesDisposed || _restoreRecoveryBlocked) return;
        var previous = _settings.Capture;
        if (!TryReadCaptureSettings(out var currentError))
        {
            ShowAppDialog(currentError, UiText.Get("Configurações de captura"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dialog = new CaptureRuleDialog(_settings.Capture)
        {
            Owner = this
        };
        LabMotion.SetReduced(dialog, LabMotion.GetReduced(this));
        dialog.ShortcutStatus = CaptureShortcutStatusText.Text;
        var accepted = ShowCaptureDialog(dialog);
        if (!accepted)
        {
            _settings.Capture = previous;
            return;
        }

        _settings.Capture = dialog.Result;
        if (!await TrySaveCapturePreferencesAsync(previous)) return;
        LoadCaptureSettings();
        ConfigureCaptureShortcuts();
        ShowCapturePreferencesSavedStatus();
    }

    private bool ShowCaptureDialog(Window dialog)
    {
        var previousOpacity = ShellRoot.Opacity;
        var previousEffect = ShellRoot.Effect;
        try
        {
            ShellRoot.Opacity = 0.58;
            ShellRoot.Effect = new System.Windows.Media.Effects.BlurEffect { Radius = 4 };
            return dialog.ShowDialog() == true;
        }
        finally
        {
            ShellRoot.Effect = previousEffect;
            ShellRoot.Opacity = previousOpacity;
        }
    }
}
