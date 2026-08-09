using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
    private readonly PortableUpdateService _portableUpdateService = new();
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
    private string? _lastReleaseUrl;
    private int _updateOfferActive;
    private CancellationTokenSource? _activeUpdateCancellation;

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
            AboutVersionText.Text = $"Versão {ProductVersion()} · Licença MIT · código aberto";
            UpdateChannelText.Text = $"Canal: estável · modo {AppPaths.Mode.ToString().ToLowerInvariant()}";
            BackupLocationText.Text =
                $"Backups em {AppPaths.BackupsDirectory}. Nenhum arquivo é enviado para a nuvem.";
            await RefreshUpdateStatusAsync();
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
                $"Não foi possível iniciar o SlashDesk.\n\n{exception.Message}",
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
            // O ícone da janela continua disponível mesmo se o shell não o extrair.
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
            Text = "SlashDesk · Monitoramento ativo",
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

        MonitorStatusText.Text = "● Monitoramento ativo";
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
                        StatusText.Text = "Expansão cancelada";
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
                    "Não foi possível inserir o texto",
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
                Header = $"{group.Key}  ·  {group.Count()}",
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
                Text = $"{snippet.Trigger}  ·  {snippet.Name}",
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
                Text = "Os atalhos usados aparecerão aqui.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("MutedBrush"),
                FontSize = 12
            });
            return;
        }

        foreach (var item in ranked)
        {
            var button = CreateSnippetButton(item.Snippet);
            button.Content = $"{item.Snippet.Trigger}  ·  {item.Usage!.Count}x";
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
        NameBox.Clear();
        TriggerBox.Text = "/";
        CategoryBox.Text = "Geral";
        FormatBox.SelectedIndex = 0;
        RichTextMarkdownConverter.Load(ContentEditor, string.Empty, SnippetFormat.Plain);
        StatusText.Text = "Novo atalho";
        RefreshNavigation();
        NameBox.Focus();
        UpdatePreview();
    }

    private async void SaveSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        var previous = _selected;
        var format = FormatBox.SelectedIndex == 1 ? SnippetFormat.Markdown : SnippetFormat.Plain;
        var candidate = new Snippet
        {
            Id = previous?.Id ?? Guid.NewGuid(),
            Name = NameBox.Text.Trim(),
            Trigger = TriggerBox.Text.Trim(),
            Category = string.IsNullOrWhiteSpace(CategoryBox.Text) ? "Geral" : CategoryBox.Text.Trim(),
            Content = RichTextMarkdownConverter.Save(ContentEditor, format),
            Format = format,
            Enabled = previous?.Enabled ?? true,
            ConfirmKeys = previous?.ConfirmKeys.ToList() ?? ["Enter", "Tab", "Space"]
        };

        var nextState = _snippets
            .Where(item => !ReferenceEquals(item, previous))
            .Append(candidate)
            .ToList();

        try
        {
            await _repository.SaveAsync(nextState);
            if (previous is null)
            {
                _snippets.Add(candidate);
            }
            else
            {
                _snippets[_snippets.IndexOf(previous)] = candidate;
            }

            _selected = candidate;
            _keyboardHook.UpdateSnippets(_snippets);
            RefreshNavigation();
            RefreshStatistics();
            StatusText.Text = "Salvo em %LocalAppData%\\SlashDesk\\snippets.md";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Não foi possível salvar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void DeleteSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null)
        {
            return;
        }

        if (MessageBox.Show(
                $"Excluir o atalho {_selected.Trigger}?",
                "SlashDesk",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        var removed = _selected;
        _snippets.Remove(removed);
        try
        {
            await _repository.SaveAsync(_snippets);
            _keyboardHook.UpdateSnippets(_snippets);
            BeginNewSnippet();
            RefreshStatistics();
            StatusText.Text = "Atalho excluído";
        }
        catch (Exception exception)
        {
            _snippets.Add(removed);
            RefreshNavigation();
            MessageBox.Show(
                exception.Message,
                "Não foi possível excluir",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void FormatBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormattingToolbar is not null)
        {
            FormattingToolbar.Visibility =
                FormatBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            UpdatePreview();
        }
    }

    private void Bold_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleBold.Execute(null, ContentEditor);

    private void Italic_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleItalic.Execute(null, ContentEditor);

    private void Underline_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleUnderline.Execute(null, ContentEditor);

    private void FontFamilyBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContentEditor is null ||
            FontFamilyBox?.SelectedItem is not ComboBoxItem { Tag: string font })
        {
            return;
        }

        ContentEditor.Selection.ApplyPropertyValue(
            TextElement.FontFamilyProperty,
            new FontFamily(font));
        ContentEditor.Focus();
    }

    private void FontSizeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ContentEditor is null ||
            FontSizeBox?.SelectedItem is not ComboBoxItem { Tag: string sizeText } ||
            !double.TryParse(
                sizeText,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var size))
        {
            return;
        }

        ContentEditor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
        ContentEditor.Focus();
    }

    private void Color_OnClick(object sender, RoutedEventArgs e)
    {
        using var picker = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(24, 32, 43)
        };
        if (picker.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        var color = picker.Color;
        ContentEditor.Selection.ApplyPropertyValue(
            TextElement.ForegroundProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B)));
        ContentEditor.Focus();
    }

    private void Highlight_OnClick(object sender, RoutedEventArgs e)
    {
        using var picker = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.Color.FromArgb(255, 235, 59)
        };
        if (picker.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        var color = picker.Color;
        ContentEditor.Selection.ApplyPropertyValue(
            TextElement.BackgroundProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B)));
        ContentEditor.Focus();
    }

    private void Bullets_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleBullets.Execute(null, ContentEditor);

    private void Numbering_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleNumbering.Execute(null, ContentEditor);

    private void AlignLeft_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.AlignLeft.Execute(null, ContentEditor);

    private void AlignCenter_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.AlignCenter.Execute(null, ContentEditor);

    private void AlignRight_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.AlignRight.Execute(null, ContentEditor);

    private void Table_OnClick(object sender, RoutedEventArgs e)
    {
        var rowsText = PromptDialog.Show(this, "Inserir tabela", "Quantidade de linhas", "2");
        if (rowsText is null)
        {
            return;
        }

        var columnsText = PromptDialog.Show(this, "Inserir tabela", "Quantidade de colunas", "2");
        if (!int.TryParse(rowsText, out var rows) ||
            !int.TryParse(columnsText, out var columns) ||
            rows is < 1 or > 10 ||
            columns is < 1 or > 8)
        {
            MessageBox.Show(
                "Use de 1 a 10 linhas e de 1 a 8 colunas.",
                "Tabela",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var table = new Table
        {
            CellSpacing = 0,
            Margin = new Thickness(0, 6, 0, 6)
        };
        for (var column = 0; column < columns; column++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        table.RowGroups.Add(group);
        TableCell? firstCell = null;
        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            var row = new TableRow();
            group.Rows.Add(row);
            for (var columnIndex = 0; columnIndex < columns; columnIndex++)
            {
                var cell = new TableCell(new Paragraph(new Run(
                    rowIndex == 0 ? $"Título {columnIndex + 1}" : "Texto")))
                {
                    BorderBrush = (Brush)FindResource("DividerBrush"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(7)
                };
                firstCell ??= cell;
                row.Cells.Add(cell);
            }
        }

        var paragraph = ContentEditor.CaretPosition.Paragraph;
        if (paragraph?.Parent is FlowDocument document)
        {
            document.Blocks.InsertAfter(paragraph, table);
        }
        else
        {
            ContentEditor.Document.Blocks.Add(table);
        }

        if (firstCell is not null)
        {
            ContentEditor.CaretPosition = firstCell.ContentStart;
        }
        ContentEditor.Focus();
        UpdatePreview();
    }

    private void Link_OnClick(object sender, RoutedEventArgs e)
    {
        var urlText = PromptDialog.Show(this, "Inserir hiperlink", "Endereço (https://...)");
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var uri))
        {
            if (urlText is not null)
            {
                MessageBox.Show("Informe um endereço completo iniciado por http:// ou https://.");
            }
            return;
        }

        if (ContentEditor.Selection.IsEmpty)
        {
            var label = PromptDialog.Show(this, "Texto do link", "Texto exibido", urlText);
            if (label is null)
            {
                return;
            }

            ContentEditor.Selection.Text = $"[{label}]({uri})";
        }
        else
        {
            try
            {
                _ = new Hyperlink(ContentEditor.Selection.Start, ContentEditor.Selection.End)
                {
                    NavigateUri = uri,
                    Foreground = (Brush)FindResource("AccentBrush")
                };
            }
            catch (InvalidOperationException)
            {
                var label = ContentEditor.Selection.Text;
                ContentEditor.Selection.Text = $"[{label}]({uri})";
            }
        }

        ContentEditor.Focus();
    }

    private void Image_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Adicionar imagem ao atalho",
            Filter = "Imagens|*.png;*.jpg;*.jpeg;*.gif;*.webp",
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        var info = new System.IO.FileInfo(picker.FileName);
        if (info.Length > 5 * 1024 * 1024)
        {
            MessageBox.Show(
                "Escolha uma imagem com até 5 MB.",
                "Imagem muito grande",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        System.IO.Directory.CreateDirectory(AppPaths.AssetsDirectory);
        var safeName = $"{Guid.NewGuid():N}{info.Extension.ToLowerInvariant()}";
        var destination = System.IO.Path.Combine(AppPaths.AssetsDirectory, safeName);
        System.IO.File.Copy(info.FullName, destination);
        ContentEditor.Selection.Text = $"![{System.IO.Path.GetFileNameWithoutExtension(info.Name)}](assets/{safeName})";
        ContentEditor.Focus();
        UpdatePreview();
    }

    private void VariableChip_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string token })
        {
            ContentEditor.Selection.Text = token;
            ContentEditor.Focus();
        }
    }

    private void ContentEditor_OnTextChanged(object sender, TextChangedEventArgs e) =>
        UpdatePreview();

    private void UpdatePreview()
    {
        if (PreviewDocument is null || ContentEditor is null || FormatBox is null)
        {
            return;
        }

        var format = FormatBox.SelectedIndex == 1 ? SnippetFormat.Markdown : SnippetFormat.Plain;
        var content = RichTextMarkdownConverter.Save(ContentEditor, format);
        var rendered = new TemplateEngine().Render(content, PreviewValues(content));
        RichTextMarkdownConverter.BuildPreview(PreviewDocument, rendered, format);
    }

    private static IReadOnlyDictionary<string, string> PreviewValues(string template)
    {
        var engine = new TemplateEngine();
        return engine.GetFillableFields(template)
            .ToDictionary(
                item => item.Name,
                item => item.DefaultValue ?? $"[{item.Name}]",
                StringComparer.CurrentCultureIgnoreCase);
    }

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
            ShortcutsView, QuickAccentView, CaptureView, StatisticsView, SettingsView, AboutView
        };
        foreach (var item in views)
        {
            item.Visibility = ReferenceEquals(item, view)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        var buttons = new[]
        {
            ShortcutsTabButton, QuickAccentTabButton, CaptureTabButton, StatisticsTabButton,
            SettingsTabButton, AboutTabButton
        };
        foreach (var button in buttons)
        {
            var selected = ReferenceEquals(button, selectedButton);
            button.Tag = selected ? "Selected" : null;
            button.Background = selected
                ? (Brush)FindResource("AccentSubtleBrush")
                : Brushes.Transparent;
            button.BorderBrush = selected
                ? (Brush)FindResource("FocusBrush")
                : Brushes.Transparent;
            button.Foreground = selected
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("MutedBrush");
        }

        if (ReferenceEquals(view, QuickAccentView))
        {
            UpdateQuickAccentPreviewSelection();
            _quickAccentPreviewTimer.Start();
        }
        else
        {
            _quickAccentPreviewTimer.Stop();
        }
    }

    private void RefreshStatistics()
    {
        var records = _usageService.Records;
        var total = records.Sum(item => item.Count);
        var characters = records.Sum(item => item.CharactersSaved);
        TotalExpansionsText.Text = total.ToString("N0");
        UsedSnippetsText.Text = records.Count(item => item.Count > 0).ToString("N0");
        CharactersSavedText.Text = characters.ToString("N0");
        TimeSavedText.Text = $"{Math.Ceiling(characters / 200d):N0} min";
        QuickAccentTotalText.Text = _usageService.QuickAccent.Count.ToString("N0");
        AverageCharactersText.Text = total == 0
            ? "0"
            : $"{characters / (double)total:N0}";

        var captures = _captureService.History;
        CaptureTotalText.Text = captures.Count.ToString("N0");
        CaptureRegionTotalText.Text = captures.Count(item =>
            item.Type.Equals("regiao", StringComparison.OrdinalIgnoreCase)).ToString("N0");
        CaptureMonitorTotalText.Text = captures.Count(item =>
            item.Type.Equals("monitor", StringComparison.OrdinalIgnoreCase)).ToString("N0");
        CaptureWindowTotalText.Text = captures.Count(item =>
            item.Type.Equals("janela", StringComparison.OrdinalIgnoreCase)).ToString("N0");

        StatisticsRankingPanel.Children.Clear();
        var ranking = _snippets
            .Select(item => new { Snippet = item, Usage = _usageService.For(item.Id) })
            .Where(item => item.Usage?.Count > 0)
            .OrderByDescending(item => item.Usage!.Count)
            .Take(8)
            .ToList();

        if (ranking.Count == 0)
        {
            StatisticsRankingPanel.Children.Add(new TextBlock
            {
                Text = "Use um atalho para iniciar as estatísticas.",
                Foreground = (Brush)FindResource("MutedBrush")
            });
        }
        else
        {
            foreach (var item in ranking)
            {
                StatisticsRankingPanel.Children.Add(new TextBlock
                {
                    Text = $"{item.Snippet.Trigger}  ·  {item.Usage!.Count:N0} uso(s)",
                    Margin = new Thickness(0, 5, 0, 5)
                });
            }
        }

        QuickAccentCharactersPanel.Children.Clear();
        var quickAccentRanking = _usageService.QuickAccent.Characters
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Key, StringComparer.CurrentCultureIgnoreCase)
            .Take(8)
            .ToList();
        QuickAccentFavoriteText.Text = quickAccentRanking.Count > 0
            ? quickAccentRanking[0].Key
            : "—";

        if (quickAccentRanking.Count == 0)
        {
            QuickAccentCharactersPanel.Children.Add(new TextBlock
            {
                Text = "Os caracteres usados aparecerão aqui.",
                Foreground = (Brush)FindResource("MutedBrush")
            });
        }
        else
        {
            foreach (var item in quickAccentRanking)
            {
                QuickAccentCharactersPanel.Children.Add(new Border
                {
                    Background = (Brush)FindResource("AccentSubtleBrush"),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 7, 7),
                    Child = new TextBlock
                    {
                        Text = $"{item.Key}  {item.Value:N0}×",
                        Foreground = (Brush)FindResource("AccentBrush"),
                        FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                        FontWeight = FontWeights.SemiBold
                    }
                });
            }
        }

        RefreshMostUsed();
    }

    private async void Settings_OnClick(object sender, RoutedEventArgs e)
    {
        var previousStart = _settings.StartWithWindows;
        _settings.CloseToTray = CloseToTrayCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _settings.ShowSuggestions = ShowSuggestionsCheckBox.IsChecked == true;
        _settings.CheckUpdatesOnStartup = CheckUpdatesCheckBox.IsChecked == true;

        try
        {
            if (previousStart != _settings.StartWithWindows)
            {
                StartupService.SetEnabled(_settings.StartWithWindows);
            }
            await _settingsStore.SaveAsync(_settings);

            if (!_settings.ShowSuggestions)
            {
                _suggestionWindow.Hide();
            }
        }
        catch (Exception exception)
        {
            _settings.StartWithWindows = previousStart;
            StartWithWindowsCheckBox.IsChecked = previousStart;
            MessageBox.Show(
                exception.Message,
                "Não foi possível salvar a configuração",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void ImportSnippets_OnClick(object sender, RoutedEventArgs e)
    {
        var source = ImportSourceBox.SelectedItem is ComboBoxItem { Tag: string sourceTag } &&
                     Enum.TryParse<SnippetImportSource>(sourceTag, out var parsed)
            ? parsed
            : SnippetImportSource.SlashDesk;
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Importar atalhos do {ImportSourceName(source)}",
            Filter = source switch
            {
                SnippetImportSource.SlashDesk => "Atalhos do SlashDesk|*.md",
                SnippetImportSource.TextBlaze => "Exportação do Text Blaze|*.json",
                SnippetImportSource.Espanso => "Configuração do Espanso|*.yml;*.yaml",
                _ => "Todos os arquivos|*.*"
            },
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var result = await _snippetImportService.ImportAsync(picker.FileName, source);
            if (result.Snippets.Count == 0)
            {
                MessageBox.Show(
                    "Nenhum atalho compatível foi encontrado no arquivo.",
                    "Importação concluída",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var conflicts = result.Snippets.Count(incoming =>
                _snippets.Any(current =>
                    current.Trigger.Equals(incoming.Trigger, StringComparison.OrdinalIgnoreCase)));
            var mode = MessageBox.Show(
                $"{result.Snippets.Count} atalho(s) compatível(is) encontrado(s)." +
                (conflicts == 0
                    ? "\n\nDeseja adicionar esses atalhos aos atuais?"
                    : $"\n\n{conflicts} conflito(s) de comando. Sim substitui os conflitos; Não mantém os atuais e importa os demais."),
                $"Importar do {ImportSourceName(source)}",
                conflicts == 0 ? MessageBoxButton.YesNo : MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (mode is MessageBoxResult.Cancel or MessageBoxResult.None ||
                (conflicts == 0 && mode == MessageBoxResult.No))
            {
                return;
            }

            _backupService.CreateManualSnapshot();
            var replaceConflicts = mode == MessageBoxResult.Yes;
            var incomingTriggers = result.Snippets
                .Select(item => item.Trigger)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var merged = _snippets
                .Where(current => !replaceConflicts || !incomingTriggers.Contains(current.Trigger))
                .ToList();
            foreach (var incoming in result.Snippets)
            {
                if (replaceConflicts ||
                    !merged.Any(current =>
                        current.Trigger.Equals(incoming.Trigger, StringComparison.OrdinalIgnoreCase)))
                {
                    merged.Add(incoming);
                }
            }

            await _repository.SaveAsync(merged);
            ReplaceList(merged);
            _keyboardHook.UpdateSnippets(_snippets);
            RefreshStatistics();
            var warningSummary = result.Warnings.Count == 0
                ? string.Empty
                : $"\n\n{result.Warnings.Count} aviso(s) de compatibilidade. Os primeiros:\n• " +
                  string.Join("\n• ", result.Warnings.Take(4));
            MessageBox.Show(
                $"{result.Snippets.Count} atalho(s) processado(s). O snippets.md foi atualizado e o estado anterior foi salvo em backup.{warningSummary}",
                "Importação concluída",
                MessageBoxButton.OK,
                result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            StatusText.Text = $"Importação do {ImportSourceName(source)} concluída";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Não foi possível importar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void CreateBackup_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _backupService.CreateManualSnapshot();
            RefreshBackupSummary();
            StatusText.Text = $"Backup criado: {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Não foi possível criar o backup",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void RestoreBackup_OnClick(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restaurar backup do SlashDesk",
            InitialDirectory = AppPaths.BackupsDirectory,
            Filter = "Backup do SlashDesk|SlashDesk-backup-*.zip|Arquivo ZIP|*.zip",
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true ||
            MessageBox.Show(
                "Restaurar este backup substituirá atalhos, preferências e estatísticas atuais. Um backup de segurança será criado antes de continuar.",
                "Restaurar backup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _backupService.RestoreSnapshot(picker.FileName);
            _settings = await _settingsStore.LoadAsync();
            ThemeService.Apply(_settings.Theme);
            ReplaceList(await _repository.LoadAsync());
            _keyboardHook.UpdateSnippets(_snippets);
            await _usageService.LoadAsync();
            RefreshStatistics();
            RefreshBackupSummary();
            MessageBox.Show(
                "Backup restaurado. As preferências completas serão aplicadas na próxima abertura do SlashDesk.",
                "Restauração concluída",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Não foi possível restaurar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenBackupFolder_OnClick(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.BackupsDirectory);
        Process.Start(new ProcessStartInfo(AppPaths.BackupsDirectory) { UseShellExecute = true });
    }

    private void RefreshBackupSummary()
    {
        if (BackupSummaryText is null)
        {
            return;
        }

        var snapshots = _backupService.ListSnapshots();
        BackupSummaryText.Text = snapshots.Count == 0
            ? "Nenhum backup criado ainda."
            : $"{snapshots.Count} backup(s) · último em {snapshots[0].LastWriteTime:g}";
    }

    private static string ImportSourceName(SnippetImportSource source) => source switch
    {
        SnippetImportSource.SlashDesk => "SlashDesk",
        SnippetImportSource.TextBlaze => "Text Blaze",
        SnippetImportSource.Espanso => "Espanso",
        _ => source.ToString()
    };

    private async void ThemeBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || ThemeBox.SelectedItem is not ComboBoxItem { Tag: string theme })
        {
            return;
        }

        _settings.Theme = theme;
        ThemeService.Apply(theme);
        await _settingsStore.SaveAsync(_settings);
        ShowView(SettingsView, SettingsTabButton);
    }

    private async void QuickAccentSettings_OnChanged(object sender, RoutedEventArgs e)
    {
        if (!_initialized)
        {
            return;
        }

        await SaveQuickAccentSettingsAsync();
    }

    private async void QuickAccentDelaySlider_OnValueChanged(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        if (QuickAccentDelayBox is null)
        {
            return;
        }

        QuickAccentDelayBox.Text = ((int)Math.Round(e.NewValue)).ToString();
        if (_initialized)
        {
            await SaveQuickAccentSettingsAsync();
        }
    }

    private void QuickAccentPreviewChoice_OnMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: string value } &&
            int.TryParse(value, out var index))
        {
            _quickAccentPreviewIndex = Math.Clamp(index, 0, 7);
            UpdateQuickAccentPreviewSelection();
            _quickAccentPreviewTimer.Stop();
            _quickAccentPreviewTimer.Start();
        }
    }

    private void UpdateQuickAccentPreviewSelection()
    {
        if (QuickAccentPreviewChoice0 is null)
        {
            return;
        }

        var choices = new[]
        {
            QuickAccentPreviewChoice0,
            QuickAccentPreviewChoice1,
            QuickAccentPreviewChoice2,
            QuickAccentPreviewChoice3,
            QuickAccentPreviewChoice4,
            QuickAccentPreviewChoice5,
            QuickAccentPreviewChoice6,
            QuickAccentPreviewChoice7
        };
        for (var index = 0; index < choices.Length; index++)
        {
            var selected = index == _quickAccentPreviewIndex;
            choices[index].Background = (Brush)FindResource(
                selected ? "AccentSubtleBrush" : "ControlBrush");
            choices[index].BorderBrush = (Brush)FindResource(
                selected ? "AccentBrush" : "DividerBrush");
            if (choices[index].Child is TextBlock text)
            {
                text.Foreground = (Brush)FindResource(
                    selected ? "AccentBrush" : "InkBrush");
            }
        }
    }

    private async void QuickAccentCharacterSet_OnChanged(
        object sender,
        RoutedEventArgs e)
    {
        if (!_initialized || _updatingQuickAccentSets)
        {
            return;
        }

        if (SelectedQuickAccentCharacterSets().Count == 0)
        {
            _updatingQuickAccentSets = true;
            QuickAccentPortugueseCheckBox.IsChecked = true;
            _updatingQuickAccentSets = false;
        }

        await SaveQuickAccentSettingsAsync();
    }

    private async void QuickAccentPortugueseOnly_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        ApplyQuickAccentCharacterSetSelection(["PortugueseBrazil"]);
        if (_initialized)
        {
            await SaveQuickAccentSettingsAsync();
        }
    }

    private async void QuickAccentSelectAll_OnClick(
        object sender,
        RoutedEventArgs e)
    {
        ApplyQuickAccentCharacterSetSelection(
            QuickAccentCharacterSetBoxes()
                .Select(item => item.Tag?.ToString() ?? string.Empty));
        if (_initialized)
        {
            await SaveQuickAccentSettingsAsync();
        }
    }

    private async Task SaveQuickAccentSettingsAsync()
    {
        _settings.QuickAccentEnabled = QuickAccentEnabledCheckBox.IsChecked == true;
        _settings.QuickAccentActivationKey = SelectedTag(QuickAccentActivationBox, "Space");
        _settings.QuickAccentToolbarPosition = SelectedTag(
            QuickAccentPositionBox,
            "BottomCenter");
        _settings.QuickAccentShowUnicode = QuickAccentUnicodeCheckBox.IsChecked == true;
        _settings.QuickAccentSortByUsage = QuickAccentSortCheckBox.IsChecked == true;
        _settings.QuickAccentInputDelayMs = int.TryParse(
            QuickAccentDelayBox.Text,
            out var delay)
            ? Math.Clamp(delay, 0, 2000)
            : 200;
        if (QuickAccentDelaySlider is not null)
        {
            QuickAccentDelaySlider.Value = Math.Clamp(
                _settings.QuickAccentInputDelayMs,
                (int)QuickAccentDelaySlider.Minimum,
                (int)QuickAccentDelaySlider.Maximum);
        }
        _settings.QuickAccentExcludedApps = QuickAccentExcludedAppsBox.Text;
        _settings.QuickAccentCharacterSets = SelectedQuickAccentCharacterSets();
        ApplyQuickAccentSettings();
        await _settingsStore.SaveAsync(_settings);
    }

    private void ApplyQuickAccentSettings()
    {
        _quickAccentService.Enabled = _settings.QuickAccentEnabled;
        _quickAccentService.ActivationKey = _settings.QuickAccentActivationKey;
        _quickAccentService.SortByUsage = _settings.QuickAccentSortByUsage;
        _quickAccentService.InputDelayMs = _settings.QuickAccentInputDelayMs;
        _quickAccentService.ExcludedApps = _settings.QuickAccentExcludedApps;
        _quickAccentService.SetCharacterSets(_settings.QuickAccentCharacterSets);
        _quickAccentService.SetUsage(_usageService.QuickAccentCharacterCounts());
        UpdateQuickAccentCharacterSetPreview();
        if (!_settings.QuickAccentEnabled)
        {
            _quickAccentWindow.Hide();
        }
    }

    private void ApplyQuickAccentCharacterSetSelection(IEnumerable<string>? sets)
    {
        var selected = new HashSet<string>(
            sets ?? [],
            StringComparer.OrdinalIgnoreCase);
        if (selected.Count == 0)
        {
            selected.Add("PortugueseBrazil");
        }

        _updatingQuickAccentSets = true;
        foreach (var checkBox in QuickAccentCharacterSetBoxes())
        {
            checkBox.IsChecked =
                checkBox.Tag is string tag && selected.Contains(tag);
        }
        if (QuickAccentCharacterSetBoxes().All(item => item.IsChecked != true))
        {
            QuickAccentPortugueseCheckBox.IsChecked = true;
        }
        _updatingQuickAccentSets = false;
        UpdateQuickAccentCharacterSetPreview();
    }

    private List<string> SelectedQuickAccentCharacterSets() =>
        QuickAccentCharacterSetBoxes()
            .Where(item => item.IsChecked == true)
            .Select(item => item.Tag?.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

    private CheckBox[] QuickAccentCharacterSetBoxes() =>
    [
        QuickAccentPortugueseCheckBox,
        QuickAccentSpanishCheckBox,
        QuickAccentFrenchCheckBox,
        QuickAccentGermanCheckBox,
        QuickAccentItalianCheckBox,
        QuickAccentNordicCheckBox,
        QuickAccentCentralEuropeanCheckBox,
        QuickAccentCurrencyCheckBox,
        QuickAccentSpecialCheckBox
    ];

    private void UpdateQuickAccentCharacterSetPreview()
    {
        if (QuickAccentSetSummaryText is null ||
            QuickAccentCharactersPreviewText is null)
        {
            return;
        }

        var selected = SelectedQuickAccentCharacterSets();
        QuickAccentSetSummaryText.Text = selected.Count == 1 &&
                                         selected[0] == "PortugueseBrazil"
            ? "Somente acentuação comum do PT-BR"
            : $"{selected.Count} conjunto(s) selecionado(s)";
        var preview = QuickAccentService.PreviewCharacters(selected);
        QuickAccentCharactersPreviewText.Text = string.IsNullOrEmpty(preview)
            ? "Nenhum caractere disponível"
            : string.Join("  ", preview);
    }

    private static string SelectedTag(ComboBox box, string fallback) =>
        box.SelectedItem is ComboBoxItem { Tag: string tag } ? tag : fallback;

    private void InitializeRecordingPresetControls()
    {
        AddPresetItems(RecordingQualityBox, RecordingPresetCatalog.Mp4Quality,
            item => item.Name, item => item.Value);
        AddPresetItems(GifFpsBox, RecordingPresetCatalog.GifFps,
            item => $"{item.Value} FPS — {item.Name}", item => item.Value.ToString());
        AddPresetItems(GifQualityBox, RecordingPresetCatalog.GifQuality,
            item => item.Name, item => item.Value.ToString());
    }

    private static void AddPresetItems<T>(
        ComboBox box,
        IReadOnlyList<RecordingPreset<T>> presets,
        Func<RecordingPreset<T>, string> content,
        Func<RecordingPreset<T>, string> tag)
    {
        box.Items.Clear();
        foreach (var preset in presets)
        {
            box.Items.Add(new ComboBoxItem
            {
                Content = content(preset),
                Tag = tag(preset),
                ToolTip = preset.Description
            });
        }
    }

    private void RecordingPreset_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePresetDescription(RecordingQualityBox, RecordingQualityDescriptionText);
        UpdatePresetDescription(GifFpsBox, GifFpsDescriptionText);
        UpdatePresetDescription(GifQualityBox, GifQualityDescriptionText);
    }

    private static void UpdatePresetDescription(ComboBox box, TextBlock description)
    {
        var text = box.SelectedItem is ComboBoxItem { ToolTip: string value }
            ? value
            : string.Empty;
        description.Text = text;
        box.ToolTip = text;
    }

    private static void SelectComboByTag(ComboBox box, string value)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
        {
            if (item.Tag is string tag &&
                tag.Equals(value, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
        box.SelectedIndex = 0;
    }

    private void LoadCaptureSettings()
    {
        var capture = _settings.Capture ??= new CaptureSettings();
        capture.Recording ??= new RecordingSettings();
        RecordingPresetCatalog.Normalize(capture.Recording);
        CaptureMonitorShortcutBox.Text = capture.ActiveMonitorShortcut;
        CaptureRegionShortcutBox.Text = capture.RegionShortcut;
        CaptureWindowShortcutBox.Text = capture.WindowShortcut;
        CaptureDirectoryBox.Text = capture.OutputDirectoryTemplate;
        CaptureFileNameBox.Text = capture.FileNameTemplate;
        SelectComboByTag(CaptureFormatBox, capture.ImageFormat);
        CaptureQualityBox.Text = capture.JpegQuality.ToString();
        CaptureClipboardCheckBox.IsChecked = capture.CopyToClipboard;
        CaptureAutoSaveCheckBox.IsChecked = capture.SaveAutomatically;
        CaptureCursorCheckBox.IsChecked = capture.IncludeCursor;
        CaptureEditorCheckBox.IsChecked = capture.OpenEditorForMonitorAndWindow;
        SelectComboByTag(CaptureDelayBox, capture.DelaySeconds.ToString());
        SelectComboByTag(CaptureRetentionBox, capture.HistoryRetentionDays.ToString());
        SelectComboByTag(RecordingTargetBox, "Monitor");
        SelectComboByTag(RecordingFpsBox, capture.Recording.VideoFps.ToString());
        SelectComboByTag(RecordingQualityBox, capture.Recording.VideoQuality);
        RecordingCursorCheckBox.IsChecked = capture.Recording.IncludeCursor;
        SelectComboByTag(GifFpsBox, capture.Recording.GifFps.ToString());
        SelectComboByTag(GifQualityBox, capture.Recording.GifQuality.ToString());
        RecordingPreset_OnChanged(this, null!);
        SelectComboByTag(CaptureHistoryFilterBox, "all");
        CaptureQualityBox.IsEnabled =
            capture.ImageFormat.Equals("JPEG", StringComparison.OrdinalIgnoreCase);
    }

    private async void SaveCaptureSettings_OnClick(object sender, RoutedEventArgs e)
    {
        if (!TryReadCaptureSettings(out var error))
        {
            MessageBox.Show(
                error,
                "Configuração de captura",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        await _settingsStore.SaveAsync(_settings);
        ConfigureCaptureShortcuts();
        StatusText.Text = "Configurações de captura salvas";
    }

    private bool TryReadCaptureSettings(out string error)
    {
        error = string.Empty;
        var shortcuts = new[]
        {
            CaptureMonitorShortcutBox.Text.Trim(),
            CaptureRegionShortcutBox.Text.Trim(),
            CaptureWindowShortcutBox.Text.Trim()
        };
        if (shortcuts.Any(item => !GlobalCaptureShortcutService.IsValid(item)))
        {
            error = "Clique no campo e pressione uma tecla, roda ou botão do mouse. " +
                    "A roda sempre exige Ctrl, Alt, Shift ou Win; cliques esquerdo e direito não são aceitos.";
            return false;
        }
        if (shortcuts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != shortcuts.Length)
        {
            error = "Cada ação precisa ter um atalho diferente.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(CaptureDirectoryBox.Text) ||
            string.IsNullOrWhiteSpace(CaptureFileNameBox.Text))
        {
            error = "Informe a pasta e o modelo de nome do arquivo.";
            return false;
        }
        var gifFps = ParseSelectedInt(GifFpsBox, 10);
        var gifQuality = ParseSelectedInt(GifQualityBox, 128);
        if (!RecordingPresetCatalog.GifFps.Any(item => item.Value == gifFps) ||
            !RecordingPresetCatalog.GifQuality.Any(item => item.Value == gifQuality))
        {
            error = "Selecione um preset disponível de FPS e qualidade do GIF.";
            return false;
        }

        var legacyGifDuration = _settings.Capture.Recording.GifDurationSeconds;
        var legacyGifWidth = _settings.Capture.Recording.GifWidth;

        var quality = int.TryParse(CaptureQualityBox.Text, out var parsedQuality)
            ? Math.Clamp(parsedQuality, 1, 100)
            : 90;
        _settings.Capture = new CaptureSettings
        {
            ActiveMonitorShortcut = shortcuts[0],
            RegionShortcut = shortcuts[1],
            WindowShortcut = shortcuts[2],
            OutputDirectoryTemplate = CaptureDirectoryBox.Text.Trim(),
            FileNameTemplate = CaptureFileNameBox.Text.Trim(),
            ImageFormat = SelectedTag(CaptureFormatBox, "PNG"),
            JpegQuality = quality,
            CopyToClipboard = CaptureClipboardCheckBox.IsChecked == true,
            SaveAutomatically = CaptureAutoSaveCheckBox.IsChecked == true,
            HideSlashDeskDuringCapture = true,
            IncludeCursor = CaptureCursorCheckBox.IsChecked == true,
            OpenEditorForMonitorAndWindow = CaptureEditorCheckBox.IsChecked == true,
            DelaySeconds = ParseSelectedInt(CaptureDelayBox, 0),
            HistoryRetentionDays = ParseSelectedInt(CaptureRetentionBox, 90),
            Recording = new RecordingSettings
            {
                VideoFps = ParseSelectedInt(RecordingFpsBox, 30),
                VideoQuality = SelectedTag(RecordingQualityBox, "Alta"),
                IncludeCursor = RecordingCursorCheckBox.IsChecked == true,
                GifFps = gifFps,
                GifDurationSeconds = legacyGifDuration,
                GifWidth = legacyGifWidth,
                GifQuality = gifQuality
            }
        };
        CaptureQualityBox.Text = quality.ToString();
        return true;
    }

    private void ConfigureCaptureShortcuts()
    {
        var capture = _settings.Capture;
        var errors = _captureShortcuts.Configure(
            this,
            capture.ActiveMonitorShortcut,
            capture.RegionShortcut,
            capture.WindowShortcut);
        CaptureShortcutStatusText.Text = errors.Count == 0
            ? "● Atalhos ativos"
            : string.Join(Environment.NewLine, errors);
        CaptureShortcutStatusText.Foreground = errors.Count == 0
            ? (Brush)FindResource("SuccessBrush")
            : (Brush)FindResource("MutedBrush");
    }

    private void CaptureShortcuts_OnTriggered(
        object? sender,
        CaptureShortcutEventArgs e) =>
        _ = RunCaptureAsync(e.Action, invokedByShortcut: true);

    private void CaptureActiveMonitor_OnClick(object sender, RoutedEventArgs e) =>
        _ = RunCaptureAsync(CaptureShortcutAction.ActiveMonitor, invokedByShortcut: false);

    private void CaptureRegion_OnClick(object sender, RoutedEventArgs e) =>
        _ = RunCaptureAsync(CaptureShortcutAction.Region, invokedByShortcut: false);

    private void CaptureWindow_OnClick(object sender, RoutedEventArgs e) =>
        _ = RunCaptureAsync(CaptureShortcutAction.Window, invokedByShortcut: false);

    private async void CaptureScrolling_OnClick(object sender, RoutedEventArgs e)
    {
        var target = _captureService.WindowUnderCursorTarget();
        if (target is null)
        {
            MessageBox.Show(
                "Posicione o cursor sobre a janela que será capturada.",
                "Captura com rolagem",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        try
        {
            Hide();
            await Task.Delay(180);
            var result = await _captureService.CaptureScrollingAsync(
                target.WindowHandle,
                target.Bounds,
                _settings.Capture,
                owner: null);
            ShowFromTray();
            if (result is not null)
            {
                StatusText.Text = $"Captura com rolagem salva: {Path.GetFileName(result.FilePath)}";
                RefreshCaptureHistory();
            }
        }
        catch (Exception exception)
        {
            ShowFromTray();
            MessageBox.Show(
                exception.Message,
                "Captura com rolagem",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task RunCaptureAsync(
        CaptureShortcutAction action,
        bool invokedByShortcut)
    {
        try
        {
            await WaitForCaptureDelayAsync();
            System.Drawing.Rectangle? bounds = null;
            System.Drawing.Bitmap? editedRegion = null;
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

            var wasVisible = IsVisible;
            var shouldHide =
                _settings.Capture.HideSlashDeskDuringCapture &&
                wasVisible &&
                !invokedByShortcut;
            if (shouldHide)
            {
                Hide();
                await Task.Delay(180);
            }

            if (action == CaptureShortcutAction.Region)
            {
                editedRegion = _captureService.SelectAndEditRegion(
                    null,
                    _settings.Capture.IncludeCursor);
            }
            else if (action == CaptureShortcutAction.Window)
            {
                bounds = _captureService.WindowUnderCursorBounds();
            }

            CaptureRecord? result = null;
            if (editedRegion is not null)
            {
                using (editedRegion)
                {
                    result = await _captureService.ProcessEditedRegionAsync(
                        editedRegion,
                        type,
                        _settings.Capture);
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
                    ? $"Captura de {type} copiada"
                    : $"Captura salva: {Path.GetFileName(result.FilePath)}";
                RefreshCaptureHistory();
                _trayIcon?.ShowBalloonTip(
                    1500,
                    "Captura concluída",
                    string.IsNullOrWhiteSpace(result.FilePath)
                        ? "Imagem copiada para o clipboard."
                        : result.FilePath,
                    Forms.ToolTipIcon.Info);
            }

            if (shouldHide)
            {
                ShowFromTray();
            }
        }
        catch (Exception exception)
        {
            ShowFromTray();
            MessageBox.Show(
                exception.Message,
                "Não foi possível capturar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
            StatusText.Text = $"Captura em {remaining}s…";
            await Task.Delay(1000);
        }
    }

    private async void StartMp4Recording_OnClick(object sender, RoutedEventArgs e)
    {
        if (_recordingControl is not null || _recordingService?.IsRecording == true)
        {
            _recordingControl?.Activate();
            return;
        }
        if (!TryReadCaptureSettings(out var settingsError))
        {
            MessageBox.Show(settingsError, "Gravação", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await _settingsStore.SaveAsync(_settings);

        try
        {
            RecordingTarget? target;
            if (SelectedTag(RecordingTargetBox, "Monitor").Equals(
                    "Window",
                    StringComparison.OrdinalIgnoreCase))
            {
                Hide();
                await Task.Delay(250);
                target = _captureService.WindowUnderCursorTarget();
            }
            else
            {
                target = ResolveRecordingTarget("Selecione a região do vídeo");
            }
            if (target is null)
            {
                ShowFromTray();
                return;
            }

            if (IsVisible)
            {
                Hide();
            }
            await Task.Delay(180);
            _recordingService = new ScreenRecordingService();
            _recordingService.RecordingFailed += (_, message) =>
                _ = Dispatcher.BeginInvoke(() => StatusText.Text = message);
            var completion = _recordingService.StartAsync(
                target,
                _settings.Capture,
                _settings.Capture.Recording);
            _recordingControl = new RecordingControlWindow(_recordingService, "MP4");
            _recordingControl.Show();
            var path = await completion;
            var elapsed = _recordingService.Elapsed;
            var completedRecordingId = _recordingService.RecordingId.ToString("N");
            _recordingControl.Close();
            _recordingControl = null;
            _recordingService.Dispose();
            _recordingService = null;
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
            ShowFromTray();
            StatusText.Text = $"Vídeo salvo: {Path.GetFileName(path)}";
            RefreshCaptureHistory();
        }
        catch (Exception exception)
        {
            var failedRecordingId = _recordingService?.RecordingId.ToString("N") ?? "unknown";
            _recordingControl?.Close();
            _recordingControl = null;
            _recordingService?.Dispose();
            _recordingService = null;
            ShowFromTray();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", failedRecordingId), ("media", "MP4"), ("result", "failure"));
            MessageBox.Show(
                exception.Message +
                "\n\nO SlashDesk usa H.264/Media Foundation. Em edições N/KN, instale o Media Feature Pack do Windows." +
                $"\nLogs: {AppPaths.LogsDirectory}",
                "Não foi possível gravar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async void StartGifRecording_OnClick(object sender, RoutedEventArgs e)
    {
        if (_recordingControl is not null)
        {
            _recordingControl.Activate();
            return;
        }
        if (!TryReadCaptureSettings(out var settingsError))
        {
            MessageBox.Show(settingsError, "GIF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await _settingsStore.SaveAsync(_settings);
        var target = _captureService.SelectRecordingRegion(this, "Selecione a região do GIF");
        if (target is null)
        {
            return;
        }

        try
        {
            Hide();
            await Task.Delay(180);
            _gifRecordingSession = _gifRecordingService.StartRecording(
                target.Bounds,
                _settings.Capture.Recording);
            _recordingControl = new RecordingControlWindow(_gifRecordingSession, "GIF");
            _recordingControl.Show();
            using var recording = await _gifRecordingSession.Completion;
            var completedRecordingId = _gifRecordingSession.RecordingId.ToString("N");
            _recordingControl.Close();
            _recordingControl = null;
            _gifRecordingSession.Dispose();
            _gifRecordingSession = null;
            ShowFromTray();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", completedRecordingId), ("media", "GIF"), ("result", "captured"));
            if (recording.Metrics?.DroppedFrames > 0)
            {
                MessageBox.Show(
                    $"O pipeline descartou {recording.Metrics.DroppedFrames:N0} quadro(s) por sobrecarga. " +
                    "O tempo foi preservado no GIF e o evento foi registrado nos logs.",
                    "GIF finalizado com sobrecarga",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            var preview = new GifPreviewWindow(recording) { Owner = this };
            if (preview.ShowDialog() != true)
            {
                StatusText.Text = "GIF descartado";
                return;
            }
            var path = await _gifRecordingService.SaveAsync(
                recording,
                _settings.Capture,
                target.Type,
                _settings.Capture.Recording.GifQuality);
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
            StatusText.Text = $"GIF salvo: {Path.GetFileName(path)}";
            RefreshCaptureHistory();
        }
        catch (OperationCanceledException)
        {
            var cancelledRecordingId = _gifRecordingSession?.RecordingId.ToString("N") ?? "unknown";
            _recordingControl?.Close();
            _recordingControl = null;
            _gifRecordingSession?.Dispose();
            _gifRecordingSession = null;
            ShowFromTray();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", cancelledRecordingId), ("media", "GIF"), ("result", "cancelled"));
            StatusText.Text = "Gravação de GIF cancelada";
        }
        catch (Exception exception)
        {
            var failedRecordingId = _gifRecordingSession?.RecordingId.ToString("N") ?? "unknown";
            _recordingControl?.Close();
            _recordingControl = null;
            _gifRecordingSession?.Dispose();
            _gifRecordingSession = null;
            ShowFromTray();
            AppDiagnosticLog.Write("recording.interface-restored",
                ("recordingId", failedRecordingId), ("media", "GIF"), ("result", "failure"));
            MessageBox.Show(
                exception.Message,
                "Não foi possível criar o GIF",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private RecordingTarget? ResolveRecordingTarget(string regionPurpose)
    {
        var selected = SelectedTag(RecordingTargetBox, "Monitor");
        if (selected.Equals("Region", StringComparison.OrdinalIgnoreCase))
        {
            return _captureService.SelectRecordingRegion(this, regionPurpose);
        }
        return _captureService.ActiveMonitorTarget();
    }

    private static int ParseSelectedInt(ComboBox box, int fallback) =>
        int.TryParse(SelectedTag(box, fallback.ToString()), out var value)
            ? value
            : fallback;

    private void CaptureFormat_OnChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CaptureQualityBox is not null && CaptureFormatBox is not null)
        {
            CaptureQualityBox.IsEnabled =
                SelectedTag(CaptureFormatBox, "PNG").Equals(
                    "JPEG",
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    private void RefreshCaptureHistory()
    {
        if (CaptureHistoryPanel is null)
        {
            return;
        }

        CaptureHistoryPanel.Children.Clear();
        CapturePreviewImage.Source = null;
        CapturePreviewEmptyPanel.Visibility = Visibility.Visible;
        CapturePreviewDetailsText.Text = "Nenhuma captura realizada";

        var mostRecent = _captureService.History.FirstOrDefault();
        if (mostRecent is not null)
        {
            var mostRecentPath = _captureService.ResolveFilePath(mostRecent);
            CapturePreviewDetailsText.Text =
                $"{mostRecent.CreatedAt:dd/MM/yyyy HH:mm}  ·  {mostRecent.Type}  ·  " +
                $"{mostRecent.Width}×{mostRecent.Height}";
            if (!mostRecent.MediaKind.Equals("video", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(mostRecentPath) &&
                File.Exists(mostRecentPath))
            {
                try
                {
                    using var stream = File.Open(
                        mostRecentPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite);
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    CapturePreviewImage.Source = image;
                    CapturePreviewEmptyPanel.Visibility = Visibility.Collapsed;
                }
                catch (IOException)
                {
                    // O histórico continua disponível mesmo se a miniatura não puder ser aberta.
                }
            }
        }

        var filter = CaptureHistoryFilterBox is null
            ? "all"
            : SelectedTag(CaptureHistoryFilterBox, "all");
        var filtered = _captureService.History.Where(item =>
            filter.Equals("all", StringComparison.OrdinalIgnoreCase) ||
            filter.Equals("video", StringComparison.OrdinalIgnoreCase) &&
            item.MediaKind.Equals("video", StringComparison.OrdinalIgnoreCase) ||
            filter.Equals("gif", StringComparison.OrdinalIgnoreCase) &&
            item.MediaKind.Equals("gif", StringComparison.OrdinalIgnoreCase) ||
            item.Type.Equals(filter, StringComparison.OrdinalIgnoreCase));
        foreach (var item in filtered.Take(40))
        {
            var resolvedPath = _captureService.ResolveFilePath(item);
            var file = string.IsNullOrWhiteSpace(resolvedPath)
                ? "Somente clipboard"
                : Path.GetFileName(resolvedPath);
            var row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 7)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var duration = item.DurationSeconds > 0
                ? $" · {TimeSpan.FromSeconds(item.DurationSeconds):mm\\:ss}"
                : string.Empty;
            row.Children.Add(new TextBlock
            {
                Text = $"{item.CreatedAt:dd/MM HH:mm} · {item.Type} · " +
                       $"{item.MediaKind} · {item.Width}×{item.Height}{duration} · {file}",
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)FindResource("MutedBrush")
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(HistoryButton("Abrir", item, OpenHistoryItem_OnClick));
            actions.Children.Add(HistoryButton("Copiar", item, CopyHistoryItem_OnClick));
            if (item.MediaKind.Equals("image", StringComparison.OrdinalIgnoreCase))
            {
                actions.Children.Add(HistoryButton("Editar", item, EditHistoryItem_OnClick));
            }
            actions.Children.Add(HistoryButton("Excluir", item, DeleteHistoryItem_OnClick));
            Grid.SetColumn(actions, 1);
            row.Children.Add(actions);
            CaptureHistoryPanel.Children.Add(row);
        }
        if (CaptureHistoryPanel.Children.Count == 0)
        {
            CaptureHistoryPanel.Children.Add(new TextBlock
            {
                Text = "As últimas capturas aparecerão aqui.",
                Foreground = (Brush)FindResource("MutedBrush")
            });
        }
        CaptureHistoryStatusText.Text =
            $"{filtered.Count():N0} de {_captureService.History.Count:N0} item(ns)";
    }

    private static Button HistoryButton(
        string label,
        CaptureRecord record,
        RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = label,
            Tag = record,
            MinWidth = 62,
            Height = 32,
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(8, 3, 8, 3)
        };
        button.Click += handler;
        return button;
    }

    private void CaptureHistoryFilter_OnChanged(object sender, SelectionChangedEventArgs e) =>
        RefreshCaptureHistory();

    private void OpenHistoryItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CaptureRecord record })
        {
            return;
        }
        var path = _captureService.ResolveFilePath(record);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show(
                "O arquivo não está mais disponível.",
                "Histórico",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void CopyHistoryItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CaptureRecord record })
        {
            return;
        }
        try
        {
            var path = _captureService.ResolveFilePath(record);
            CaptureService.CopyFileToClipboard(path);
            StatusText.Text = $"Arquivo copiado: {Path.GetFileName(path)}";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Histórico",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private async void EditHistoryItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CaptureRecord record })
        {
            return;
        }
        if (await _captureService.EditExistingAsync(record.Id, _settings.Capture, this))
        {
            StatusText.Text =
                $"Captura atualizada: {Path.GetFileName(_captureService.ResolveFilePath(record))}";
            RefreshCaptureHistory();
        }
    }

    private async void DeleteHistoryItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CaptureRecord record })
        {
            return;
        }
        var choice = MessageBox.Show(
            "Sim: excluir também o arquivo local.\nNão: remover somente do histórico.",
            "Excluir captura",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);
        if (choice == MessageBoxResult.Cancel)
        {
            return;
        }
        await _captureService.DeleteAsync(
            record.Id,
            deleteFile: choice == MessageBoxResult.Yes);
        RefreshCaptureHistory();
    }

    private async void CleanCaptureHistory_OnClick(object sender, RoutedEventArgs e)
    {
        var days = ParseSelectedInt(CaptureRetentionBox, 90);
        if (days <= 0)
        {
            CaptureHistoryStatusText.Text = "Limpeza automática desativada";
            return;
        }
        var count = await _captureService.CleanOlderThanAsync(days, deleteFiles: false);
        CaptureHistoryStatusText.Text = $"{count:N0} entrada(s) antiga(s) removida(s)";
        RefreshCaptureHistory();
    }

    private void OpenCaptureFolder_OnClick(object sender, RoutedEventArgs e)
    {
        var directory = CaptureService.ResolveDirectoryTemplate(
            Environment.ExpandEnvironmentVariables(_settings.Capture.OutputDirectoryTemplate),
            DateTimeOffset.Now);
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
    }

    private async void CheckUpdates_OnClick(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        try
        {
            var result = await _updateService.CheckAsync(force: true);
            UpdateUpdateDisplay(await _updateService.LoadStateAsync());
            if (result.UpdateAvailable && result.Release is not null)
            {
                await OfferUpdateAsync(result);
            }
            else
            {
                MessageBox.Show(
                    result.Message,
                    "Atualizações do SlashDesk",
                    MessageBoxButton.OK,
                    result.Status == UpdateCheckStatus.Offline
                        ? MessageBoxImage.Information
                        : MessageBoxImage.None);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Não foi possível consultar o GitHub.\n\n{exception.Message}",
                "Atualizações do SlashDesk",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }

    private async Task CheckUpdatesSilentlyAsync()
    {
        try
        {
            var result = await _updateService.CheckAsync();
            UpdateUpdateDisplay(await _updateService.LoadStateAsync());
            if (result.UpdateAvailable && result.Release is not null)
            {
                StatusText.Text = result.Message;
                _trayIcon?.ShowBalloonTip(
                    2500,
                    "Atualização disponível",
                    result.Message,
                    Forms.ToolTipIcon.Info);
                await OfferUpdateAsync(result);
            }
        }
        catch
        {
            // A inicialização nunca é bloqueada por uma consulta opcional.
        }
    }

    private async Task OfferUpdateAsync(UpdateCheckResult result)
    {
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
                        MessageBox.Show(
                            "Esta compilação instalada ainda não possui um instalador transacional. " +
                            "A página oficial será aberta para uma atualização manual que preserva " +
                            "%LocalAppData%\\SlashDesk.",
                            "Atualização da versão instalada",
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
                "Download validado. Encerrando para substituir somente SlashDesk.exe...",
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
            StatusText.Text = "Atualização cancelada; nenhum arquivo do aplicativo foi alterado";
        }
        catch (Exception exception)
        {
            progressWindow.AllowClose();
            AppDiagnosticLog.WriteException("update.prepare.failed", exception);
            MessageBox.Show(
                "A atualização não foi aplicada. O executável atual e SlashDeskData foram " +
                "preservados.\n\n" + exception.Message,
                "Falha na atualização",
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
            ? "Última verificação: ainda não verificado"
            : $"Última verificação: {state.LastCheckedUtc.Value.ToLocalTime():g}";
        LastUpdateResultText.Text = $"Resultado: {state.LastResult}";
        _lastReleaseUrl = state.LastReleaseUrl;
        ReleaseNotesButton.Visibility = string.IsNullOrWhiteSpace(_lastReleaseUrl)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void ReleaseNotes_OnClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastReleaseUrl))
        {
            Process.Start(new ProcessStartInfo(_lastReleaseUrl) { UseShellExecute = true });
        }
    }

    private static string ProductVersion() =>
        (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetName().Version?.ToString(3) ?? "0.0.0";

    private void OpenGitHub_OnClick(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(
            "https://github.com/lucasllira/SlashText")
        {
            UseShellExecute = true
        });
    }

    private void ReplaceList(IEnumerable<Snippet> snippets)
    {
        _snippets.Clear();
        foreach (var snippet in snippets)
        {
            _snippets.Add(snippet);
        }
        RefreshNavigation();
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateResponsiveLayout(e.NewSize.Width);

    private void UpdateResponsiveLayout(double width)
    {
        if (ShortcutSidebarPanel is null)
        {
            return;
        }

        var compact = width < 1080;
        if (compact)
        {
            ShortcutSecondaryRow.Height = new GridLength(250);
            ShortcutLeftColumn.Width = new GridLength(250);
            ShortcutLeftDividerColumn.Width = GridLength.Auto;
            ShortcutRightDividerColumn.Width = GridLength.Auto;
            ShortcutRightColumn.Width = new GridLength(250);

            Grid.SetRow(ShortcutEditorPanel, 0);
            Grid.SetColumn(ShortcutEditorPanel, 0);
            Grid.SetColumnSpan(ShortcutEditorPanel, 5);

            Grid.SetRow(ShortcutSidebarPanel, 1);
            Grid.SetColumn(ShortcutSidebarPanel, 0);
            Grid.SetColumnSpan(ShortcutSidebarPanel, 2);

            Grid.SetRow(ShortcutVariablesPanel, 1);
            Grid.SetColumn(ShortcutVariablesPanel, 2);
            Grid.SetColumnSpan(ShortcutVariablesPanel, 3);

            ShortcutLeftDivider.Visibility = Visibility.Collapsed;
            ShortcutRightDivider.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShortcutSecondaryRow.Height = new GridLength(0);
            ShortcutLeftColumn.Width = new GridLength(260);
            ShortcutLeftDividerColumn.Width = new GridLength(1);
            ShortcutRightDividerColumn.Width = new GridLength(1);
            ShortcutRightColumn.Width = new GridLength(260);

            Grid.SetRow(ShortcutSidebarPanel, 0);
            Grid.SetColumn(ShortcutSidebarPanel, 0);
            Grid.SetColumnSpan(ShortcutSidebarPanel, 1);

            Grid.SetRow(ShortcutEditorPanel, 0);
            Grid.SetColumn(ShortcutEditorPanel, 2);
            Grid.SetColumnSpan(ShortcutEditorPanel, 1);

            Grid.SetRow(ShortcutVariablesPanel, 0);
            Grid.SetColumn(ShortcutVariablesPanel, 4);
            Grid.SetColumnSpan(ShortcutVariablesPanel, 1);

            ShortcutLeftDivider.Visibility = Visibility.Visible;
            ShortcutRightDivider.Visibility = Visibility.Visible;
        }
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

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_exitRequested && _settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _trayIcon?.ShowBalloonTip(
                1800,
                "SlashDesk continua ativo",
                "Use o ícone da bandeja para abrir ou sair.",
                Forms.ToolTipIcon.Info);
            return;
        }

        _activeUpdateCancellation?.Cancel();
        DisposeServices();
    }

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        DisposeServices();
        System.Windows.Application.Current.Shutdown();
    }

    private void ShowFromTray()
    {
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
        Activated -= MainWindow_OnActivated;
        _keyboardHook.ExpansionRequested -= KeyboardHook_OnExpansionRequested;
        _keyboardHook.SuggestionsChanged -= KeyboardHook_OnSuggestionsChanged;
        _keyboardHook.Dispose();
        _quickAccentService.Changed -= QuickAccentService_OnChanged;
        _quickAccentService.CharacterInserted -= QuickAccentService_OnCharacterInserted;
        _quickAccentService.Dispose();
        _captureShortcuts.Triggered -= CaptureShortcuts_OnTriggered;
        _captureShortcuts.Dispose();
        _recordingControl?.Close();
        _recordingService?.Dispose();
        _gifRecordingSession?.Dispose();
        _suggestionWindow.Close();
        _quickAccentWindow.Close();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
    }
}
