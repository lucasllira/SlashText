using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SlashText.Views;

public sealed class QuickAccentWindow : Window
{
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };

    public QuickAccentWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        IsHitTestVisible = false;
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(244, 25, 29, 39)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8),
            Child = _panel
        };
    }

    public void UpdateChoices(string choices, int selectedIndex, string position, bool showUnicode)
    {
        _panel.Children.Clear();
        for (var index = 0; index < choices.Length; index++)
        {
            var character = choices[index];
            _panel.Children.Add(new Border
            {
                Background = index == selectedIndex
                    ? new SolidColorBrush(Color.FromRgb(99, 91, 255))
                    : Brushes.Transparent,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(11, 7, 11, 7),
                Margin = new Thickness(2),
                Child = new TextBlock
                {
                    Text = showUnicode ? $"{character}\nU+{(int)character:X4}" : character.ToString(),
                    Foreground = Brushes.White,
                    FontSize = showUnicode ? 12 : 20,
                    TextAlignment = TextAlignment.Center
                }
            });
        }

        Show();
        Left = Math.Max(8, (SystemParameters.WorkArea.Width - ActualWidth) / 2);
        Top = position switch
        {
            "TopCenter" => 24,
            "Center" => Math.Max(8, (SystemParameters.WorkArea.Height - ActualHeight) / 2),
            _ => Math.Max(8, SystemParameters.WorkArea.Height - ActualHeight - 42)
        };
    }
}
