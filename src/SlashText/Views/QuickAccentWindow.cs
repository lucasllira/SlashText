using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SlashText.Services;

namespace SlashText.Views;

public sealed class QuickAccentWindow : Window
{
    private readonly StackPanel _panel = new() { Orientation = Orientation.Horizontal };
    private readonly Border _surface;

    public QuickAccentWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        IsHitTestVisible = false;
        _surface = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(244, 25, 29, 39)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, 66, 202, 211)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8),
            Child = _panel
        };
        Content = _surface;
    }

    public void UpdateChoices(string choices, int selectedIndex, string position, bool showUnicode)
    {
        _panel.Children.Clear();
        _surface.Background = ThemeService.IsDark
            ? new SolidColorBrush(Color.FromArgb(248, 18, 24, 32))
            : new SolidColorBrush(Color.FromArgb(248, 255, 255, 255));
        _surface.BorderBrush = FindBrush(
            "DividerBrush",
            new SolidColorBrush(Color.FromRgb(40, 48, 61)));
        var accent = FindBrush(
            "AccentBrush",
            new SolidColorBrush(Color.FromRgb(8, 126, 139)));
        var ink = FindBrush(
            "InkBrush",
            ThemeService.IsDark ? Brushes.White : Brushes.Black);
        for (var index = 0; index < choices.Length; index++)
        {
            var character = choices[index];
            _panel.Children.Add(new Border
            {
                Background = index == selectedIndex
                    ? accent
                    : Brushes.Transparent,
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(11, 7, 11, 7),
                Margin = new Thickness(2),
                Child = new TextBlock
                {
                    Text = showUnicode ? $"{character}\nU+{(int)character:X4}" : character.ToString(),
                    Foreground = index == selectedIndex ? Brushes.White : ink,
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

    private static Brush FindBrush(string key, Brush fallback) =>
        System.Windows.Application.Current.TryFindResource(key) as Brush ?? fallback;
}
