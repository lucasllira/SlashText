using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SlashText.Views;

public sealed class RegionCaptureWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Rectangle _selection = new()
    {
        Stroke = Brushes.White,
        StrokeThickness = 2,
        Fill = new SolidColorBrush(Color.FromArgb(45, 0, 184, 212)),
        Visibility = Visibility.Collapsed
    };
    private Point _start;
    private bool _dragging;

    public Rect SelectedRegion { get; private set; }

    public RegionCaptureWindow()
    {
        Title = "Selecionar região";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0));
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        var help = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(220, 24, 32, 43)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 9),
            Child = new TextBlock
            {
                Text = "Arraste para selecionar · Esc cancela",
                Foreground = Brushes.White,
                FontSize = 14
            }
        };
        Canvas.SetLeft(help, 24);
        Canvas.SetTop(help, 24);
        _canvas.Children.Add(help);
        _canvas.Children.Add(_selection);
        Content = _canvas;

        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                DialogResult = false;
            }
        };
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(_canvas);
        _dragging = true;
        _selection.Visibility = Visibility.Visible;
        CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }
        UpdateSelection(e.GetPosition(_canvas));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();
        var end = e.GetPosition(_canvas);
        UpdateSelection(end);
        var local = Normalize(_start, end);
        if (local.Width < 4 || local.Height < 4)
        {
            DialogResult = false;
            return;
        }

        var screenStart = PointToScreen(new Point(local.Left, local.Top));
        var screenEnd = PointToScreen(new Point(local.Right, local.Bottom));
        SelectedRegion = new Rect(
            screenStart.X,
            screenStart.Y,
            Math.Abs(screenEnd.X - screenStart.X),
            Math.Abs(screenEnd.Y - screenStart.Y));
        DialogResult = true;
    }

    private void UpdateSelection(Point end)
    {
        var rect = Normalize(_start, end);
        Canvas.SetLeft(_selection, rect.Left);
        Canvas.SetTop(_selection, rect.Top);
        _selection.Width = rect.Width;
        _selection.Height = rect.Height;
    }

    private static Rect Normalize(Point start, Point end) =>
        new(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));
}
