using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SlashText.Models;
using Point = System.Windows.Point;

namespace SlashText.Views;

public sealed class SuggestionWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private readonly StackPanel _items = new();
    private readonly Border _surface;

    public SuggestionWindow()
    {
        Width = 330;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 280;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        AllowsTransparency = true;
        Background = Brushes.Transparent;

        _surface = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 3,
                Opacity = 0.18
            },
            Child = _items
        };
        Content = _surface;

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | WsExNoActivate);
        };
    }

    public void UpdateSuggestions(IReadOnlyList<Snippet> snippets, Point position)
    {
        if (snippets.Count == 0)
        {
            Hide();
            return;
        }

        _surface.Background = FindBrush("SurfaceBrush", Brushes.White);
        _surface.BorderBrush = FindBrush("DividerBrush", Brushes.LightGray);
        _items.Children.Clear();
        foreach (var snippet in snippets)
        {
            var row = new Grid { Margin = new Thickness(8, 6, 8, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(new TextBlock
            {
                Text = snippet.Trigger,
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                FontWeight = FontWeights.SemiBold,
                Foreground = FindBrush("AccentBrush", new SolidColorBrush(Color.FromRgb(99, 91, 255)))
            });
            var name = new TextBlock
            {
                Text = snippet.Name,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = FindBrush("MutedBrush", new SolidColorBrush(Color.FromRgb(89, 99, 113)))
            };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
            _items.Children.Add(row);
        }

        Left = position.X;
        Top = position.Y;
        if (!IsVisible)
        {
            Show();
        }
    }

    private static Brush FindBrush(string key, Brush fallback) =>
        System.Windows.Application.Current.TryFindResource(key) as Brush ?? fallback;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr window, int index, int value);
}
