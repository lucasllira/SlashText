using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SlashText.Design;
using SlashText.Services;

namespace SlashText.Views;

/// <summary>Complete offline catalog. Paging bounds decoded images and live WPF controls.</summary>
public static class CaptureEmojiPicker
{
    public const int PageSize = 96;
    private static string Key(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Trim().ToLowerInvariant();
    private static readonly IReadOnlyDictionary<string, string> SearchKeys = NotoEmojiCatalog.Items.ToDictionary(
        item => item.Value, item => Key(item.Name + " " + item.Keywords + " " + item.Value), StringComparer.Ordinal);

    public static IReadOnlyList<NotoEmojiItem> Search(string query, string? category = null)
    {
        var words = Key(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return NotoEmojiCatalog.Items.Where(item =>
            (string.IsNullOrEmpty(category) || category == "Todos" || item.Category == category) &&
            words.All(word => SearchKeys[item.Value].Contains(word, StringComparison.Ordinal))).ToArray();
    }

    internal static FrameworkElement CreateContent(Action<string> select, Action cancel)
    {
        var panel = new Grid { Margin = new Thickness(24) };
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
            panel.RowDefinitions.Add(new RowDefinition { Height = height });
        var title = new TextBlock { Text = "Escolher emote", Margin = new Thickness(0, 0, 0, 8) };
        title.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Heading"); panel.Children.Add(title);
        var caption = new TextBlock { Text = $"{NotoEmojiCatalog.Items.Count:N0} emotes Noto · catálogo local · inclui tons de pele",
            Margin = new Thickness(0, 0, 0, 16), TextWrapping = TextWrapping.Wrap };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted"); Grid.SetRow(caption, 1); panel.Children.Add(caption);
        var filters = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        filters.ColumnDefinitions.Add(new ColumnDefinition());
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(225) });
        var search = new TextBox { Name = "EmojiSearch", Margin = new Thickness(0, 0, 12, 0),
            ToolTip = "Buscar nome, palavra-chave, emoji ou shortcode, ex.: coração, gato, :smile:" };
        search.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Field");
        System.Windows.Automation.AutomationProperties.SetName(search, "Buscar emote por nome ou palavra-chave");
        filters.Children.Add(search);
        var category = new ComboBox { ItemsSource = new[] { "Todos" }.Concat(NotoEmojiCatalog.Categories).ToArray(),
            SelectedIndex = 0, ToolTip = "Categoria de emotes" };
        category.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Combo");
        System.Windows.Automation.AutomationProperties.SetName(category, "Categoria de emotes");
        Grid.SetColumn(category, 1); filters.Children.Add(category); Grid.SetRow(filters, 2); panel.Children.Add(filters);
        var choices = new WrapPanel { Name = "EmojiChoices" };
        var scroll = new ScrollViewer { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 3); panel.Children.Add(scroll);
        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions);
        Button ActionButton(string text, Action action)
        {
            var button = new Button { Content = text, Margin = new Thickness(8, 0, 0, 0) };
            button.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Button");
            button.Click += (_, _) => action(); actions.Children.Add(button); return button;
        }
        var status = new TextBlock { Name = "EmojiStatus", VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        status.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted");
        IReadOnlyList<NotoEmojiItem> matches = Array.Empty<NotoEmojiItem>();
        var page = 0;
        Button previous = null!, next = null!;
        void RenderPage()
        {
            choices.Children.Clear();
            foreach (var item in matches.Skip(page * PageSize).Take(PageSize))
            {
                var button = new Button
                {
                    Content = new Image { Source = NotoEmojiCatalog.CreateImageSource(item.Value), Width = 30, Height = 30 },
                    ToolTip = item.Name, Width = 46, Height = 46, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(5)
                };
                System.Windows.Automation.AutomationProperties.SetName(button, item.Name);
                button.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Button");
                button.Click += (_, _) => select(item.Value); choices.Children.Add(button);
            }
            previous.IsEnabled = page > 0; next.IsEnabled = (page + 1) * PageSize < matches.Count;
            status.Text = matches.Count == 0 ? "Nenhum emote encontrado." :
                $"{page * PageSize + 1}–{Math.Min((page + 1) * PageSize, matches.Count)} de {matches.Count:N0} · página {page + 1}/{(matches.Count + PageSize - 1) / PageSize}";
            scroll.ScrollToTop();
        }
        previous = ActionButton("Anterior", () => { if (page > 0) { page--; RenderPage(); } });
        next = ActionButton("Próxima", () => { if ((page + 1) * PageSize < matches.Count) { page++; RenderPage(); } });
        var close = ActionButton("Cancelar", cancel); close.IsCancel = true;
        footer.Children.Add(status); Grid.SetRow(footer, 4); panel.Children.Add(footer);
        void Rebuild() { matches = Search(search.Text, category.SelectedItem as string); page = 0; RenderPage(); }
        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        debounce.Tick += (_, _) => { debounce.Stop(); Rebuild(); };
        search.TextChanged += (_, _) => { debounce.Stop(); debounce.Start(); };
        category.SelectionChanged += (_, _) => { debounce.Stop(); Rebuild(); };
        panel.Unloaded += (_, _) => debounce.Stop();
        panel.Loaded += (_, _) => search.Focus();
        Rebuild(); LabMotion.SetEntrance(panel, "Popup"); return panel;
    }

    public static string? Show(Window? owner)
    {
        string? selected = null;
        var dialog = new Window
        {
            Title = "Escolher emote", Owner = owner, Width = Math.Min(900, SystemParameters.WorkArea.Width - 32),
            Height = Math.Min(650, SystemParameters.WorkArea.Height - 48),
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.SetResourceReference(Window.BackgroundProperty, "Lab.raised");
        dialog.SetResourceReference(Window.ForegroundProperty, "Lab.text");
        dialog.Content = CreateContent(value => { selected = value; dialog.DialogResult = true; }, () => dialog.DialogResult = false);
        dialog.SourceInitialized += (_, _) => ThemeService.ApplyToWindow(dialog);
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.DialogResult = false; };
        return dialog.ShowDialog() == true ? selected : null;
    }
}
