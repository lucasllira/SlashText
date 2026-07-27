using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SlashText.Services;

namespace SlashText.Views;

public sealed class VariableInputWindow : Window
{
    private readonly Dictionary<string, TextBox> _inputs =
        new(StringComparer.CurrentCultureIgnoreCase);

    public VariableInputWindow(IReadOnlyList<TemplateField> fields)
    {
        Title = "Preencher campos";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 250));
        ShowInTaskbar = false;

        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        root.Children.Add(new TextBlock
        {
            Text = "Complete antes de inserir",
            FontFamily = new FontFamily("Segoe UI Variable Display"),
            FontSize = 22,
            FontWeight = FontWeights.SemiBold
        });

        var fieldsPanel = new StackPanel();
        Grid.SetRow(fieldsPanel, 2);
        root.Children.Add(fieldsPanel);

        foreach (var field in fields)
        {
            var label = new TextBlock
            {
                Text = Humanize(field.Name),
                Margin = new Thickness(0, 0, 0, 5),
                Foreground = new SolidColorBrush(Color.FromRgb(89, 99, 113))
            };
            var input = new TextBox
            {
                Text = field.DefaultValue ?? string.Empty,
                Margin = new Thickness(0, 0, 0, 13),
                MinWidth = 360
            };
            _inputs[field.Name] = input;
            fieldsPanel.Children.Add(label);
            fieldsPanel.Children.Add(input);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetRow(buttons, 4);
        root.Children.Add(buttons);

        var cancel = new Button { Content = "Cancelar", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) };
        var insert = new Button
        {
            Content = "Inserir",
            IsDefault = true,
            Background = new SolidColorBrush(Color.FromRgb(99, 91, 255)),
            Foreground = Brushes.White
        };
        insert.Click += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(insert);

        Content = root;
        Loaded += (_, _) => _inputs.Values.FirstOrDefault()?.Focus();
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                DialogResult = true;
                Close();
            }
        };
    }

    public IReadOnlyDictionary<string, string> Values =>
        _inputs.ToDictionary(item => item.Key, item => item.Value.Text);

    private static string Humanize(string name)
    {
        var value = name.Replace('_', ' ').Trim();
        return string.IsNullOrEmpty(value)
            ? "Campo"
            : char.ToUpper(value[0]) + value[1..];
    }
}
