using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SlashText.Models;
using SlashText.Services;
using SlashText.Views;
using Forms = System.Windows.Forms;
using Button = System.Windows.Controls.Button;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;

namespace SlashText;

public partial class MainWindow : Window
{
    private readonly SnippetMarkdownRepository _repository = new();
    private readonly KeyboardHookService _keyboardHook = new();
    private readonly TextExpansionService _expansionService = new();
    private readonly UsageService _usageService = new();
    private readonly QuickAccentService _quickAccentService = new();
    private readonly BackupService _backupService = new();
    private readonly SnippetImportService _snippetImportService = new();
    private readonly CaptureService _captureService = new();
    private readonly GifRecordingService _gifRecordingService = new();
    private readonly GlobalCaptureShortcutService _captureShortcuts = new();
    private readonly UpdateService _updateService = new();
    private readonly JsonFileStore<AppSettings> _settingsStore = new(AppPaths.SettingsFile);
    private readonly ObservableCollection<Snippet> _snippets = [];
    private readonly SuggestionWindow _suggestionWindow = new();
    private readonly QuickAccentWindow _quickAccentWindow = new();

    private Forms.NotifyIcon? _trayIcon;
    private AppSettings _settings = new();
    private Snippet? _selected;
    private bool _exitRequested;
    private bool _servicesDisposed;
    private bool _initialized;
    private bool _updatingQuickAccentSets;
    private readonly DispatcherTimer _quickAccentPreviewTimer =
        new() { Interval = TimeSpan.FromMilliseconds(900) };
    private int _quickAccentPreviewIndex = 1;
    private ScreenRecordingService? _recordingService;
    private GifRecordingSession? _gifRecordingSession;
    private RecordingControlWindow? _recordingControl;

    public MainWindow()
    {
        InitializeComponent();
        InitializeRecordingPresetControls();
        InitializeTray();
        Loaded += MainWindow_OnLoaded;
        Closing += MainWindow_OnClosing;
        Closed += MainWindow_OnClosed;
        StateChanged += MainWindow_OnStateChanged;
        Activated += MainWindow_OnActivated;
        _keyboardHook.ExpansionRequested += KeyboardHook_OnExpansionRequested;
        _keyboardHook.SuggestionsChanged += KeyboardHook_OnSuggestionsChanged;
        _quickAccentService.Changed += QuickAccentService_OnChanged;
        _quickAccentService.CharacterInserted += QuickAccentService_OnCharacterInserted;
        _captureShortcuts.Triggered += CaptureShortcuts_OnTriggered;
        _quickAccentPreviewTimer.Tick += (_, _) =>
        {
            _quickAccentPreviewIndex = _quickAccentPreviewIndex >= 7
                ? 1
                : _quickAccentPreviewIndex + 1;
            UpdateQuickAccentPreviewSelection();
        };
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }
        try
        {
            _settings = await _settingsStore.LoadAsync();
            _settings.Capture ??= new CaptureSettings();
            _settings.Capture.Recording ??= new RecordingSettings();
            RecordingPresetCatalog.Normalize(_settings.Capture.Recording);
            ThemeService.Apply(_settings.Theme);
            await _usageService.LoadAsync();
            CloseToTrayCheckBox.IsChecked = _settings.CloseToTray;
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            ShowSuggestionsCheckBox.IsChecked = _settings.ShowSuggestions;
            CheckUpdatesCheckBox.IsChecked = _settings.CheckUpdatesOnStartup;
            SelectComboByTag(ThemeBox, _settings.Theme);
            QuickAccentEnabledCheckBox.IsChecked = _settings.QuickAccentEnabled;
            SelectComboByTag(QuickAccentActivationBox, _settings.QuickAccentActivationKey);
            SelectComboByTag(QuickAccentPositionBox, _settings.QuickAccentToolbarPosition);
            QuickAccentUnicodeCheckBox.IsChecked = _settings.QuickAccentShowUnicode;
            QuickAccentSortCheckBox.IsChecked = _settings.QuickAccentSortByUsage;
            QuickAccentDelayBox.Text = _settings.QuickAccentInputDelayMs.ToString();
            QuickAccentDelaySlider.Value = Math.Clamp(
                _settings.QuickAccentInputDelayMs,
                (int)QuickAccentDelaySlider.Minimum,
                (int)QuickAccentDelaySlider.Maximum);
            QuickAccentExcludedAppsBox.Text = _settings.QuickAccentExcludedApps;
            ApplyQuickAccentCharacterSetSelection(_settings.QuickAccentCharacterSets);
            ApplyQuickAccentSettings();
            await _captureService.LoadAsync();
            await _captureService.CleanOlderThanAsync(
                _settings.Capture.HistoryRetentionDays,
                deleteFiles: false);
            LoadCaptureSettings();
            _initialized = true;

            var loaded = await _repository.LoadAsync();
            ReplaceList(loaded);
            _backupService.CreateDailySnapshot();
            StartMonitoring();
            _quickAccentService.Start();

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
            StatusText.Text = $"{_snippets.Count} atalho(s) carregado(s)";
            ConfigureCaptureShortcuts();
            RefreshCaptureHistory();

            if (!_settings.OnboardingCompleted)
            {
                var onboarding = new OnboardingWindow { Owner = this };
                onboarding.ShowDialog();
                _settings.OnboardingCompleted = true;
                await _settingsStore.SaveAsync(_settings);
            }
            if (_settings.CheckUpdatesOnStartup)
            {
                _ = CheckUpdatesSilentlyAsync();
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"NÃ£o foi possÃ­vel iniciar o SlashDesk.\n\n{exception.Message}",
                "SlashDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            BeginNewSnippet();
        }
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
            // O Ã­cone da janela continua disponÃ­vel mesmo se o shell nÃ£o o extrair.
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Abrir SlashDesk", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Novo atalho", null, (_, _) => Dispatcher.Invoke(() =>
        {
            ShowFromTray();
            BeginNewSnippet();
        }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Dispatcher.Invoke(RequestExit));

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "SlashDesk Â· Monitoramento ativo",
            Icon = icon ?? DrawingSystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private void StartMonitoring()
    {
        _keyboardHook.UpdateSnippets(_snippets);
        if (!_keyboardHook.IsRunning)
        {
            _keyboardHook.Start();
        }

        MonitorStatusText.Text = "â— Monitoramento ativo";
    }

    private void QuickAccentService_OnChanged(object? sender, QuickAccentChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!e.Visible)
            {
                _quickAccentWindow.Hide();
                return;
            }

            _quickAccentWindow.UpdateChoices(
                e.Choices,
                e.SelectedIndex,
                _settings.QuickAccentToolbarPosition,
                _settings.QuickAccentShowUnicode);
        }));
    }

    private void QuickAccentService_OnCharacterInserted(
        object? sender,
        QuickAccentCharacterInsertedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            await _usageService.RecordQuickAccentAsync(e.Character);
            _quickAccentService.SetUsage(_usageService.QuickAccentCharacterCounts());
            RefreshStatistics();
        }));
    }

    private void KeyboardHook_OnSuggestionsChanged(object? sender, SnippetSuggestionsEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_settings.ShowSuggestions)
            {
                _suggestionWindow.Hide();
                return;
            }

            _suggestionWindow.UpdateSuggestions(e.Snippets, e.ScreenPosition);
        }));
    }

    private void KeyboardHook_OnExpansionRequested(
        object? sender,
        SnippetExpansionRequestedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                _suggestionWindow.Hide();
                IReadOnlyDictionary<string, string> values =
                    new Dictionary<string, string>();
                var fields = _expansionService.GetFillableFields(e.Snippet);

                if (fields.Count > 0)
                {
                    var form = VariableInputWindow.ShowForTarget(fields, e.TargetWindow);
                    if (form.DialogResult != true)
                    {
                        StatusText.Text = "ExpansÃ£o cancelada";
                        return;
                    }

                    values = form.Values;
                }

                var inserted = await _expansionService.ExpandAsync(
                    e.Snippet,
                    values,
                    e.TargetWindow);
                await _usageService.RecordAsync(e.Snippet, inserted);
                RefreshStatistics();
                StatusText.Text = $"{e.Snippet.Trigger} inserido";
            }
            catch (Exception exception)
            {
                StatusText.Text = $"Falha ao inserir {e.Snippet.Trigger}";
                MessageBox.Show(
                    exception.Message,
                    "NÃ£o foi possÃ­vel inserir o texto",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }));
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e) =>
        RefreshNavigation();

    private void RefreshNavigation()
    {
        if (CategoriesPanel is null)
        {
            return;
        }

        var query = SearchBox.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _snippets
            : _snippets.Where(item =>
                item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.Trigger.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        CategoriesPanel.Children.Clear();
        foreach (var group in filtered
                     .GroupBy(item => item.Category)
                     .OrderBy(item => item.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var list = new StackPanel();
            foreach (var snippet in group.OrderBy(item => item.Trigger))
            {
                list.Children.Add(CreateSnippetButton(snippet));
            }

            CategoriesPanel.Children.Add(new Expander
            {
                Header = $"{group.Key}  Â·  {group.Count()}",
                IsExpanded = true,
                Margin = new Thickness(0, 0, 0, 8),
                FontWeight = FontWeights.SemiBold,
                Content = list
            });
        }

        RefreshMostUsed();
    }

    private Button CreateSnippetButton(Snippet snippet)
    {
        var button = new Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = ReferenceEquals(snippet, _selected)
                ? (Brush)FindResource("SelectedBrush")
                : Brushes.Transparent,
            Padding = new Thickness(9, 7, 9, 7),
            Margin = new Thickness(0, 3, 0, 0),
            Tag = snippet,
            Content = new TextBlock
            {
                Text = $"{snippet.Trigger}  Â·  {snippet.Name}",
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = FontWeights.Normal
            }
        };
        button.Click += (_, _) => SelectSnippet((Snippet)button.Tag);
        return button;
    }

    private void RefreshMostUsed()
    {
        MostUsedPanel.Children.Clear();
        var ranked = _snippets
            .Select(item => new { Snippet = item, Usage = _usageService.For(item.Id) })
            .Where(item => item.Usage?.Count > 0)
            .OrderByDescending(item => item.Usage!.Count)
            .Take(3)
            .ToList();

        if (ranked.Count == 0)
        {
            MostUsedPanel.Children.Add(new TextBlock
            {
                Text = "Os atalhos usados aparecerÃ£o aqui.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontSize = 12
            });
            return;
        }

        foreach (var item in ranked)
        {
            var button = CreateSnippetButton(item.Snippet);
            button.Content = $"{item.Snippet.Trigger}  Â·  {item.Usage!.Count}x";
            MostUsedPanel.Children.Add(button);
        }
    }

    private void SelectSnippet(Snippet snippet)
    {
        _selected = snippet;
        NameBox.Text = snippet.Name;
        TriggerBox.Text = snippet.Trigger;
        CategoryBox.Text = snippet.Category;
        FormatBox.SelectedIndex = snippet.Format == SnippetFormat.Markdown ? 1 : 0;
        RichTextMarkdownConverter.Load(ContentEditor, snippet.Content, snippet.Format);
        StatusText.Text = snippet.Enabled ? "Atalho ativo" : "Atalho pausado";
        RefreshNavigation();
        UpdatePreview();
    }

    private void NewSnippet_OnClick(object sender, RoutedEventArgs e) =>
        BeginNewSnippet();

    private void BeginNewSnippet()
    {
        _selected = null;
        Nam×Þ{îÚ$z{-®éÜj×V6VçB—2æ÷BçVÆÂ¢°¢f"Ö÷7E&V6VçEF‚Òö6GW&U6W'f–6Rå&W6öÇfTf–ÆUF‚†Ö÷7E&V6VçB“°¢6GW&U&Wf–WtFWF–Ç5FW‡BåFW‡BÐ¢B'¶Ö÷7E&V6VçBä7&VFVDC¦FBôÔÒ÷———’„ƒ¦Ö×Ò+r¶Ö÷7E&V6VçBåG—WÒ+r"°¢B'¶Ö÷7E&V6VçBåv–GF‡Ü9w¶Ö÷7E&V6VçBä†V–v‡GÒ#°¢–b‚Ö÷7E&V6VçBäÖVF–¶–æBäWVÇ2‚'f–FVò"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’b`¢7G&–ærä—4çVÆÄ÷%v†—FU76R†Ö÷7E&V6VçEF‚’b`¢f–ÆRäW†—7G2†Ö÷7E&V6VçEF‚’¢°¢G'¢°¢W6–ærf"7G&VÒÒf–ÆRä÷Vâ€¢Ö÷7E&V6VçEF‚À¢f–ÆTÖöFRä÷VâÀ¢f–ÆT66W72å&VBÀ¢f–ÆU6†&Rå&VEw&—FR“°¢f"–ÖvRÒæWr&—FÖ–ÖvR‚“°¢–ÖvRä&Vv–ä–æ—B‚“°¢–ÖvRä66†T÷F–öâÒ&—FÖ66†T÷F–öâäöäÆöC°¢–ÖvRå7G&VÕ6÷W&6RÒ7G&VÓ°¢–ÖvRäVæD–æ—B‚“°¢–ÖvRäg&VW¦R‚“°¢6GW&U&Wf–Wt–ÖvRå6÷W&6RÒ–ÖvS°¢6GW&U&Wf–WtV×G•æVÂåf—6–&–Æ—G’Òf—6–&–Æ—G’ä6öÆÆ6VC°¢Ð¢6F6‚„”ôW†6WF–öâ¢°¢òòò†—7L;7&–6ò6öçF–çVF—7öì:×fVÂÖW6Öò6RÖ–æ–GW&ì:6òVFW"6W"&W'Fà¢Ð¢Ð¢Ð ¢f"f–ÇFW"Ò6GW&T†—7F÷'”f–ÇFW$&÷‚—2çVÆÀ¢ò&ÆÂ ¢¢6VÆV7FVEFr„6GW&T†—7F÷'”f–ÇFW$&÷‚Â&ÆÂ"“°¢f"f–ÇFW&VBÒö6GW&U6W'f–6Rä†—7F÷'’åv†W&R†—FVÒÓà¢f–ÇFW"äWVÇ2‚&ÆÂ"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’ÇÀ¢f–ÇFW"äWVÇ2‚'f–FVò"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’b`¢—FVÒäÖVF–¶–æBäWVÇ2‚'f–FVò"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’ÇÀ¢f–ÇFW"äWVÇ2‚&v–b"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’b`¢—FVÒäÖVF–¶–æBäWVÇ2‚&v–b"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’ÇÀ¢—FVÒåG—RäWVÇ2†f–ÇFW"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’“°¢f÷&V6‚‡f"—FVÒ–âf–ÇFW&VBåF¶RƒC’¢°¢f"&W6öÇfVEF‚Òö6GW&U6W'f–6Rå&W6öÇfTf–ÆUF‚†—FVÒ“°¢f"f–ÆRÒ7G&–ærä—4çVÆÄ÷%v†—FU76R‡&W6öÇfVEF‚¢ò%6öÖVçFR6Æ—&ö&B ¢¢F‚ävWDf–ÆTæÖR‡&W6öÇfVEF‚“°¢f"&÷rÒæWrw&–@¢°¢Ö&v–âÒæWrF†–6¶æW72ƒÂÂÂr¢Ó°¢&÷rä6öÇVÖäFVf–æ—F–öç2äFB†æWr6öÇVÖäFVf–æ—F–öâ‚’“°¢&÷rä6öÇVÖäFVf–æ—F–öç2äFB†æWr6öÇVÖäFVf–æ—F–öâ²v–GF‚Òw&–DÆVæwF‚äWFòÒ“°¢f"GW&F–öâÒ—FVÒäGW&F–öå6V6öæG2â ¢òB"+rµF–ÖU7âäg&öÕ6V6öæG2†—FVÒäGW&F–öå6V6öæG2“¦ÖÕÅÃ§77Ò ¢¢7G&–æräV×G“°¢&÷rä6†–ÆG&VâäFB†æWrFW‡D&Æö6°¢°¢FW‡BÒB'¶—FVÒä7&VFVDC¦FBôÔÒ„ƒ¦Ö×Ò+r¶—FVÒåG—WÒ+r"°¢B'¶—FVÒäÖVF–¶–æGÒ+r¶—FVÒåv–GF‡Ü9w¶—FVÒä†V–v‡G×¶GW&F–öçÒ+r¶f–ÆWÒ"À¢fW'F–6ÄÆ–væÖVçBÒfW'F–6ÄÆ–væÖVçBä6VçFW"À¢FW‡EG&–ÖÖ–ærÒFW‡EG&–ÖÖ–ærä6†&7FW$VÆÆ—6—2À¢f÷&Vw&÷VæBÒ„''W6‚”f–æE&W6÷W&6R‚$×WFVD''W6‚"¢Ò“°¢f"7F–öç2ÒæWr7F6µæVÂ²÷&–VçFF–öâÒ÷&–VçFF–öâä†÷&—¦öçFÂÓ°¢7F–öç2ä6†–ÆG&VâäFB„†—7F÷'”'WGFöâ‚$'&—""Â—FVÒÂ÷Vä†—7F÷'”—FVÕôöä6Æ–6²’“°¢7F–öç2ä6†–ÆG&VâäFB„†—7F÷'”'WGFöâ‚$6÷–""Â—FVÒÂ6÷”†—7F÷'”—FVÕôöä6Æ–6²’“°¢–b†—FVÒäÖVF–¶–æBäWVÇ2‚&–ÖvR"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’¢°¢7F–öç2ä6†–ÆG&VâäFB„†—7F÷'”'WGFöâ‚$VF—F""Â—FVÒÂVF—D†—7F÷'”—FVÕôöä6Æ–6²’“°¢Ð¢7F–öç2ä6†–ÆG&VâäFB„†—7F÷'”'WGFöâ‚$W†6ÇV—""Â—FVÒÂFVÆWFT†—7F÷'”—FVÕôöä6Æ–6²’“°¢w&–Bå6WD6öÇVÖâ†7F–öç2Â“°¢&÷rä6†–ÆG&VâäFB†7F–öç2“°¢6GW&T†—7F÷'•æVÂä6†–ÆG&VâäFB‡&÷r“°¢Ð¢–b„6GW&T†—7F÷'•æVÂä6†–ÆG&Vâä6÷VçBÓÒ¢°¢6GW&T†—7F÷'•æVÂä6†–ÆG&VâäFB†æWrFW‡D&Æö6°¢°¢FW‡BÒ$2;¦ÇF–Ö26GW&2&V6W,:6òV’â"À¢f÷&Vw&÷VæBÒ„''W6‚”f–æE&W6÷W&6R‚$×WFVD''W6‚"¢Ò“°¢Ð¢6GW&T†—7F÷'•7FGW5FW‡BåFW‡BÐ¢B'¶f–ÇFW&VBä6÷VçB‚“¤ãÒFRµö6GW&U6W'f–6Rä†—7F÷'’ä6÷VçC¤ãÒ—FVÒ†ç2’#°¢Ð ¢&—fFR7FF–2'WGFöâ†—7F÷'”'WGFöâ€¢7G&–ærÆ&VÂÀ¢6GW&U&V6÷&B&V6÷&BÀ¢&÷WFVDWfVçD†æFÆW"†æFÆW"¢°¢f"'WGFöâÒæWr'WGFöà¢°¢6öçFVçBÒÆ&VÂÀ¢FrÒ&V6÷&BÀ¢Ö–åv–GF‚Òc"À¢†V–v‡BÒ3"À¢Ö&v–âÒæWrF†–6¶æW72ƒbÂÂÂ’À¢FF–ærÒæWrF†–6¶æW72ƒ‚Â2Â‚Â2¢Ó°¢'WGFöâä6Æ–6²³Ò†æFÆW#°¢&WGW&â'WGFöã°¢Ð ¢&—fFRfö–B6GW&T†—7F÷'”f–ÇFW%ôöä6†ævVB†ö&¦V7B6VæFW"Â6VÆV7F–öä6†ævVDWfVçD&w2R’Óà¢&Vg&W6„6GW&T†—7F÷'’‚“° ¢&—fFRfö–B÷Vä†—7F÷'”—FVÕôöä6Æ–6²†ö&¦V7B6VæFW"Â&÷WFVDWfVçD&w2R¢°¢–b‡6VæFW"—2æ÷B'WGFöâ²Fs¢6GW&U&V6÷&B&V6÷&BÒ¢°¢&WGW&ã°¢Ð¢f"F‚Òö6GW&U6W'f–6Rå&W6öÇfTf–ÆUF‚‡&V6÷&B“°¢–b‡7G&–ærä—4çVÆÄ÷%v†—FU76R‡F‚’ÇÂf–ÆRäW†—7G2‡F‚’¢°¢ÖW76vT&÷‚å6†÷r€¢$ò'V—fòì:6òW7L:Ö—2F—7öì:×fVÂâ"À¢$†—7L;7&–6ò"À¢ÖW76vT&÷„'WGFöâäô²À¢ÖW76vT&÷„–ÖvRä–æf÷&ÖF–öâ“°¢&WGW&ã°¢Ð¢&ö6W72å7F'B†æWr&ö6W757F'D–æfò‡F‚’²W6U6†VÆÄW†V7WFRÒG'VRÒ“°¢Ð ¢&—fFRfö–B6÷”†—7F÷'”—FVÕôöä6Æ–6²†ö&¦V7B6VæFW"Â&÷WFVDWfVçD&w2R¢°¢–b‡6VæFW"—2æ÷B'WGFöâ²Fs¢6GW&U&V6÷&B&V6÷&BÒ¢°¢&WGW&ã°¢Ð¢G'¢°¢f"F‚Òö6GW&U6W'f–6Rå&W6öÇfTf–ÆUF‚‡&V6÷&B“°¢6GW&U6W'f–6Rä6÷”f–ÆUFô6Æ—&ö&B‡F‚“°¢7FGW5FW‡BåFW‡BÒB$'V—fò6÷–Fó¢µF‚ävWDf–ÆTæÖR‡F‚—Ò#°¢Ð¢6F6‚„W†6WF–öâW†6WF–öâ¢°¢ÖW76vT&÷‚å6†÷r€¢W†6WF–öâäÖW76vRÀ¢$†—7L;7&–6ò"À¢ÖW76vT&÷„'WGFöâäô²À¢ÖW76vT&÷„–ÖvRä–æf÷&ÖF–öâ“°¢Ð¢Ð ¢&—fFR7–æ2fö–BVF—D†—7F÷'”—FVÕôöä6Æ–6²†ö&¦V7B6VæFW"Â&÷WFVDWfVçD&w2R¢°¢–b‡6VæFW"—2æ÷B'WGFöâ²Fs¢6GW&U&V6÷&B&V6÷&BÒ¢°¢&WGW&ã°¢Ð¢–b†v—Bö6GW&U6W'f–6RäVF—DW†—7F–æt7–æ2‡&V6÷&Bä–BÂ÷6WGF–æw2ä6GW&RÂF†—2’¢°¢7FGW5FW‡BåFW‡BÐ¢B$6GW&GVÆ—¦F¢µF‚ävWDf–ÆTæÖR…ö6GW&U6W'f–6Rå&W6öÇfTf–ÆUF‚‡&V6÷&B’—Ò#°¢&Vg&W6„6GW&T†—7F÷'’‚“°¢Ð¢Ð ¢&—fFR7–æ2fö–BFVÆWFT†—7F÷'”—FVÕôöä6Æ–6²†ö&¦V7B6VæFW"Â&÷WFVDWfVçD&w2R¢°¢–b‡6VæFW"—2æ÷B'WGFöâ²Fs¢6GW&U&V6÷&B&V6÷&BÒ¢°¢&WGW&ã°¢Ð¢f"6†ö–6RÒÖW76vT&÷‚å6†÷r€¢%6–Ó¢W†6ÇV—"FÖ,:–Òò'V—fòÆö6ÂåÆäì:6ó¢&VÖ÷fW"6öÖVçFRFò†—7L;7&–6òâ"À¢$W†6ÇV—"6GW&"À¢ÖW76vT&÷„'WGFöâå–W4æô6æ6VÂÀ¢ÖW76vT&÷„–ÖvRåv&æ–ær“°¢–b†6†ö–6RÓÒÖW76vT&÷…&W7VÇBä6æ6VÂ¢°¢&WGW&ã°¢Ð¢v—Bö6GW&U6W'f–6RäFVÆWFT7–æ2€¢&V6÷&Bä–BÀ¢FVÆWFTf–ÆS¢6†ö–6RÓÒÖW76vT&÷…&W7VÇBå–W2“°¢&Vg&W6„6GW&T†—7F÷'’‚“°¢Ð ¢&—fFR7–æ2fö–B6ÆVä6GW&T†—7F÷'•ôöä6Æ–6²†ö&¦V7B6VæFW"Â&÷WFVDWfVçD&w2R¢°¢f"F—2Ò'6U6VÆV7FVD–çB„6GW&U&WFVçF–öä&÷‚Â““°¢–b†F—2ÃÒ¢°¢6GW&T†—7F÷'•7FGW5FW‡BåFW‡BÒ$Æ–×W¦WFöÜ:F–6FW6F—fF#°¢&WGW&ã°¢Ð¢f"6÷VçBÒv—Bö6GW&U6W'f–6Rä6ÆVäöÆFW%F†ä7–æ2†F—2ÂFVÆWFTf–ÆW3¢fÇ6R“°¢6GW&T†—7F÷'•7FGW5FW‡BåFW‡BÒB'¶6÷VçC¤ãÒVçG&F‡2’çF–v‡2’&VÖ÷f–F‡2’#°¢&Vg&W6„6GW&T†—7F÷'’‚“°¢Ð ¢&—fFRfö–B÷Vä6GW&TföÆFW%ôöä6Æ–6²†ö&¦V7B6VæFW"Â&÷WFVDWfVçD&w2R¢°¢f"F—&V7F÷'’Ò6GW&U6W'f–6Rå&W6öÇfTF—&V7F÷'•FV×ÆFR€¢Vçf—&öæÖVçBäW‡æDVçf—&öæÖVçEf&–&ÆW2…÷6WGF–æw2ä6GW&Rä÷WGWDF—&V7F÷'•FV×ÆFR’À¢FFUF–ÖTöfg6WBäæ÷r“°¢F—&V7F÷'’ä7&VFTF—&V7F÷'’†F—&V7F÷'’“°¢&ö6W72å7F'B†æWr&ö6W757F'D–æfò†F—&V7F÷'’’²W6U6†VÆÄW†V7WFRÒG'VRÒ“°¢Ð ¢&—fFR7–æ2fö–B6†V6µWFFW5ôöä6Æ–6²†ö&¦V7B6VæFW"Â&÷WFVDWfVçD&w2R¢°¢G'¢°¢f"&W7VÇBÒv—B÷WFFU6W'f–6Rä6†V6´7–æ2‚“°¢f"6†ö–6RÒÖW76vT&÷‚å6†÷r€¢&W7VÇBäÖW76vR²‡&W7VÇBåW&Â—2çVÆÂò7G&–æräV×G’¢%ÆåÆä'&—":v–æFRF÷væÆöCò"’À¢$GVÆ—¦:|;VW2Fò6Æ6„FW6²"À¢&W7VÇBåW&Â—2çVÆÂòÖW76vT&÷„'WGFöâäô²¢ÖW76vT&÷„'WGFöâå–W4æòÀ¢&W7VÇBåWFFTf–Æ&ÆRòÖW76vT&÷„–ÖvRä–æf÷&ÖF–öâ¢ÖW76vT&÷„–ÖvRäæöæR“°¢–b†6†ö–6RÓÒÖW76vT&÷…&W7VÇBå–W2bb&W7VÇBåW&Â—2æ÷BçVÆÂ¢°¢&ö6W72å7F'B†æWr&ö6W757F'D–æfò‡&W7VÇBåW&Â’²W6U6†VÆÄW†V7WFRÒG'VRÒ“°¢Ð¢Ð¢6F6‚„W†6WF–öâW†6WF–öâ¢°¢ÖW76vT&÷‚å6†÷r€¢B$ì:6òfö’÷7<:×fVÂ6öç7VÇF"òv—D‡V"åÆåÆç¶W†6WF–öâäÖW76vWÒ"À¢$GVÆ—¦:|;VW2Fò6Æ6„FW6²"À¢ÖW76vT&÷„'WGFöâäô²À¢ÖW76vT&÷„–ÖvRåv&æ–ær“°¢Ð¢Ð ¢&—fFR7–æ2F6²6†V6µWFFW56–ÆVçFÇ”7–æ2‚¢°¢G'¢°¢f"&W7VÇBÒv—B÷WFFU6W'f–6Rä6†V6´7–æ2‚“°¢–b‡&W7VÇBåWFFTf–Æ&ÆR¢°¢F—7F6†W"ä–çfö¶R‚‚’Óà¢°¢7FGW5FW‡BåFW‡BÒ&W7VÇBäÖW76vS°¢÷G&”–6öãòå6†÷t&ÆÆööåF—€¢#SÀ¢$GVÆ—¦:|:6òF—7öì:×fVÂ"À¢&W7VÇBäÖW76vRÀ¢f÷&×2åFööÅF—–6öâä–æfò“°¢Ò“°¢Ð¢Ð¢6F6€¢°¢òò–æ–6–Æ—¦:|:6òçVæ6:’&Æ÷VVF÷"VÖ6öç7VÇF÷6–öæÂà¢Ð¢Ð ¢&—fFRfö–B÷Väv—D‡V%ôöä6Æ–6²†ö&¦V7B6VæFW"Â&÷WFVDWfVçD&w2R¢°¢&ö6W72å7F'B†æWr&ö6W757F'D–æfò€¢&‡GG3¢òöv—F‡V"æ6öÒöÇV66ÆÆ—&õ6Æ6…FW‡B"¢°¢W6U6†VÆÄW†V7WFRÒG'VP¢Ò“°¢Ð ¢&—fFRfö–B&WÆ6TÆ—7B„”VçVÖW&&ÆSÅ6æ—WCâ6æ—WG2¢°¢÷6æ—WG2ä6ÆV"‚“°¢f÷&V6‚‡f"6æ—WB–â6æ—WG2¢°¢÷6æ—WG2äFB‡6æ—WB“°¢Ð¢&Vg&W6„æf–vF–öâ‚“°¢Ð ¢&—fFRfö–BÖ–åv–æF÷uôöå6—¦T6†ævVB†ö&¦V7B6VæFW"Â6—¦T6†ævVDWfVçD&w2R’Óà¢WFFU&W7öç6—fTÆ–÷WB†RäæWu6—¦Råv–GF‚“° ¢&—fFRfö–BWFFU&W7öç6—fTÆ–÷WB†F÷V&ÆRv–GF‚¢°¢–b…6†÷'F7WE6–FV&%æVÂ—2çVÆÂ¢°¢&WGW&ã°¢Ð ¢f"6ö×7BÒv–GF‚Âƒ°¢–b†6ö×7B¢°¢6†÷'F7WE6V6öæF'•&÷rä†V–v‡BÒæWrw&–DÆVæwF‚ƒ#S“°¢6†÷'F7WDÆVgD6öÇVÖâåv–GF‚ÒæWrw&–DÆVæwF‚ƒ#S“°¢6†÷'F7WDÆVgDF—f–FW$6öÇVÖâåv–GF‚Òw&–DÆVæwF‚äWFó°¢6†÷'F7WE&–v‡DF—f–FW$6öÇVÖâåv–GF‚Òw&–DÆVæwF‚äWFó°¢6†÷'F7WE&–v‡D6öÇVÖâåv–GF‚ÒæWrw&–DÆVæwF‚ƒ#S“° ¢w&–Bå6WE&÷r…6†÷'F7WDVF—F÷%æVÂÂ“°¢w&–Bå6WD6öÇVÖâ…6†÷'F7WDVF—F÷%æVÂÂ“°¢w&–Bå6WD6öÇVÖå7â…6†÷'F7WDVF—F÷%æVÂÂR“° ¢w&–Bå6WE&÷r…6†÷'F7WE6–FV&%æVÂÂ“°¢w&–Bå6WD6öÇVÖâ…6†÷'F7WE6–FV&%æVÂÂ“°¢w&–Bå6WD6öÇVÖå7â…6†÷'F7WE6–FV&%æVÂÂ"“° ¢w&–Bå6WE&÷r…6†÷'F7WEf&–&ÆW5æVÂÂ“°¢w&–Bå6WD6öÇVÖâ…6†÷'F7WEf&–&ÆW5æVÂÂ"“°¢w&–Bå6WD6öÇVÖå7â…6†÷'F7WEf&–&ÆW5æVÂÂ2“° ¢6†÷'F7WDÆVgDF—f–FW"åf—6–&–Æ—G’Òf—6–&–Æ—G’ä6öÆÆ6VC°¢6†÷'F7WE&–v‡DF—f–FW"åf—6–&–Æ—G’Òf—6–&–Æ—G’ä6öÆÆ6VC°¢Ð¢VÇ6P¢°¢6†÷'F7WE6V6öæF'•&÷rä†V–v‡BÒæWrw&–DÆVæwF‚ƒ“°¢6†÷'F7WDÆVgD6öÇVÖâåv–GF‚ÒæWrw&–DÆVæwF‚ƒ#c“°¢6†÷'F7WDÆVgDF—f–FW$6öÇVÖâåv–GF‚ÒæWrw&–DÆVæwF‚ƒ“°¢6†÷'F7WE&–v‡DF—f–FW$6öÇVÖâåv–GF‚ÒæWrw&–DÆVæwF‚ƒ“°¢6†÷'F7WE&–v‡D6öÇVÖâåv–GF‚ÒæWrw&–DÆVæwF‚ƒ#c“° ¢w&–Bå6WE&÷r…6†÷'F7WE6–FV&%æVÂÂ“°¢w&–Bå6WD6öÇVÖâ…6†÷'F7WE6–FV&%æVÂÂ“°¢w&–Bå6WD6öÇVÖå7â…6†÷'F7WE6–FV&%æVÂÂ“° ¢w&–Bå6WE&÷r…6†÷'F7WDVF—F÷%æVÂÂ“°¢w&–Bå6WD6öÇVÖâ…6†÷'F7WDVF—F÷%æVÂÂ"“°¢w&–Bå6WD6öÇVÖå7â…6†÷'F7WDVF—F÷%æVÂÂ“° ¢w&–Bå6WE&÷r…6†÷'F7WEf&–&ÆW5æVÂÂ“°¢w&–Bå6WD6öÇVÖâ…6†÷'F7WEf&–&ÆW5æVÂÂB“°¢w&–Bå6WD6öÇVÖå7â…6†÷'F7WEf&–&ÆW5æVÂÂ“° ¢6†÷'F7WDÆVgDF—f–FW"åf—6–&–Æ—G’Òf—6–&–Æ—G’åf—6–&ÆS°¢6†÷'F7WE&–v‡DF—f–FW"åf—6–&–Æ—G’Òf—6–&–Æ—G’åf—6–&ÆS°¢Ð¢Ð ¢&—fFRfö–BÖ–åv–æF÷uôöå7FFT6†ævVB†ö&¦V7Cò6VæFW"ÂWfVçD&w2R¢°¢–b…v–æF÷u7FFRÓÒv–æF÷u7FFRäÖ–æ–Ö—¦VB¢°¢†–FR‚“°¢Ð¢Ð ¢&—fFRfö–BÖ–åv–æF÷uôöä7F—fFVB†ö&¦V7Cò6VæFW"ÂWfVçD&w2R¢°¢–b…ö–æ—F–Æ—¦VBb`¢÷6WGF–æw2åF†VÖRäWVÇ2‚%7—7FVÒ"Â7G&–æt6ö×&—6öâä÷&F–æÄ–væ÷&T66R’¢°¢F†VÖU6W'f–6RäÇ’…÷6WGF–æw2åF†VÖR“°¢Ð¢Ð ¢&—fFRfö–BÖ–åv–æF÷uôöä6Æ÷6–ær†ö&¦V7Cò6VæFW"Â6æ6VÄWfVçD&w2R¢°¢–b‚öW†—E&WVW7FVBbb÷6WGF–æw2ä6Æ÷6UFõG&’¢°¢Rä6æ6VÂÒG'VS°¢†–FR‚“°¢÷G&”–6öãòå6†÷t&ÆÆööåF—€¢ƒÀ¢%6Æ6„FW6²6öçF–çVF—fò"À¢%W6Rò:Ö6öæRF&æFV¦&'&—"÷R6—"â"À¢f÷&×2åFööÅF—–6öâä–æfò“°¢&WGW&ã°¢Ð ¢F—7÷6U6W'f–6W2‚“°¢Ð ¢&—fFRfö–BÖ–åv–æF÷uôöä6Æ÷6VB†ö&¦V7Cò6VæFW"ÂWfVçD&w2R¢°¢F—7÷6U6W'f–6W2‚“°¢7—7FVÒåv–æF÷w2äÆ–6F–öâä7W'&VçBå6‡WFF÷vâ‚“°¢Ð ¢&—fFRfö–B6†÷tg&öÕG&’‚¢°¢6†÷r‚“°¢v–æF÷u7FFRÒv–æF÷u7FFRäæ÷&ÖÃ°¢7F—fFR‚“°¢Ð ¢&—fFRfö–B&WVW7DW†—B‚¢°¢öW†—E&WVW7FVBÒG'VS°¢6Æ÷6R‚“°¢Ð ¢&—fFRfö–BF—7÷6U6W'f–6W2‚¢°¢–b…÷6W'f–6W4F—7÷6VB¢°¢&WGW&ã°¢Ð ¢÷6W'f–6W4F—7÷6VBÒG'VS°¢7F—fFVBÓÒÖ–åv–æF÷uôöä7F—fFVC°¢ö¶W–&ö&D†öö²äW‡ç6–öå&WVW7FVBÓÒ¶W–&ö&D†ööµôöäW‡ç6–öå&WVW7FVC°¢ö¶W–&ö&D†öö²å7VvvW7F–öç46†ævVBÓÒ¶W–&ö&D†ööµôöå7VvvW7F–öç46†ævVC°¢ö¶W–&ö&D†öö²äF—7÷6R‚“°¢÷V–6´66VçE6W'f–6Rä6†ævVBÓÒV–6´66VçE6W'f–6Uôöä6†ævVC°¢÷V–6´66VçE6W'f–6Rä6†&7FW$–ç6W'FVBÓÒV–6´66VçE6W'f–6Uôöä6†&7FW$–ç6W'FVC°¢÷V–6´66VçE6W'f–6RäF—7÷6R‚“°¢ö6GW&U6†÷'F7WG2åG&–vvW&VBÓÒ6GW&U6†÷'F7WG5ôöåG&–vvW&VC°¢ö6GW&U6†÷'F7WG2äF—7÷6R‚“°¢÷&V6÷&F–æt6öçG&öÃòä6Æ÷6R‚“°¢÷&V6÷&F–æu6W'f–6SòäF—7÷6R‚“°¢öv–e&V6÷&F–æu6W76–öãòäF—7÷6R‚“°¢÷7VvvW7F–öåv–æF÷rä6Æ÷6R‚“°¢÷V–6´66VçEv–æF÷rä6Æ÷6R‚“°¢–b…÷G&”–6öâ—2æ÷BçVÆÂ¢°¢÷G&”–6öâåf—6–&ÆRÒfÇ6S°¢÷G&”–6öâäF—7÷6R‚“°¢Ð¢Ð§Ð