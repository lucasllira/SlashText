using System.Windows;
using System.Windows.Controls;
using SlashText.Services;
using TextBox = System.Windows.Controls.TextBox;

namespace SlashText.Views;

public sealed class PromptDialog : Window
{
    private readonly TextBox _input;

    private PromptDialog(string title, string label, string initialValue)
    {
        Title = title;
        Width = 430;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        SetResourceReference(BackgroundProperty, "CanvasBrush");
        SourceInitialized += (_, _) => ThemeService.ApplyToWindow(this);

        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 7) });
        _input = new TextBox { Text = initialValue };
        panel.Children.Add(_input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };
        buttons.Children.Add(new Button
        {
            Content = "Cancelar",
            IsCancel = true,
            Margin = new Thickness(0, 0, 10, 0)
        });
        var confirm = new Button { Content = "Aplicar", IsDefault = true };
        confirm.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        buttons.Children.Add(confirm);
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };
    }

    public static string? Show(Window owner, string title, string label, string initialValue = "")
    {
        var dialog = new PromptDialog(title, label, initialValue) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog._input.Text.Trim() : null;
    }
}
