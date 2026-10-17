using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using SlashText.Services;
using SlashText.Views;
using Forms = System.Windows.Forms;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;

namespace SlashText;

public partial class MainWindow
{
    private Forms.ToolStripMenuItem? _quickAccentTrayToggle;
    private bool _quickAccentTraySaving;
    private string? _quickAccentTrayError;

    private void RefreshQuickAccentTrayState(Forms.ContextMenuStrip menu)
    {
        if (_quickAccentTrayToggle is not null)
        {
            _quickAccentTrayToggle.Text = _settings.QuickAccentEnabled ? UiText.Get("Inativar Acento Rápido") : UiText.Get("Ativar Acento Rápido");
            _quickAccentTrayToggle.Tag = _settings.QuickAccentEnabled ? "X" : "Check";
            _quickAccentTrayToggle.Enabled = !_settingsOperationInProgress && !_shortcutOperationInProgress && !_quickAccentTraySaving && !_settingsStore.IsProtected;
        }
        foreach (Forms.ToolStripMenuItem group in menu.Items.OfType<Forms.ToolStripMenuItem>())
        {
            if (group.Name is "Captures" or "Shortcuts") group.Enabled = !_settingsOperationInProgress && !_shortcutOperationInProgress && (group.Name != "Captures" || !_captureGate.IsActive);
            if (group.Name == "Recordings")
            {
                group.Enabled = CanUseRecordingInterface && !_settingsOperationInProgress && !_shortcutOperationInProgress;
                foreach (Forms.ToolStripMenuItem item in group.DropDownItems.OfType<Forms.ToolStripMenuItem>())
                    item.Enabled = item.Name == "CancelPreparation" ? _recordingOperation is { Phase: RecordingOperationPhase.Preparing } : !_captureGate.IsActive;
            }
        }
    }

    internal void OpenQuickAccentFromTray()
    {
        ShowFromTray(); ShowView(QuickAccentView, QuickAccentTabButton);
    }

    internal async Task<bool> SetQuickAccentEnabledFromTrayAsync(bool enabled)
    {
        _quickAccentTrayError = null;
        if (_settingsOperationInProgress || _shortcutOperationInProgress || _quickAccentTraySaving || _settingsStore.IsProtected) return false;
        _quickAccentTraySaving = true;
        await _quickAccentSaveGate.WaitAsync();
        var previous = _settings.QuickAccentEnabled;
        var root = (FrameworkElement)Content;
        var wasEnabled = root.IsEnabled;
        var ownsSettingsGate = false;
        try
        {
            if (_settingsOperationInProgress || _shortcutOperationInProgress) return false;
            _settingsOperationInProgress = true; ownsSettingsGate = true; root.IsEnabled = false;
            _settings.QuickAccentEnabled = enabled;
            await SavePreferencesAsync();
            _updatingQuickAccentControls = true;
            try { QuickAccentEnabledCheckBox.IsChecked = enabled; }
            finally { _updatingQuickAccentControls = false; }
            ApplyQuickAccentSettings();
            StatusText.Text = enabled ? UiText.Get("Acento Rápido ativado") : UiText.Get("Acento Rápido inativado");
            return true;
        }
        catch (Exception exception)
        {
            _settings.QuickAccentEnabled = previous;
            _quickAccentTrayError = UiText.Get("A preferência anterior foi mantida. ") + exception.Message;
            return false;
        }
        finally
        {
            if (ownsSettingsGate) { root.IsEnabled = wasEnabled; _settingsOperationInProgress = false; }
            _quickAccentSaveGate.Release(); _quickAccentTraySaving = false;
            if (_trayIcon?.ContextMenuStrip is { } menu) { RefreshQuickAccentTrayState(menu); ApplyTrayTheme(menu); }
        }
    }

    internal void RefreshTrayForEvidence(Forms.ContextMenuStrip menu) => RefreshQuickAccentTrayState(menu);

    internal Forms.ContextMenuStrip CreateTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.ShowImageMargin = true;
        menu.Padding = new Forms.Padding(4);

        var capturesMenu = new Forms.ToolStripMenuItem(UiText.Get("Capturas")) { Name = "Captures" };
        capturesMenu.DropDownItems.Add(
            "Monitor",
            null,
            (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                _ = RunCaptureAsync(
                    CaptureShortcutAction.ActiveMonitor,
                    invokedByShortcut: true))));
        capturesMenu.DropDownItems.Add(
            UiText.Get("Região"),
            null,
            (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                _ = RunCaptureAsync(
                    CaptureShortcutAction.Region,
                    invokedByShortcut: true))));
        capturesMenu.DropDownItems.Add(
            UiText.Get("Janela"),
            null,
            (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                _ = RunCaptureAsync(
                    CaptureShortcutAction.Window,
                    invokedByShortcut: true))));
        capturesMenu.DropDownItems.Add(
            UiText.Get("Captura longa"),
            null,
            (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                _ = RunScrollingCaptureAsync(invokedByShortcut: false))));

        var recordingsMenu = new Forms.ToolStripMenuItem(UiText.Get("Gravações")) { Name = "Recordings" };
        recordingsMenu.DropDownItems.Add(
            UiText.Get("Vídeo MP4"),
            null,
            (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                StartMp4Recording_OnClick(this, new RoutedEventArgs()))));
        recordingsMenu.DropDownItems.Add(
            UiText.Get("GIF animado"),
            null,
            (_, _) => Dispatcher.BeginInvoke(new Action(() =>
                StartGifRecording_OnClick(this, new RoutedEventArgs()))));
        var cancelPreparation = new Forms.ToolStripMenuItem(UiText.Get("Cancelar preparação")) { Name = "CancelPreparation", Tag = "X" };
        cancelPreparation.Click += (_, _) => Dispatcher.BeginInvoke(new Action(CancelRecordingPreparation));
        recordingsMenu.DropDownItems.Add(cancelPreparation);

        var shortcutsMenu = new Forms.ToolStripMenuItem(UiText.Get("Atalhos")) { Name = "Shortcuts" };
        shortcutsMenu.DropDownItems.Add(
            UiText.Get("Novo atalho"),
            null,
            (_, _) => Dispatcher.Invoke(() =>
            {
                ShowFromTray();
                BeginNewSnippet();
            }));
        shortcutsMenu.DropDownItems.Add(
            UiText.Get("Gerenciar atalhos"),
            null,
            (_, _) => Dispatcher.Invoke(() =>
            {
                ShowFromTray();
                ShowView(ShortcutsView, ShortcutsTabButton);
            }));

        var accentMenu = new Forms.ToolStripMenuItem(UiText.Get("Acento Rápido")) { Tag = "Languages" };
        _quickAccentTrayToggle = new Forms.ToolStripMenuItem();
        _quickAccentTrayToggle.Click += async (_, _) =>
        {
            if (!await SetQuickAccentEnabledFromTrayAsync(!_settings.QuickAccentEnabled) && _quickAccentTrayError is { } error)
            {
                ShowFromTray(); ShowSettingsDialog(error, UiText.Get("Não foi possível alterar o Acento Rápido"), image: MessageBoxImage.Warning);
            }
        };
        accentMenu.DropDownItems.Add(_quickAccentTrayToggle);
        accentMenu.DropDownItems.Add(UiText.Get("Gerenciar Acento Rápido"), null, (_, _) => Dispatcher.Invoke(OpenQuickAccentFromTray));
        var applicationMenu = new Forms.ToolStripMenuItem(UiText.Get("Aplicativo"));
        applicationMenu.DropDownItems.Add(
            UiText.Get("Abrir SlashDesk"),
            null,
            (_, _) => Dispatcher.Invoke(ShowFromTray));
        applicationMenu.DropDownItems.Add(
            UiText.Get("Configurações"),
            null,
            (_, _) => Dispatcher.Invoke(() =>
            {
                ShowFromTray();
                ShowView(SettingsView, SettingsTabButton);
            }));
        applicationMenu.DropDownItems.Add(
            UiText.Get("Sobre"),
            null,
            (_, _) => Dispatcher.Invoke(() =>
            {
                ShowFromTray();
                ShowView(AboutView, AboutTabButton);
            }));
        applicationMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        applicationMenu.DropDownItems.Add(
            UiText.Get("Sair"),
            null,
            (_, _) => Dispatcher.Invoke(RequestExit));

        menu.Items.Add(capturesMenu);
        menu.Items.Add(recordingsMenu);
        menu.Items.Add(shortcutsMenu);
        menu.Items.Add(accentMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(applicationMenu);
        var icons = new Dictionary<string, string>
        {
            ["Capturas"] = "Camera", ["Monitor"] = "Monitor", ["Região"] = "ScanLine", ["Janela"] = "AppWindow", ["Captura longa"] = "ScanText",
            ["Gravações"] = "Video", ["Vídeo MP4"] = "Video", ["GIF animado"] = "Film",
            ["Atalhos"] = "Keyboard", ["Novo atalho"] = "Plus", ["Gerenciar atalhos"] = "Keyboard",
            ["Aplicativo"] = "AppWindow", ["Abrir SlashDesk"] = "AppWindow", ["Configurações"] = "Settings", ["Sobre"] = "Info", ["Sair"] = "X",
            ["Gerenciar Acento Rápido"] = "Languages"
        };
        void AssignIcons(Forms.ToolStripItemCollection items)
        {
            foreach (Forms.ToolStripMenuItem item in items.OfType<Forms.ToolStripMenuItem>())
            {
                var source = icons.Keys.FirstOrDefault(key => UiText.Get(key) == item.Text);
                if (source is not null) item.Tag = icons[source];
                if (item.HasDropDownItems) AssignIcons(item.DropDownItems);
            }
        }
        AssignIcons(menu.Items);
        menu.Opening += (_, _) => RefreshQuickAccentTrayState(menu);
        TrayMenuTheme.Wire(menu);
        RefreshQuickAccentTrayState(menu); ApplyTrayTheme(menu);
        return menu;

    }

    private void InitializeTray()
    {
        DrawingIcon? icon = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath);
            }
        }
        catch (Exception)
        {
            // O ícone da janela continua disponível mesmo se o shell não o extrair.
        }

        var menu = CreateTrayMenu();

        _trayIcon = new Forms.NotifyIcon
        {
            Text = UiText.Get("SlashDesk · Monitoramento ativo"),
            Icon = icon ?? DrawingSystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _trayIcon.BalloonTipClicked += (_, _) =>
            Dispatcher.Invoke(OpenPendingCaptureNotification);
    }

    private void ShowTrayBalloon(
        int timeout,
        string title,
        string text,
        Forms.ToolTipIcon icon,
        string? openFilePath = null)
    {
        _pendingCaptureNotificationPath =
            string.IsNullOrWhiteSpace(openFilePath) ? null : openFilePath;
        _trayIcon?.ShowBalloonTip(timeout, title, text, icon);
    }

    private void OpenPendingCaptureNotification()
    {
        var path = _pendingCaptureNotificationPath;
        _pendingCaptureNotificationPath = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            if (_captureNotificationEvidenceLauncher is not null) _captureNotificationEvidenceLauncher(path);
            else Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            ShowAppDialog(
                exception.Message,
                UiText.Get("Não foi possível abrir a captura"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static void ApplyTrayTheme(Forms.ContextMenuStrip menu) => TrayMenuTheme.Apply(menu);
}
