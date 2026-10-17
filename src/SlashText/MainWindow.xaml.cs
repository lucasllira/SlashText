using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;
using Forms = System.Windows.Forms;

namespace SlashText;

public partial class MainWindow : Window
{
    private readonly SnippetMarkdownRepository _repository = new();
    private readonly KeyboardHookService _keyboardHook = new();
    private readonly TextExpansionService _expansionService = new();
    private readonly UsageService _usageService = new();
    private readonly QuickAccentService _quickAccentService = new();
    private readonly BackupService _backupService = new();
    private readonly AssetMaintenanceService _assetMaintenanceService = new();
    private readonly SnippetImportService _snippetImportService = new();
    private CancellationTokenSource? _captureImageLoadCancellation;
    private CancellationTokenSource? _captureThumbnailCancellation;
    private readonly CaptureService _captureService = new();
    private readonly GifRecordingService _gifRecordingService = new();
    private readonly GlobalCaptureShortcutService _captureShortcuts = new();
    private readonly UpdateService _updateService;
    private readonly PortableUpdateService _portableUpdateService = new();
    private readonly UpdateNotificationGate _updateNotificationGate = new();
    private readonly SingleFlightGate _expansionRequestGate = new();
    private readonly SingleFlightGate _captureGate = new();
    private readonly JsonFileStore<AppSettings> _settingsStore = new(AppPaths.SettingsFile);
    private readonly ObservableCollection<Snippet> _snippets = [];
    private readonly SuggestionWindow _suggestionWindow = new();
    private readonly QuickAccentWindow _quickAccentWindow = new();

    private Forms.NotifyIcon? _trayIcon;
    private string? _pendingCaptureNotificationPath;
    private AppSettings _settings = new();
    private Snippet? _selected;
    private string? _selectedCategory;
    private bool _showMostUsed;
    private bool _exitRequested;
    private bool _servicesDisposed;
    private bool _restoreRecoveryBlocked;
    private bool _initialized;
    private bool _snippetStorageAvailable = true;
    private bool _updatingQuickAccentSets;
    private bool _updatingCaptureRecordingCompact;
    private int _quickAccentPreviewIndex;
    private ScreenRecordingService? _recordingService;
    private GifRecordingSession? _gifRecordingSession;
    private RecordingControlWindow? _recordingControl;
    private string? _lastReleaseUrl;
    private int _updateOfferActive;
    private CancellationTokenSource? _activeUpdateCancellation;
    private CancellationTokenSource? _updateMonitorCancellation;
    private Task? _updateMonitorTask;
    private int _shortcutResponsiveBand = -1;
    private CaptureLauncherMode _captureLauncherMode = CaptureLauncherMode.Region;
    private CaptureMediaMode _captureMediaMode = CaptureMediaMode.Image;
    private CaptureRecord? _captureWorkbenchRecord;
    private string? _captureWorkbenchPath;
    private double _captureWorkbenchZoom = 1d;
    private bool _captureEditorExpanded;
    private bool _captureCommitInProgress;
    private TaskCompletionSource<CaptureImageEditResult?>? _pendingCaptureEdit;
    private CaptureEditorDocument? _completedCaptureDocument;
    private readonly Dictionary<UIElement, Visibility> _capturePageVisibility = new();
    private GridLength[]? _capturePageRowHeights;

    private static readonly DependencyProperty CaptureHistoryOffsetProperty =
        DependencyProperty.Register(
            nameof(CaptureHistoryOffset),
            typeof(double),
            typeof(MainWindow),
            new PropertyMetadata(0d, (target, args) =>
            {
                if (target is MainWindow window && window.CaptureHistoryScroller is not null)
                {
                    window.CaptureHistoryScroller.ScrollToHorizontalOffset((double)args.NewValue);
                }
            }));

    private double CaptureHistoryOffset
    {
        get => (double)GetValue(CaptureHistoryOffsetProperty);
        set => SetValue(CaptureHistoryOffsetProperty, value);
    }

    private const double ShortcutLeftMinimum = 220;
    private const double ShortcutLeftMaximum = 460;
    private const double ShortcutRightMinimum = 240;
    private const double ShortcutRightMaximum = 480;
    private const double ShortcutSplitterStep = 16;
    private const double ShortcutDividerSpace = 32;
    private static readonly TimeSpan UpdateMonitorInterval = TimeSpan.FromMinutes(30);

    private enum CaptureLauncherMode
    {
        ActiveMonitor,
        Region,
        Window,
        Scrolling,
        Gif
    }

    private enum CaptureMediaMode
    {
        Image,
        Video,
        Gif
    }

    public MainWindow() : this(captureEvidence: false) { }

    internal MainWindow(bool captureEvidence, UpdateService? updateService = null)
    {
        _updateService = updateService ?? new UpdateService();
        InitializeComponent();
        CategoryBox.ItemsSource = ShortcutCategories.All;
        SelectShortcutCategory("Geral");
        InitializeShortcutFormatting();
        ShortcutEditorPanel.SizeChanged += (_, _) => { if (_shortcutEditorExpanded) UpdateShortcutEditorHeight(); };
        CaptureInlineEditor.StateChanged += CaptureInlineEditor_OnStateChanged;
        CaptureEditorContext.Attach(CaptureInlineEditor);
        CaptureInlineEditor.SaveCopyRequested += (_, _) => SaveCapturePreview_OnClick(this, new RoutedEventArgs());
        CaptureInlineEditor.ExitRequested += (_, _) => { if (_captureEditorExpanded) SetCaptureEditorExpanded(false); };
        _captureService.ImageEditor = EditNewCaptureInWorkbenchAsync;
        CaptureImageMediaButton.IsChecked = true;
        CaptureRegionModeButton.IsChecked = true;
        InitializeRecordingPresetControls();
        // Unshown UI fixture: no tray, startup/data loading, hooks or updater.
        if (captureEvidence) return;
        InitializeTray();
        Loaded += MainWindow_OnLoaded;
        Closing += MainWindow_OnClosing;
        Closed += MainWindow_OnClosed;
        StateChanged += MainWindow_OnStateChanged;
        Activated += MainWindow_OnActivated;
        _keyboardHook.ExpansionRequested += KeyboardHook_OnExpansionRequested;
        _keyboardHook.SuggestionsChanged += KeyboardHook_OnSuggestionsChanged;
        _suggestionWindow.SnippetChosen += SuggestionWindow_OnSnippetChosen;
        _quickAccentService.Changed += QuickAccentService_OnChanged;
        _quickAccentService.CharacterInserted += QuickAccentService_OnCharacterInserted;
        _captureShortcuts.Triggered += CaptureShortcuts_OnTriggered;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        var startup = new StartupModuleCoordinator();
        try
        {
            var settingsLoaded = await startup.RunAsync(
                "Configurações",
                async () => _settings = await _settingsStore.LoadAsync());
            if (!settingsLoaded)
            {
                _settings = new AppSettings();
            }

            _settings.Capture ??= new CaptureSettings();
            _settings.Capture.Recording ??= new RecordingSettings();
            if (AppPaths.IsCapturePilot)
            {
                Title = AppPaths.IsLegacyBackupsPilot ? UiText.Get("SlashDesk — Piloto Backups antigos 3.3.0 · #119") : AppPaths.IsProtectedStoragePilot ? UiText.Get("SlashDesk — Piloto Dados protegidos 3.3.0 · #116") : AppPaths.IsBackupCompatibilityPilot ? UiText.Get("SlashDesk — Piloto Backups 3.3.0 · #117") : AppPaths.IsLanguagesPilot ? UiText.Get("SlashDesk — Piloto Idiomas 3.3.0 · #82") : AppPaths.IsAuxiliaryPilot ? UiText.Get("SlashDesk — Piloto Interfaces auxiliares 3.3.0 · #69") : AppPaths.IsAboutPilot ? UiText.Get("SlashDesk — Piloto Sobre 3.3.0 · #68") : AppPaths.IsSettingsPilot ? UiText.Get("SlashDesk — Piloto Configurações 3.3.0 · #67") : AppPaths.IsStatisticsPilot ? UiText.Get("SlashDesk — Piloto Estatísticas 3.3.0 · #66") : AppPaths.IsQuickAccentPilot ? UiText.Get("SlashDesk — Piloto Acento Rápido 3.3.0 · #65") : AppPaths.IsCaptureOcrPilot ? UiText.Get("SlashDesk — Piloto Captura + OCR 3.3.0") : AppPaths.IsCaptureComplementsPilot ? UiText.Get("SlashDesk — Piloto Complementos da Captura 3.3.0 · #74") : AppPaths.IsShortcutsPilot ? UiText.Get("SlashDesk — Piloto Atalhos 3.3.0 · #64") : UiText.Get("SlashDesk — Piloto Captura 3.3.0 · #63");
                if (!AppPaths.IsSettingsPilot && !AppPaths.IsAboutPilot && !AppPaths.IsAuxiliaryPilot && !AppPaths.IsLanguagesPilot && !AppPaths.IsBackupCompatibilityPilot && !AppPaths.IsProtectedStoragePilot && !AppPaths.IsLegacyBackupsPilot)
                {
                    _settings.CheckUpdatesOnStartup = false;
                    _settings.StartWithWindows = false;
                }
                _settings.OnboardingCompleted = true;
                if (!AppPaths.IsSettingsPilot && !AppPaths.IsAboutPilot && !AppPaths.IsAuxiliaryPilot && !AppPaths.IsLanguagesPilot && !AppPaths.IsBackupCompatibilityPilot && !AppPaths.IsProtectedStoragePilot && !AppPaths.IsLegacyBackupsPilot) _settings.CloseToTray = false;
                if (!File.Exists(AppPaths.SettingsFile) || _settingsStore.IsProtected || AppPaths.IsProtectedStoragePilot || AppPaths.IsLegacyBackupsPilot)
                    _settings.Capture.OutputDirectoryTemplate = Path.Combine(AppPaths.DataDirectory, "Capturas", "{year}", "{month}");
                StartWithWindowsCheckBox.IsEnabled = false;
                CheckUpdatesCheckBox.IsEnabled = false;
                CheckUpdatesButton.IsEnabled = false;
                SettingsCheckUpdatesButton.IsEnabled = false;
            }
            RecordingPresetCatalog.Normalize(_settings.Capture.Recording);
            ThemeService.Apply(_settings.Theme);
            if (_trayIcon?.ContextMenuStrip is { } startupTrayMenu)
            {
                ApplyTrayTheme(startupTrayMenu);
            }

            await startup.RunAsync(
                "Estatísticas",
                _usageService.LoadAsync);

            CloseToTrayCheckBox.IsChecked = _settings.CloseToTray;
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            ShowSuggestionsCheckBox.IsChecked = _settings.ShowSuggestions;
            CheckUpdatesCheckBox.IsChecked = _settings.CheckUpdatesOnStartup;
            RefreshAboutPresentation();
            SettingsVersionText.Text =
                UiText.Format($"Versão atual: {ProductVersion()}");
            UpdateChannelText.Text =
                UiText.Format($"Canal: estável · modo {AppPaths.Mode.ToString().ToLowerInvariant()}");
            BackupLocationText.Text =
                UiText.Format($"Backups em {AppPaths.BackupsDirectory}. Nenhum arquivo é enviado para a nuvem.");

            await startup.RunAsync(
                "Estado de atualização",
                RefreshUpdateStatusAsync);

            SelectComboByTag(ThemeBox, _settings.Theme);
            SelectComboByTag(LanguageBox, UiText.Normalize(_settings.Language));
            LoadQuickAccentControls();

            await startup.RunAsync(
                "Histórico de captura",
                async () =>
                {
                    await _captureService.LoadAsync();
                    await _captureService.CleanOlderThanAsync(
                        _settings.Capture.HistoryRetentionDays,
                        deleteFiles: false);
                });
            LoadCaptureSettings();
            _initialized = true;

            IReadOnlyList<Snippet> loaded = [];
            _snippetStorageAvailable = await startup.RunAsync(
                "Atalhos",
                async () => loaded = await _repository.LoadAsync());
            if (_snippetStorageAvailable)
                loaded = await ShortcutShareMessage.RenameUntouchedDefaultAsync(_repository, loaded);
            ReplaceList(loaded);

            startup.Run(
                "Backup diário",
                _backupService.CreateDailySnapshot);
            startup.Run(
                "Monitor de atalhos",
                StartMonitoring);
            startup.Run(
                "Acento Rápido",
                _quickAccentService.Start);
            StartQuickAccentStatusMonitor();

            if (_snippets.Count > 0)
            {
                SelectSnippet(_snippets[0]);
            }
            else
            {
                BeginNewSnippet();
            }

            RefreshStatistics();
            ShowView(ShortcutsView, ShortcutsTabButton);
            StatusText.Text =
                UiText.Format($"{_snippets.Count} atalho(s) carregado(s)");
            startup.Run(
                "Atalhos globais de captura",
                ConfigureCaptureShortcuts);
            RefreshCaptureHistory();
            if (_settingsStore.IsProtected || _captureService.IsHistoryProtected || AppPaths.IsLegacyBackupsPilot || AppPaths.IsProtectedStoragePilot || AppPaths.IsLanguagesPilot || AppPaths.IsBackupCompatibilityPilot) ShowView(SettingsView, SettingsTabButton);
            else if (AppPaths.IsAuxiliaryPilot) ShowView(SettingsView, SettingsTabButton);
            else if (AppPaths.IsAboutPilot) ShowView(AboutView, AboutTabButton);
            else if (AppPaths.IsSettingsPilot) ShowView(SettingsView, SettingsTabButton);
            else if (AppPaths.IsStatisticsPilot) ShowView(StatisticsView, StatisticsTabButton);
            else if (AppPaths.IsQuickAccentPilot) ShowView(QuickAccentView, QuickAccentTabButton);
            else if (AppPaths.IsCapturePilot && !AppPaths.IsShortcutsPilot) ShowView(CaptureView, CaptureTabButton);

            if (!_settingsStore.IsProtected && !_settings.OnboardingCompleted)
            {
                await startup.RunAsync(
                    "Onboarding",
                    async () =>
                    {
                        var onboarding =
                            new OnboardingWindow { Owner = this };
                        onboarding.ShowDialog();
                        _settings.OnboardingCompleted = true;
                        await SavePreferencesAsync();
                    });
            }

            StartUpdateMonitor();
            ShowStartupWarnings(startup);
        }
        catch (Exception exception)
        {
            AppDiagnosticLog.Write(
                "startup.shell.failed",
                ("exceptionType", exception.GetType().FullName),
                ("hresult", $"0x{exception.HResult:X8}"));
            MessageBox.Show(
                UiText.Get("O SlashDesk encontrou uma falha inesperada ao preparar a interface. ") +
                UiText.Get("Nenhum dado foi apagado. Consulte a pasta Logs para o diagnóstico."),
                "SlashDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            BeginNewSnippet();
        }
    }

    private void ShowStartupWarnings(
        StartupModuleCoordinator startup)
    {
        if (startup.Failures.Count == 0)
        {
            return;
        }

        var modules = string.Join(
            Environment.NewLine,
            startup.Failures
                .Select(failure => $"• {UiText.Get(failure.Module)}")
                .Distinct(StringComparer.CurrentCultureIgnoreCase));
        var protectedStorage = _snippetStorageAvailable
            ? string.Empty
            : Environment.NewLine + Environment.NewLine +
              UiText.Get("O arquivo snippets.md não será alterado enquanto estiver indisponível. ") +
              UiText.Get("Use Restaurar ZIP na área de backups para recuperar os atalhos.");

        StatusText.Text =
            UiText.Format($"SlashDesk iniciado em modo reduzido · {startup.Failures.Count} módulo(s) indisponível(is)");
        ShowAppDialog(
            UiText.Get("O SlashDesk iniciou, mas alguns módulos não puderam ser carregados:") +
            Environment.NewLine + Environment.NewLine +
            modules +
            protectedStorage +
            Environment.NewLine + Environment.NewLine +
            UiText.Get("Os demais recursos continuam disponíveis. Consulte a pasta Logs para detalhes técnicos."),
            UiText.Get("SlashDesk iniciado em modo reduzido"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private bool EnsureSnippetStorageAvailable()
    {
        if (_snippetStorageAvailable)
        {
            return true;
        }

        ShowAppDialog(
            UiText.Get("O arquivo snippets.md não pôde ser carregado e está protegido contra alterações. ") +
            UiText.Get("Restaure um backup válido antes de salvar, excluir ou importar atalhos."),
            UiText.Get("Atalhos em modo protegido"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return false;
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_servicesDisposed) return;
        if (_recordingExitPending) { e.Cancel = true; return; }
        if (_restoreRecoveryBlocked) { _pendingCaptureEdit?.TrySetResult(null); DisposeServices(); return; }
        if (_settingsOperationInProgress) { e.Cancel = true; _exitRequested = false; StatusText.Text = UiText.Get("Aguarde a manutenção em andamento antes de sair."); return; }
        if (!_exitRequested && _settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            ShowTrayBalloon(
                1800,
                UiText.Get("SlashDesk continua ativo"),
                UiText.Get("Use o ícone da bandeja para abrir ou sair."),
                Forms.ToolTipIcon.Info);
            return;
        }

        if (_recordingService?.IsRecording == true)
        {
            e.Cancel = true; _exitRequested = false;
            _recordingService.Stop();
            _recordingControl?.Activate();
            StatusText.Text = UiText.Get("Aguarde a parada da gravação. Se ela não for confirmada, use Encerrar SlashDesk no controle de gravação.");
            return;
        }
        if (!CanDiscardShortcutDraft()) { e.Cancel = true; _exitRequested = false; return; }
        _activeUpdateCancellation?.Cancel();
        if (CaptureInlineEditor.HasUnsavedChanges && ShowAppDialog(UiText.Get("Há alterações de imagem não salvas. Sair sem salvar?"), UiText.Get("Editor de imagem"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
        { e.Cancel = true; _exitRequested = false; return; }
        if (_recordingOperation is { Phase: RecordingOperationPhase.Preparing } operation)
        {
            e.Cancel = true;
            _recordingExitPending = true;
            _exitRequested = true;
            operation.Cancel();
            _ = CloseAfterRecordingPreparationAsync(operation);
            return;
        }
        _pendingCaptureEdit?.TrySetResult(null);
        DisposeServices();
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        DisposeServices();
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowFromTray()
    {
        if (!CanUseRecordingInterface) return;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void RequestExit()
    {
        _exitRequested = true;
        Close();
    }

    private void DisposeServices()
    {
        if (_servicesDisposed)
        {
            return;
        }

        _servicesDisposed = true;
        _recordingOperation?.Cancel();
        _captureImageLoadCancellation?.Cancel();
        _captureThumbnailCancellation?.Cancel();
        StopQuickAccentStatusMonitor();
        StopUpdateMonitor();
        Activated -= MainWindow_OnActivated;
        _keyboardHook.ExpansionRequested -= KeyboardHook_OnExpansionRequested;
        _keyboardHook.SuggestionsChanged -= KeyboardHook_OnSuggestionsChanged;
        _keyboardHook.Dispose();
        _quickAccentService.Changed -= QuickAccentService_OnChanged;
        _quickAccentService.CharacterInserted -= QuickAccentService_OnCharacterInserted;
        _quickAccentService.Dispose();
        _captureShortcuts.Triggered -= CaptureShortcuts_OnTriggered;
        _captureShortcuts.Dispose();
        CleanupRecordingResources();
        CaptureInlineEditor.Dispose();
        _suggestionWindow.Close();
        _quickAccentWindow.Close();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
    }
}
