using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SlashText.Design;
using SlashText.Services;
using Button = System.Windows.Controls.Button;

namespace SlashText;

public partial class MainWindow
{
    private void ShortcutsTab_OnClick(object sender, RoutedEventArgs e)
    {
        ShowView(ShortcutsView, ShortcutsTabButton);
    }

    private void QuickAccentTab_OnClick(object sender, RoutedEventArgs e) =>
        ShowView(QuickAccentView, QuickAccentTabButton);

    private void CaptureTab_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshCaptureHistory();
        ShowView(CaptureView, CaptureTabButton);
    }

    private void StatisticsTab_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshStatistics();
        ShowView(StatisticsView, StatisticsTabButton);
    }

    private void SettingsTab_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshBackupSummary();
        ShowView(SettingsView, SettingsTabButton);
    }

    private void AboutTab_OnClick(object sender, RoutedEventArgs e) =>
        ShowView(AboutView, AboutTabButton);

    private void ShowView(UIElement view, Button selectedButton)
    {
        var views = new UIElement[]
        {
            ShortcutsView, CaptureView, QuickAccentView, StatisticsView, SettingsView, AboutView
        };
        foreach (var item in views)
        {
            item.Visibility = ReferenceEquals(item, view)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        var buttons = new[]
        {
            ShortcutsTabButton, CaptureTabButton, QuickAccentTabButton, StatisticsTabButton,
            SettingsTabButton, AboutTabButton
        };
        foreach (var button in buttons)
        {
            var selected = ReferenceEquals(button, selectedButton);
            button.Tag = selected ? "Selected" : null;
        }

        (ShellPageTitle.Text, ShellPageDescription.Text) = ReferenceEquals(view, ShortcutsView)
            ? (UiText.Get("Atalhos"), UiText.Get("Crie, organize e edite seus textos prontos."))
            : ReferenceEquals(view, QuickAccentView)
                ? (UiText.Get("Acento Rápido"), UiText.Get("Digite caracteres especiais sem interromper seu fluxo."))
                : ReferenceEquals(view, CaptureView)
                    ? (UiText.Get("Captura"), UiText.Get("Capture, edite e grave sua tela localmente."))
                    : ReferenceEquals(view, StatisticsView)
                        ? (UiText.Get("Estatísticas"), UiText.Get("Acompanhe o uso real dos recursos do SlashDesk."))
                        : ReferenceEquals(view, SettingsView)
                            ? (UiText.Get("Configurações"), UiText.Get("Personalize o comportamento do SlashDesk."))
                            : (UiText.Get("Sobre"), UiText.Get("Versão, privacidade, suporte e atualizações."));
        ShortcutHeaderActions.Visibility = ReferenceEquals(view, ShortcutsView)
            ? Visibility.Visible
            : Visibility.Collapsed;
        ShellCollectionStatusText.Visibility = ReferenceEquals(view, ShortcutsView)
            ? Visibility.Visible
            : Visibility.Collapsed;

        var monitorReady = _keyboardHook.IsRunning;
        MonitorStatusText.Text = monitorReady
            ? ReferenceEquals(view, QuickAccentView) ? UiText.Get("Monitor de atalhos ativo") : UiText.Get("Monitoramento ativo")
            : UiText.Get("Monitor de atalhos indisponível");
        ShellStatusText.Text = MonitorStatusText.Text;
        MonitorStatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, monitorReady ? "SuccessBrush" : "MutedBrush");
        ShellMonitorStatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, monitorReady ? "SuccessBrush" : "MutedBrush");

        if (ReferenceEquals(view, ShortcutsView) && ShortcutsView.IsLoaded) LabMotion.PlayEntrance(ShortcutsView);
        if (ReferenceEquals(view, AboutView))
        {
            RefreshAboutPresentation(); UpdateAboutLayout(ActualWidth);
            if (AboutView.IsLoaded) LabMotion.PlayEntrance(AboutView);
        }
        RefreshStorageProtection();
        if (ReferenceEquals(view, SettingsView))
        {
            RefreshSettingsPresentation(); UpdateSettingsLayout(ActualWidth);
            if (SettingsView.IsLoaded) LabMotion.PlayEntrance(SettingsView);
        }
        if (ReferenceEquals(view, StatisticsView))
        {
            UpdateStatisticsLayout(ActualWidth);
            if (StatisticsView.IsLoaded) LabMotion.PlayEntrance(StatisticsView);
        }
        if (ReferenceEquals(view, QuickAccentView))
        {
            UpdateQuickAccentLayout(ActualWidth);
            UpdateQuickAccentPreviewSelection();
            if (QuickAccentView.IsLoaded) LabMotion.PlayEntrance(QuickAccentView);
        }
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximizedState();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeWindow_OnClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeWindow_OnClick(object sender, RoutedEventArgs e) =>
        ToggleMaximizedState();

    private void CloseWindow_OnClick(object sender, RoutedEventArgs e) =>
        Close();

    private void ToggleMaximizedState() =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width);
        UpdateQuickAccentLayout(e.NewSize.Width);
        UpdateStatisticsLayout(e.NewSize.Width);
        UpdateSettingsLayout(e.NewSize.Width);
        UpdateAboutLayout(e.NewSize.Width);
        if (_captureEditorExpanded) UpdateCaptureEditorViewport();
    }

    private void UpdateResponsiveLayout(double width)
    {
        if (ShortcutSidebarPanel is null)
        {
            return;
        }

        var band = width <= 1050 ? 0 : width < 1180 ? 1 : 2;
        var bandChanged = band != _shortcutResponsiveBand;
        _shortcutResponsiveBand = band;

        var horizontalMargin = band < 2 ? 20d : 32d;
        WorkspaceHost.Margin = new Thickness(horizontalMargin, band == 0 ? 12 : 20,
            horizontalMargin, band == 0 ? 12 : 20);
        ShortcutSecondaryRow.Height = new GridLength(0);
        ShortcutLeftDividerColumn.Width = new GridLength(16);
        ShortcutRightDividerColumn.Width = new GridLength(16);

        Grid.SetRow(ShortcutSidebarPanel, 0);
        Grid.SetColumn(ShortcutSidebarPanel, 0);
        Grid.SetColumnSpan(ShortcutSidebarPanel, 1);
        Grid.SetRow(ShortcutEditorPanel, 0);
        Grid.SetColumn(ShortcutEditorPanel, _shortcutEditorExpanded ? 0 : 2);
        Grid.SetColumnSpan(ShortcutEditorPanel, _shortcutEditorExpanded ? 5 : 1);
        Grid.SetRow(ShortcutVariablesPanel, 0);
        Grid.SetColumn(ShortcutVariablesPanel, 4);
        Grid.SetColumnSpan(ShortcutVariablesPanel, 1);

        ShortcutLeftDivider.Visibility = _shortcutEditorExpanded ? Visibility.Collapsed : Visibility.Visible;
        ShortcutRightDivider.Visibility = !_shortcutEditorExpanded && _shortcutVariablesVisible ? Visibility.Visible : Visibility.Collapsed;
        if (_shortcutEditorExpanded) UpdateShortcutEditorHeight();
        NormalizeShortcutColumns(width, bandChanged);
    }

    private void NormalizeShortcutColumns(double windowWidth, bool restoreDefaults = false)
    {
        var band = _shortcutResponsiveBand < 0
            ? windowWidth <= 1050 ? 0 : windowWidth < 1180 ? 1 : 2
            : _shortcutResponsiveBand;
        var margin = band < 2 ? 20d : 32d;
        var editorMinimum = band == 0 ? 420d : band == 1 ? 440d : 520d;
        var available = Math.Max(0, windowWidth - (margin * 2));
        var rightMinimum = _shortcutVariablesVisible ? ShortcutRightMinimum : 0d;
        var sideSpace = Math.Max(
            ShortcutLeftMinimum + rightMinimum,
            available - (_shortcutVariablesVisible ? ShortcutDividerSpace : 16d) - editorMinimum);

        ShortcutEditorColumn.MinWidth = editorMinimum;
        ShortcutLeftColumn.MinWidth = ShortcutLeftMinimum;
        ShortcutRightColumn.MinWidth = rightMinimum;
        ShortcutLeftColumn.MaxWidth = Math.Max(
            ShortcutLeftMinimum,
            Math.Min(ShortcutLeftMaximum, sideSpace - rightMinimum));
        ShortcutRightColumn.MaxWidth = Math.Max(
            ShortcutRightMinimum,
            Math.Min(ShortcutRightMaximum, sideSpace - ShortcutLeftMinimum));

        var defaultLeft = band == 0 ? 240d : band == 1 ? 260d : 300d;
        var defaultRight = band == 0 ? 240d : 280d;
        var left = restoreDefaults ? defaultLeft : ShortcutLeftColumn.ActualWidth;
        var right = restoreDefaults ? defaultRight : ShortcutRightColumn.ActualWidth;
        if (left <= 0) left = defaultLeft;
        if (right <= 0) right = defaultRight;

        left = Math.Clamp(left, ShortcutLeftMinimum, ShortcutLeftColumn.MaxWidth);
        right = _shortcutVariablesVisible ? Math.Clamp(right, rightMinimum, ShortcutRightColumn.MaxWidth) : 0d;
        ShortcutRightDividerColumn.Width = new GridLength(_shortcutVariablesVisible ? 16 : 0);
        var overflow = Math.Max(0, left + right - sideSpace);
        if (overflow > 0)
        {
            var rightReduction = Math.Min(overflow, right - rightMinimum);
            right -= rightReduction;
            overflow -= rightReduction;
            left -= Math.Min(overflow, left - ShortcutLeftMinimum);
        }

        ShortcutLeftColumn.Width = new GridLength(left);
        ShortcutRightColumn.Width = new GridLength(right);
        ShortcutLeftColumn.MaxWidth = Math.Max(
            ShortcutLeftMinimum,
            Math.Min(ShortcutLeftMaximum, sideSpace - right));
        ShortcutRightColumn.MaxWidth = Math.Max(
            ShortcutRightMinimum,
            Math.Min(ShortcutRightMaximum, sideSpace - left));
    }

    private void ShortcutSplitter_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Home)
        {
            RestoreShortcutSplitter((GridSplitter)sender);
            e.Handled = true;
            return;
        }
        if (e.Key is not (Key.Left or Key.Right))
        {
            return;
        }

        var direction = e.Key == Key.Right ? 1d : -1d;
        if (ReferenceEquals(sender, ShortcutLeftDivider))
        {
            ShortcutLeftColumn.Width = new GridLength(
                Math.Clamp(ShortcutLeftColumn.ActualWidth + (direction * ShortcutSplitterStep),
                    ShortcutLeftMinimum, ShortcutLeftColumn.MaxWidth));
        }
        else
        {
            ShortcutRightColumn.Width = new GridLength(
                Math.Clamp(ShortcutRightColumn.ActualWidth - (direction * ShortcutSplitterStep),
                    ShortcutRightMinimum, ShortcutRightColumn.MaxWidth));
        }
        NormalizeShortcutColumns(ActualWidth);
        e.Handled = true;
    }

    private void ShortcutSplitter_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        RestoreShortcutSplitter((GridSplitter)sender);
        e.Handled = true;
    }

    private void RestoreShortcutSplitter(GridSplitter splitter)
    {
        var band = _shortcutResponsiveBand;
        if (ReferenceEquals(splitter, ShortcutLeftDivider))
        {
            ShortcutLeftColumn.Width = new GridLength(band == 0 ? 240 : band == 1 ? 260 : 300);
        }
        else
        {
            ShortcutRightColumn.Width = new GridLength(band == 0 ? 240 : 280);
        }
        NormalizeShortcutColumns(ActualWidth);
    }

    private void ShortcutSplitter_OnDragStarted(object sender, DragStartedEventArgs e) =>
        ((GridSplitter)sender).Tag = "Dragging";

    private void ShortcutSplitter_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        ((GridSplitter)sender).Tag = null;
        NormalizeShortcutColumns(ActualWidth);
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    private void MainWindow_OnActivated(object? sender, EventArgs e)
    {
        if (_initialized &&
            _settings.Theme.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            ThemeService.Apply(_settings.Theme);
        }
    }
}
