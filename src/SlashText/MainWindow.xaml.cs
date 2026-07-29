using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
    private readonly CaptureService _captureService = new();
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

    public MainWindow()
    {
        InitializeComponent();
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
            QuickAccentExcludedAppsBox.Text = _settings.QuickAccentExcludedApps;
            ApplyQuickAccentCharacterSetSelection(_settings.QuickAccentCharacterSets);
            ApplyQuickAccentSettings();
            await _captureService.LoadAsync();
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
            StatusText.Text = "Salvo em SlashDeskData/snippets.md";
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

    private void SettingsTab_OnClick(object sender, RoutedEventArgs e) =>
        ShowView(SettingsView, SettingsTabButton);

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
            button.Background = ReferenceEquals(button, selectedButton)
                ? (Brush)FindResource("SelectedBrush")
                : Brushes.Transparent;
            button.Foreground = ReferenceEquals(button, selectedButton)
                ? (Brush)FindResource("AccentBrush")
                : (Brush)FindResource("InkBrush");
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
        CaptureMonitorShortcutBox.Text = capture.ActiveMonitorShortcut;
        CaptureRegionShortcutBox.Text = capture.RegionShortcut;
        CaptureWindowShortcutBox.Text = capture.WindowShortcut;
        CaptureDirectoryBox.Text = capture.OutputDirectoryTemplate;
        CaptureFileNameBox.Text = capture.FileNameTemplate;
        SelectComboByTag(CaptureFormatBox, capture.ImageFormat);
        CaptureQualityBox.Text = capture.JpegQuality.ToString();
        CaptureClipboardCheckBox.IsChecked = capture.CopyToClipboard;
        CaptureAutoSaveCheckBox.IsChecked = capture.SaveAutomatically;
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
            HideSlashDeskDuringCapture = true
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

    private async Task RunCaptureAsync(
        CaptureShortcutAction action,
        bool invokedByShortcut)
    {
        try
        {
            System.Drawing.Rectangle? bounds = null;
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
                var region = _captureService.SelectRegion(null);
                if (region is not null)
                {
                    bounds = new System.Drawing.Rectangle(
                        (int)Math.Round(region.Value.X),
                        (int)Math.Round(region.Value.Y),
                        (int)Math.Round(region.Value.Width),
                        (int)Math.Round(region.Value.Height));
                }
            }
            else if (action == CaptureShortcutAction.Window)
            {
                bounds = _captureService.WindowUnderCursorBounds();
            }

            if (bounds is not null)
            {
                var result = await _captureService.CaptureAndProcessAsync(
                    bounds.Value,
                    type,
                    _settings.Capture,
                    openEditor: action == CaptureShortcutAction.Region,
                    owner: shouldHide ? null : this);
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
        foreach (var item in _captureService.History.Take(6))
        {
            var file = string.IsNullOrWhiteSpace(item.FilePath)
                ? "Somente clipboard"
                : Path.GetFileName(item.FilePath);
            CaptureHistoryPanel.Children.Add(new TextBlock
            {
                Text = $"{item.CreatedAt:dd/MM HH:mm} · {item.Type} · " +
                       $"{item.Width}×{item.Height} · {file}",
                Margin = new Thickness(0, 0, 0, 7),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)FindResource("MutedBrush")
            });
        }
        if (CaptureHistoryPanel.Children.Count == 0)
        {
            CaptureHistoryPanel.Children.Add(new TextBlock
            {
                Text = "As últimas capturas aparecerão aqui.",
                Foreground = (Brush)FindResource("MutedBrush")
            });
        }
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
        try
        {
            var result = await _updateService.CheckAsync();
            var choice = MessageBox.Show(
                result.Message + (result.Url is null ? string.Empty : "\n\nAbrir a página de download?"),
                "Atualizações do SlashDesk",
                result.Url is null ? MessageBoxButton.OK : MessageBoxButton.YesNo,
                result.UpdateAvailable ? MessageBoxImage.Information : MessageBoxImage.None);
            if (choice == MessageBoxResult.Yes && result.Url is not null)
            {
                Process.Start(new ProcessStartInfo(result.Url) { UseShellExecute = true });
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
    }

    private async Task CheckUpdatesSilentlyAsync()
    {
        try
        {
            var result = await _updateService.CheckAsync();
            if (result.UpdateAvailable)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = result.Message;
                    _trayIcon?.ShowBalloonTip(
                        2500,
                        "Atualização disponível",
                        result.Message,
                        Forms.ToolTipIcon.Info);
                });
            }
        }
        catch
        {
            // A inicialização nunca é bloqueada por uma consulta opcional.
        }
    }

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
        _suggestionWindow.Close();
        _quickAccentWindow.Close();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
    }
}
