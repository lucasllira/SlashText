using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SlashText.Services;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace SlashText.Views;

public sealed class RegionCaptureWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Rectangle _selection = new()
    {
        Stroke = Brushes.White,
        StrokeThickness = 1.5,
        StrokeDashArray = new DoubleCollection { 3, 2 },
        Fill = Brushes.Transparent,
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false
    };
    private readonly Rectangle[] _shades =
    [
        Shade(), Shade(), Shade(), Shade()
    ];
    private readonly Border _sizeBadge = new()
    {
        Background = new SolidColorBrush(Color.FromArgb(235, 20, 27, 37)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(7),
        Padding = new Thickness(9, 5, 9, 5),
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false
    };
    private readonly Border _toolbar;
    private readonly Border[] _handles;
    private Point _start;
    private Rect _localSelection;
    private bool _dragging;
    private bool _selectionReady;

    public Rect SelectedRegion { get; private set; }
    public CaptureAnnotationKind PreferredTool { get; private set; } =
        CaptureAnnotationKind.Arrow;

    public RegionCaptureWindow()
    {
        Title = "Selecionar região";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false;
        Background = Brushes.Black;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        var desktop = CaptureVirtualDesktop();
        _canvas.Children.Add(new Image
        {
            Source = desktop,
            Width = Width,
            Height = Height,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        });
        foreach (var shade in _shades)
        {
            _canvas.Children.Add(shade);
        }

        var help = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 20, 27, 37)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(14, 9, 14, 9),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "Arraste para selecionar  ·  Enter confirma  ·  Esc cancela",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13
            }
        };
        Canvas.SetLeft(help, 24);
        Canvas.SetTop(help, 24);
        _canvas.Children.Add(help);
        _canvas.Children.Add(_selection);

        _handles =
        [
            Handle(), Handle(), Handle(), Handle(),
            Handle(), Handle(), Handle(), Handle()
        ];
        foreach (var handle in _handles)
        {
            _canvas.Children.Add(handle);
        }

        _sizeBadge.Child = new TextBlock
        {
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12
        };
        _canvas.Children.Add(_sizeBadge);

        _toolbar = BuildToolbar();
        _canvas.Children.Add(_toolbar);
        Content = _canvas;

        Loaded += (_, _) => UpdateShade(default);
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private Border BuildToolbar()
    {
        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        tools.Children.Add(ToolbarButton(
            "Capturar",
            "Confirmar e abrir o editor",
            true,
            (_, _) => Complete(CaptureAnnotationKind.Arrow)));
        tools.Children.Add(Separator());
        tools.Children.Add(ToolbarButton(
            "↗", "Abrir com Seta", false,
            (_, _) => Complete(CaptureAnnotationKind.Arrow)));
        tools.Children.Add(ToolbarButton(
            "▰", "Abrir com Marca-texto", false,
            (_, _) => Complete(CaptureAnnotationKind.Highlighter)));
        tools.Children.Add(ToolbarButton(
            "□", "Abrir com Retângulo", false,
            (_, _) => Complete(CaptureAnnotationKind.Rectangle)));
        tools.Children.Add(ToolbarButton(
            "○", "Abrir com Círculo", false,
            (_, _) => Complete(CaptureAnnotationKind.Ellipse)));
        tools.Children.Add(ToolbarButton(
            "✎", "Abrir com Lápis", false,
            (_, _) => Complete(CaptureAnnotationKind.Pencil)));
        tools.Children.Add(ToolbarButton(
            "T", "Abrir com Texto", false,
            (_, _) => Complete(CaptureAnnotationKind.Text)));
        tools.Children.Add(Separator());
        tools.Children.Add(ToolbarButton(
            "↶", "Selecionar novamente", false,
            (_, _) => ResetSelection()));
        tools.Children.Add(ToolbarButton(
            "×", "Cancelar", false,
            (_, _) => DialogResult = false));

        return new Border
        {
            Visibility = Visibility.Collapsed,
            Background = new SolidColorBrush(Color.FromArgb(245, 18, 26, 37)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(7),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 5,
                Opacity = .38,
                Color = Colors.Black
            },
            Child = tools
        };
    }

    private static Button ToolbarButton(
        string text,
        string toolTip,
        bool primary,
        RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            ToolTip = toolTip,
            MinWidth = primary ? 112 : 40,
            Height = 38,
            Margin = new Thickness(2),
            Padding = primary
                ? new Thickness(16, 5, 16, 5)
                : new Thickness(8, 5, 8, 5),
            Foreground = Brushes.White,
            Background = primary
                ? new SolidColorBrush(Color.FromRgb(14, 165, 233))
                : Brushes.Transparent,
            BorderBrush = primary
                ? new SolidColorBrush(Color.FromRgb(56, 189, 248))
                : new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            FontSize = primary ? 13 : 17,
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            Cursor = Cursors.Hand
        };
        button.Click += click;
        return button;
    }

    private static Border Separator() => new()
    {
        Width = 1,
        Height = 26,
        Margin = new Thickness(5, 7, 5, 7),
        Background = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255))
    };

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            IsToolbarSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _start = e.GetPosition(_canvas);
        _dragging = true;
        _selectionReady = false;
        _toolbar.Visibility = Visibility.Collapsed;
        SetHandlesVisibility(Visibility.Collapsed);
        _selection.Visibility = Visibility.Visible;
        _sizeBadge.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_start);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragging)
        {
            UpdateSelection(e.GetPosition(_canvas));
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        ReleaseMouseCapture();
        UpdateSelection(e.GetPosition(_canvas));
        if (_localSelection.Width < 4 || _localSelection.Height < 4)
        {
            ResetSelection();
            return;
        }

        _selectionReady = true;
        SetHandlesVisibility(Visibility.Visible);
        PositionHandles();
        PositionToolbar();
        _toolbar.Visibility = Visibility.Visible;
        Cursor = Cursors.Arrow;
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _selectionReady)
        {
            Complete(CaptureAnnotationKind.Arrow);
            e.Handled = true;
        }
        else if (e.Key == Key.R && _selectionReady)
        {
            ResetSelection();
            e.Handled = true;
        }
    }

    private void Complete(CaptureAnnotationKind preferredTool)
    {
        if (!_selectionReady)
        {
            return;
        }

        var screenStart = PointToScreen(
            new Point(_localSelection.Left, _localSelection.Top));
        var screenEnd = PointToScreen(
            new Point(_localSelection.Right, _localSelection.Bottom));
        SelectedRegion = new Rect(
            screenStart.X,
            screenStart.Y,
            Math.Abs(screenEnd.X - screenStart.X),
            Math.Abs(screenEnd.Y - screenStart.Y));
        PreferredTool = preferredTool;
        DialogResult = true;
    }

    private void ResetSelection()
    {
        _dragging = false;
        _selectionReady = false;
        ReleaseMouseCapture();
        _selection.Visibility = Visibility.Collapsed;
        _sizeBadge.Visibility = Visibility.Collapsed;
        _toolbar.Visibility = Visibility.Collapsed;
        SetHandlesVisibility(Visibility.Collapsed);
        Cursor = Cursors.Cross;
        UpdateShade(default);
    }

    private void UpdateSelection(Point end)
    {
        _localSelection = Normalize(_start, end);
        Canvas.SetLeft(_selection, _localSelection.Left);
        Canvas.SetTop(_selection, _localSelection.Top);
        _selection.Width = _localSelection.Width;
        _selection.Height = _localSelection.Height;
        UpdateShade(_localSelection);

        if (_sizeBadge.Child is TextBlock size)
        {
            var screenStart = PointToScreen(
                new Point(_localSelection.Left, _localSelection.Top));
            var screenEnd = PointToScreen(
                new Point(_localSelection.Right, _localSelection.Bottom));
            size.Text =
                $"{Math.Round(Math.Abs(screenEnd.X - screenStart.X)):N0} × " +
                $"{Math.Round(Math.Abs(screenEnd.Y - screenStart.Y)):N0}";
        }
        Canvas.SetLeft(_sizeBadge, _localSelection.Left);
        Canvas.SetTop(
            _sizeBadge,
            _localSelection.Top > 40
                ? _localSelection.Top - 34
                : _localSelection.Bottom + 8);
    }

    private void UpdateShade(Rect clear)
    {
        var width = Math.Max(0, ActualWidth > 0 ? ActualWidth : Width);
        var height = Math.Max(0, ActualHeight > 0 ? ActualHeight : Height);
        if (clear.IsEmpty || clear.Width <= 0 || clear.Height <= 0)
        {
            Place(_shades[0], 0, 0, width, height);
            for (var index = 1; index < _shades.Length; index++)
            {
                Place(_shades[index], 0, 0, 0, 0);
            }
            return;
        }

        Place(_shades[0], 0, 0, width, clear.Top);
        Place(_shades[1], 0, clear.Bottom, width, height - clear.Bottom);
        Place(_shades[2], 0, clear.Top, clear.Left, clear.Height);
        Place(
            _shades[3],
            clear.Right,
            clear.Top,
            width - clear.Right,
            clear.Height);
    }

    private void PositionHandles()
    {
        var x = new[]
        {
            _localSelection.Left,
            _localSelection.Left + (_localSelection.Width / 2),
            _localSelection.Right
        };
        var y = new[]
        {
            _localSelection.Top,
            _localSelection.Top + (_localSelection.Height / 2),
            _localSelection.Bottom
        };
        var positions = new[]
        {
            new Point(x[0], y[0]), new Point(x[1], y[0]),
            new Point(x[2], y[0]), new Point(x[0], y[1]),
            new Point(x[2], y[1]), new Point(x[0], y[2]),
            new Point(x[1], y[2]), new Point(x[2], y[2])
        };
        for (var index = 0; index < _handles.Length; index++)
        {
            Canvas.SetLeft(_handles[index], positions[index].X - 5);
            Canvas.SetTop(_handles[index], positions[index].Y - 5);
        }
    }

    private void PositionToolbar()
    {
        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = _toolbar.DesiredSize;
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        var left = Math.Clamp(
            _localSelection.Left +
            ((_localSelection.Width - desired.Width) / 2),
            12,
            Math.Max(12, width - desired.Width - 12));
        var below = _localSelection.Bottom + 14;
        var top = below + desired.Height <= height - 12
            ? below
            : Math.Max(12, _localSelection.Top - desired.Height - 14);
        Canvas.SetLeft(_toolbar, left);
        Canvas.SetTop(_toolbar, top);
    }

    private void SetHandlesVisibility(Visibility visibility)
    {
        foreach (var handle in _handles)
        {
            handle.Visibility = visibility;
        }
    }

    private bool IsToolbarSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, _toolbar))
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private static Rectangle Shade() => new()
    {
        Fill = new SolidColorBrush(Color.FromArgb(150, 3, 12, 22)),
        IsHitTestVisible = false
    };

    private static Border Handle() => new()
    {
        Width = 10,
        Height = 10,
        CornerRadius = new CornerRadius(5),
        Background = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(14, 165, 233)),
        BorderThickness = new Thickness(2),
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false
    };

    private static void Place(
        FrameworkElement element,
        double left,
        double top,
        double width,
        double height)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
    }

    private static Rect Normalize(Point start, Point end) =>
        new(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));

    private static BitmapSource CaptureVirtualDesktop()
    {
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        var left = virtualScreen.Left;
        var top = virtualScreen.Top;
        var width = Math.Max(1, virtualScreen.Width);
        var height = Math.Max(1, virtualScreen.Height);
        using var bitmap = new DrawingBitmap(
            width,
            height,
            DrawingPixelFormat.Format32bppPArgb);
        using (var graphics = DrawingGraphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                left,
                top,
                0,
                0,
                bitmap.Size,
                System.Drawing.CopyPixelOperation.SourceCopy);
        }

        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            _ = DeleteObject(handle);
        }
    }

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);
}
