using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using SlashText.Services;
using Forms = System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;

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
            Background = FindBrush("SurfaceBrush", Brushes.White),
            BorderBrush = FindBrush("DividerBrush", Brushes.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 16,
                ShadowDepth = 3,
                Opacity = ThemeService.IsDark ? .28 : .12
            },
            Child = _panel
        };
        Content = _surface;
    }

    public void UpdateChoices(string choices, int selectedIndex, string position, bool showUnicode)
    {
        _panel.Children.Clear();
        _surface.Background = FindBrush("SurfaceBrush", Brushes.White);
        _surface.BorderBrush = FindBrush(
            "DividerBrush",
            new SolidColorBrush(Color.FromRgb(40, 48, 61)));
        var accent = FindBrush(
            "AccentBrush",
            new SolidColorBrush(Color.FromRgb(8, 126, 139)));
        var selected = FindBrush("AccentSubtleBrush", accent);
        var ink = FindBrush(
            "InkBrush",
            ThemeService.IsDark ? Brushes.White : Brushes.Black);
        for (var index = 0; index < choices.Length; index++)
        {
            var character = choices[index];
            _panel.Children.Add(new Border
            {
                Background = index == selectedIndex
                    ? selected
                    : Brushes.Transparent,
                BorderBrush = index == selectedIndex ? accent : Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(11, 7, 11, 7),
                Margin = new Thickness(2),
                Child = new TextBlock
                {
                    Text = showUnicode ? $"{character}\nU+{(int)character:X4}" : character.ToString(),
                    Foreground = index == selectedIndex ? accent : ink,
                    FontSize = showUnicode ? 12 : 20,
                    TextAlignment = TextAlignment.Center
                }
            });
        }

        Show();
        UpdateLayout();
        PositionOnActiveMonitor(position);
    }

    private void PositionOnActiveMonitor(string position)
    {
        var anchor = CaretLocator.GetScreenPosition();
        var screen = Forms.Screen.FromPoint(new DrawingPoint((int)anchor.X, (int)anchor.Y));
        var handle = new WindowInteropHelper(this).Handle;
        var dpi = Math.Max(96u, GetDpiForWindow(handle));
        var scale = dpi / 96d;
        var desired = new DrawingSize(
            Math.Max(1, (int)Math.Ceiling(ActualWidth * scale)),
            Math.Max(1, (int)Math.Ceiling(ActualHeight * scale)));
        var bounds = QuickAccentPlacementCalculator.Place(
            screen.WorkingArea,
            desired,
            position,
            Math.Max(8, (int)Math.Round(16 * scale)));
        _ = SetWindowPos(
            handle,
            IntPtr.Zero,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            0x0010 | 0x0004);
    }

    private static Brush FindBrush(string key, Brush fallback) =>
        System.Windows.Application.Current.TryFindResource(key) as Brush ?? fallback;

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
