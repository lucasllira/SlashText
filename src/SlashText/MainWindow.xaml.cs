using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SlashText.Models;
using SlashText.Services;

namespace SlashText;

public partial class MainWindow : Window
{
    private readonly SnippetMarkdownRepository _repository = new();
    private readonly ObservableCollection<Snippet> _snippets = [];
    private Snippet? _selected;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_OnLoaded;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var loaded = await _repository.LoadAsync();
            ReplaceList(loaded);
            SnippetsList.SelectedIndex = _snippets.Count > 0 ? 0 : -1;

            if (_snippets.Count == 0)
            {
                BeginNewSnippet();
            }

            StatusText.Text = $"{_snippets.Count} atalho(s) carregado(s)";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Não foi possível carregar o snippets.md.\n\n{exception.Message}",
                "SlashText",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            BeginNewSnippet();
        }
    }

    private void SearchBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        SnippetsList.ItemsSource = string.IsNullOrWhiteSpace(query)
            ? _snippets
            : _snippets.Where(item =>
                    item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                    item.Trigger.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Category.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
    }

    private void SnippetsList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SnippetsList.SelectedItem is not Snippet snippet)
        {
            return;
        }

        _selected = snippet;
        NameBox.Text = snippet.Name;
        TriggerBox.Text = snippet.Trigger;
        CategoryBox.Text = snippet.Category;
        ContentBox.Text = snippet.Content;
        FormatBox.SelectedIndex = snippet.Format == SnippetFormat.Markdown ? 1 : 0;
        StatusText.Text = snippet.Enabled ? "Atalho ativo" : "Atalho pausado";
    }

    private void NewSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        BeginNewSnippet();
    }

    private void BeginNewSnippet()
    {
        SnippetsList.SelectedItem = null;
        _selected = null;
        NameBox.Clear();
        TriggerBox.Text = "/";
        CategoryBox.Text = "Geral";
        ContentBox.Clear();
        FormatBox.SelectedIndex = 0;
        StatusText.Text = "Novo atalho";
        NameBox.Focus();
    }

    private async void SaveSnippet_OnClick(object sender, RoutedEventArgs e)
    {
        var previous = _selected;
        var candidate = new Snippet
        {
            Id = previous?.Id ?? Guid.NewGuid(),
            Name = NameBox.Text.Trim(),
            Trigger = TriggerBox.Text.Trim(),
            Category = string.IsNullOrWhiteSpace(CategoryBox.Text)
                ? "Geral"
                : CategoryBox.Text.Trim(),
            Content = ContentBox.Text.Replace("\r\n", "\n"),
            Format = FormatBox.SelectedIndex == 1
                ? SnippetFormat.Markdown
                : SnippetFormat.Plain,
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
                var index = _snippets.IndexOf(previous);
                _snippets[index] = candidate;
            }

            _selected = candidate;
            ApplyFilter();
            SnippetsList.SelectedItem = candidate;
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

        var result = MessageBox.Show(
            $"Excluir o atalho {_selected.Trigger}?",
            "SlashText",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        var removed = _selected;
        _snippets.Remove(removed);

        try
        {
            await _repository.SaveAsync(_snippets);
            ApplyFilter();
            BeginNewSnippet();
            StatusText.Text = "Atalho excluído; backup criado";
        }
        catch (Exception exception)
        {
            _snippets.Add(removed);
            ApplyFilter();
            MessageBox.Show(
                exception.Message,
                "Não foi possível excluir",
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

        ApplyFilter();
    }
}
