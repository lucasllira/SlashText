using System.Collections.ObjectModel;
using System.ComponentModel;
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
    private readonly JsonFileStore<AppSettings> _settingsStore = new(AppPaths.SettingsFile);
    private readonly ObservableCollection<Snippet> _snippets = [];
    private readonly SuggestionWindow _suggestionWindow = new();

    private Forms.NotifyIcon? _trayIcon;
    private AppSettings _settings = new();
    private Snippet? _selected;
    private bool _exitRequested;
    private bool _servicesDisposed;
    private bool _initialized;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTray();
        Loaded += MainWindow_OnLoaded;
        Closing += MainWindow_OnClosing;
        Closed += MainWindow_OnClosed;
        StateChanged += MainWindow_OnStateChanged;
        _keyboardHook.ExpansionRequested += KeyboardHook_OnExpansionRequested;
        _keyboardHook.SuggestionsChanged += KeyboardHook_OnSuggestionsChanged;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        try
        {
            _settings = await _settingsStore.LoadAsync();
            await _usageService.LoadAsync();
            CloseToTrayCheckBox.IsChecked = _settings.CloseToTray;
            StartWithWindowsCheckBox.IsChecked = _settings.StartWithWindows;
            ShowSuggestionsCheckBox.IsChecked = _settings.ShowSuggestions;

            var loaded = await _repository.LoadAsync();
            ReplaceList(loaded);
            StartMonitoring();

            if (_snippets.Count > 0)
            {
                SelectSnippet(_snippets[0]);
            }
            else
            {
                BeginNewSnippet();
            }

            RefreshStatistics();
            StatusText.Text = $"{_snippets.Count} atalho(s) carregado(s)";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Não foi possível iniciar o SlashText.\n\n{exception.Message}",
                "SlashText",
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
        menu.Items.Add("Abrir SlashText", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Novo atalho", null, (_, _) => Dispatcher.Invoke(() =>
        {
            ShowFromTray();
            BeginNewSnippet();
        }));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Dispatcher.Invoke(RequestExit));

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "SlashText · Monitoramento ativo",
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
                    var form = new VariableInputWindow(fields);
                    if (form.ShowDialog() != true)
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
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 230, 255))
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
            StatusText.Text = "Salvo em snippets.md";
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
                "SlashText",
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
        }
    }

    private void Bold_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleBold.Execute(null, ContentEditor);

    private void Italic_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleItalic.Execute(null, ContentEditor);

    private void Underline_OnClick(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleUnderline.Execute(null, ContentEditor);

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

    private void VariableChip_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string token })
        {
            ContentEditor.Selection.Text = token;
            ContentEditor.Focus();
        }
    }

    private void ShortcutsTab_OnClick(object sender, RoutedEventArgs e)
    {
        ShortcutsView.Visibility = Visibility.Visible;
        StatisticsView.Visibility = Visibility.Collapsed;
        ShortcutsTabButton.Foreground = (Brush)FindResource("AccentBrush");
        StatisticsTabButton.Foreground = (Brush)FindResource("InkBrush");
    }

    private void StatisticsTab_OnClick(object sender, RoutedEventArgs e)
    {
        RefreshStatistics();
        ShortcutsView.Visibility = Visibility.Collapsed;
        StatisticsView.Visibility = Visibility.Visible;
        StatisticsTabButton.Foreground = (Brush)FindResource("AccentBrush");
        ShortcutsTabButton.Foreground = (Brush)FindResource("InkBrush");
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

        RefreshMostUsed();
    }

    private async void Settings_OnClick(object sender, RoutedEventArgs e)
    {
        var previousStart = _settings.StartWithWindows;
        _settings.CloseToTray = CloseToTrayCheckBox.IsChecked == true;
        _settings.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _settings.ShowSuggestions = ShowSuggestionsCheckBox.IsChecked == true;

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

    private void ReplaceList(IEnumerable<Snippet> snippets)
    {
        _snippets.Clear();
        foreach (var snippet in snippets)
        {
            _snippets.Add(snippet);
        }
        RefreshNavigation();
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
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
                "SlashText continua ativo",
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
        _keyboardHook.ExpansionRequested -= KeyboardHook_OnExpansionRequested;
        _keyboardHook.SuggestionsChanged -= KeyboardHook_OnSuggestionsChanged;
        _keyboardHook.Dispose();
        _suggestionWindow.Close();
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
    }
}
