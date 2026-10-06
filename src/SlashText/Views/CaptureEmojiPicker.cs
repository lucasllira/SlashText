using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlashText.Design;
using SlashText.Services;

namespace SlashText.Views;

/// <summary>Shared searchable picker using the exact local export assets.</summary>
public static class CaptureEmojiPicker
{
    public static IReadOnlyList<NotoEmojiItem> Search(string query)
    {
        static string Key(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Trim().ToLowerInvariant();
        var key = Key(query);
        return NotoEmojiCatalog.Items.Where(item => Key(item.Name).Contains(key, StringComparison.Ordinal)).ToArray();
    }

    public static string? Show(Window? owner)
    {
        string? selected = null;
        var dialog = new Window
        {
            Title = "Escolher emote", Owner = owner, Width = 660, MaxWidth = SystemParameters.WorkArea.Width - 32,
            MaxHeight = SystemParameters.WorkArea.Height - 48, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.SetResourceReference(Window.BackgroundProperty, "Lab.raised");
        dialog.SetResourceReference(Window.ForegroundProperty, "Lab.text");
        var panel = new StackPanel { Margin = new Thickness(24) };
        var title = new TextBlock { Text = "Escolher emote", Margin = new Thickness(0, 0, 0, 16) };
        title.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Heading");
        panel.Children.Add(title);
        var search = new TextBox { Margin = new Thickness(0, 0, 0, 16), ToolTip = "Buscar por nome, ex.: coração ou sorriso" };
        search.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Field");
        System.Windows.Automation.AutomationProperties.SetName(search, "Buscar emote por nome");
        panel.Children.Add(search);
        var choices = new WrapPanel();
        var scroll = new ScrollViewer { Content = choices, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        panel.Children.Add(scroll);
        var status = new TextBlock { Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
        status.SetResourceReference(TextBlock.ForegroundProperty, "Lab.muted");
        panel.Children.Add(status);
        void Rebuild()
        {
            choices.Children.Clear();
            foreach (var item in Search(search.Text))
            {
                var button = new Button
                {
                    Content = new Image { Source = NotoEmojiCatalog.CreateImageSource(item.Value), Width = 30, Height = 30 },
                    ToolTip = item.Name, Width = 45, Height = 45, Margin = new Thickness(0, 0, 5, 5), Padding = new Thickness(5)
                };
                System.Windows.Automation.AutomationProperties.SetName(button, item.Name);
                button.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Button");
                button.Click += (_, _) => { selected = item.Value; dialog.DialogResult = true; };
                choices.Children.Add(button);
            }
            status.Text = choices.Children.Count == 0 ? "Nenhum emote encontrado." : "Selecione e clique na imagem para inserir.";
        }
        search.TextChanged += (_, _) => Rebuild(); Rebuild();
        var cancel = new Button { Content = "Cancelar", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        cancel.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Button");
        panel.Children.Add(cancel);
        LabMotion.SetEntrance(panel, "Popup");
        dialog.Content = panel;
        dialog.SourceInitialized += (_, _) => ThemeService.ApplyToWindow(dialog);
        dialog.Loaded += (_, _) => search.Focus();
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.DialogResult = false; };
        return dialog.ShowDialog() == true ? selected : null;
    }
}
