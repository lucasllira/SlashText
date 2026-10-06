using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SlashText.Design;
using SlashText.Services;

namespace SlashText.Views;

/// <summary>Both editors use the same local assets as the final renderer.</summary>
public static class CaptureEmojiPicker
{
    public static string? Show(Window? owner)
    {
        string? selected = null;
        var dialog = new Window
        {
            Title = "Inserir emoji", Owner = owner, Width = 440, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        dialog.SetResourceReference(Window.BackgroundProperty, "Lab.raised");
        dialog.SetResourceReference(Window.ForegroundProperty, "Lab.text");
        var panel = new StackPanel { Margin = new Thickness(24) };
        var title = new TextBlock { Text = "Escolha um emoji", Margin = new Thickness(0, 0, 0, 14) };
        title.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Heading");
        panel.Children.Add(title);
        var choices = new WrapPanel();
        foreach (var item in NotoEmojiCatalog.Items)
        {
            var button = new Button
            {
                Content = new Image { Source = NotoEmojiCatalog.CreateImageSource(item.Value), Width = 28, Height = 28 },
                ToolTip = item.Name, Width = 52, Height = 48, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(6)
            };
            button.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Button");
            button.Click += (_, _) => { selected = item.Value; dialog.DialogResult = true; };
            choices.Children.Add(button);
        }
        panel.Children.Add(choices);
        var cancel = new Button { Content = "Cancelar", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0) };
        cancel.SetResourceReference(FrameworkElement.StyleProperty, "Lab.Button");
        panel.Children.Add(cancel);
        LabMotion.SetEntrance(panel, "Popup");
        dialog.Content = panel;
        dialog.SourceInitialized += (_, _) => ThemeService.ApplyToWindow(dialog);
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.DialogResult = false; };
        return dialog.ShowDialog() == true ? selected : null;
    }
}
