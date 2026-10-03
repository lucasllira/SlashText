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
using DrawingColor = System.Drawing.Color;

namespace SlashText.Views;

/// <summary>
/// Lightweight, in-page annotation surface used by the Capture workbench.
/// The full editor remains available for crop, resize and privacy effects.
/// </summary>
public sealed class CaptureWorkbenchEditor : UserControl, IDisposable
{
    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly Canvas _overlay = new() { Background = Brushes.Transparent };
    private readonly Grid _surface = new() { Background = Brushes.Black, ClipToBounds = true };
    private readonly List<CaptureAnnotation> _annotations = [];
    private readonly Stack<CaptureAnnotation> _redo = new();
    private readonly List<Point> _pencilPoints = [];
    private DrawingBitmap? _source;
    private CaptureAnnotationKind? _tool;
    private Point _start;
    private bool _drawing;
    private int _color = DrawingColor.FromArgb(232, 78, 96).ToArgb();
    private float _thickness = 4;

    public event EventHandler? StateChanged;

    public bool HasImage => _source is not null;
    public bool CanUndo => _annotations.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public CaptureAnnotationKind? SelectedTool => _tool;

    public CaptureWorkbenchEditor()
    {
        Focusable = true;
        _surface.Children.Add(_image);
        _surface.Children.Add(_overlay);
        _overlay.MouseLeftButtonDown += OverlayOnMouseLeftButtonDown;
        _overlay.MouseMove += OverlayOnMouseMove;
        _overlay.MouseLeftButtonUp += OverlayOnMouseLeftButtonUp;
        Content = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.Both,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = _surface
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public void LoadImage(string path)
    {
        using var file = new DrawingBitmap(path);
        LoadImage(file);
    }

    public void LoadImage(DrawingBitmap bitmap)
    {
        _source?.Dispose();
        _source = new DrawingBitmap(bitmap);
        _annotations.Clear();
        _redo.Clear();
        _tool = null;
        _drawing = false;
        _overlay.ReleaseMouseCapture();
        _overlay.Cursor = Cursors.Arrow;
        var scale = Math.Min(1d, Math.Min(1120d / _source.Width, 620d / _source.Height));
        var width = Math.Max(1d, _source.Width * scale);
        var height = Math.Max(1d, _source.Height * scale);
        _surface.Width = width;
        _surface.Height = height;
        _image.Width = width;
        _image.Height = height;
        _overlay.Width = width;
        _overlay.Height = height;
        _image.Source = ToBitmapSource(_source);
        Rebuild();
        NotifyStateChanged();
    }

    public void Clear()
    {
        _source?.Dispose();
        _source = null;
        _image.Source = null;
        _annotations.Clear();
        _redo.Clear();
        _tool = null;
        _drawing = false;
        _overlay.ReleaseMouseCapture();
        _overlay.Cursor = Cursors.Arrow;
        _overlay.Children.Clear();
        NotifyStateChanged();
    }

    public void SelectTool(CaptureAnnotationKind? tool)
    {
        _tool = tool;
        _overlay.Cursor = tool switch
        {
            null => Cursors.Arrow,
            CaptureAnnotationKind.Text => Cursors.IBeam,
            _ => Cursors.Cross
        };
        NotifyStateChanged();
    }

    public void SetColor(int argb)
    {
        _color = argb;
        NotifyStateChanged();
    }

    public void SetThickness(float thickness) => _thickness = Math.Clamp(thickness, 1, 24);

    public void Undo()
    {
        if (_annotations.Count == 0) return;
        var last = _annotations[^1];
        _annotations.RemoveAt(_annotations.Count - 1);
        _redo.Push(last);
        Rebuild();
        NotifyStateChanged();
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _annotations.Add(_redo.Pop());
        Rebuild();
        NotifyStateChanged();
    }

    public DrawingBitmap Render()
    {
        if (_source is null) throw new InvalidOperationException("Nenhuma imagem carregada.");
        return CaptureAnnotationRenderer.Render(
            _source,
            _annotations,
            Math.Max(1, _overlay.Width),
            Math.Max(1, _overlay.Height));
    }

    private void OverlayOnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_source is null || _tool is null) return;
        Focus();
        _start = Clamp(e.GetPosition(_overlay));
        if (_tool == CaptureAnnotationKind.Text)
        {
            var text = PromptForText();
            if (!string.IsNullOrWhiteSpace(text))
            {
                Add(new CaptureAnnotation
                {
                    Kind = CaptureAnnotationKind.Text,
                    Start = _start,
                    End = _start,
                    Text = text,
                    Argb = _color,
                    OutlineArgb = _color,
                    Thickness = _thickness,
                    Size = 28
                });
            }
            return;
        }
        if (_tool == CaptureAnnotationKind.Stamp)
        {
            var stamp = PromptForStamp();
            if (!string.IsNullOrWhiteSpace(stamp))
            {
                Add(new CaptureAnnotation
                {
                    Kind = CaptureAnnotationKind.Stamp,
                    Start = _start,
                    End = _start,
                    Text = stamp,
                    Size = 42
                });
            }
            return;
        }

        _drawing = true;
        _pencilPoints.Clear();
        _pencilPoints.Add(_start);
        _overlay.CaptureMouse();
        e.Handled = true;
    }

    private void OverlayOnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing || _tool is null) return;
        var end = Clamp(e.GetPosition(_overlay));
        if (_tool == CaptureAnnotationKind.Pencil) _pencilPoints.Add(end);
        Rebuild(CreateAnnotation(end));
    }

    private void OverlayOnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing || _tool is null) return;
        _drawing = false;
        _overlay.ReleaseMouseCapture();
        var end = Clamp(e.GetPosition(_overlay));
        if (_tool == CaptureAnnotationKind.Pencil) _pencilPoints.Add(end);
        Add(CreateAnnotation(end));
        e.Handled = true;
    }

    private CaptureAnnotation CreateAnnotation(Point end) => new()
    {
        Kind = _tool ?? CaptureAnnotationKind.Arrow,
        Start = _start,
        End = end,
        Points = [.. _pencilPoints],
        Argb = _color,
        OutlineArgb = _color,
        Thickness = _thickness,
        Opacity = _tool == CaptureAnnotationKind.Highlighter ? .38f : 1f
    };

    private void Add(CaptureAnnotation annotation)
    {
        _annotations.Add(annotation);
        _redo.Clear();
        Rebuild();
        NotifyStateChanged();
    }

    private void Rebuild(CaptureAnnotation? pending = null)
    {
        _overlay.Children.Clear();
        foreach (var annotation in _annotations) AddVisual(annotation);
        if (pending is not null) AddVisual(pending);
    }

    private void AddVisual(CaptureAnnotation annotation)
    {
        var color = DrawingColor.FromArgb(annotation.OutlineArgb ?? annotation.Argb);
        var brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        var thickness = annotation.Kind == CaptureAnnotationKind.Highlighter
            ? annotation.Thickness * 4
            : annotation.Thickness;
        if (annotation.Kind == CaptureAnnotationKind.Highlighter) brush.Opacity = .38;

        switch (annotation.Kind)
        {
            case CaptureAnnotationKind.Arrow:
            case CaptureAnnotationKind.Highlighter:
                _overlay.Children.Add(new Line
                {
                    X1 = annotation.Start.X,
                    Y1 = annotation.Start.Y,
                    X2 = annotation.End.X,
                    Y2 = annotation.End.Y,
                    Stroke = brush,
                    StrokeThickness = thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    IsHitTestVisible = false
                });
                if (annotation.Kind == CaptureAnnotationKind.Arrow)
                    _overlay.Children.Add(ArrowHead(annotation, brush));
                break;
            case CaptureAnnotationKind.Rectangle:
                var rectangle = new Rectangle
                {
                    Width = Math.Abs(annotation.End.X - annotation.Start.X),
                    Height = Math.Abs(annotation.End.Y - annotation.Start.Y),
                    Stroke = brush,
                    StrokeThickness = thickness,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(rectangle, Math.Min(annotation.Start.X, annotation.End.X));
                Canvas.SetTop(rectangle, Math.Min(annotation.Start.Y, annotation.End.Y));
                _overlay.Children.Add(rectangle);
                break;
            case CaptureAnnotationKind.Pencil:
                _overlay.Children.Add(new Polyline
                {
                    Points = new PointCollection(annotation.Points),
                    Stroke = brush,
                    StrokeThickness = thickness,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    IsHitTestVisible = false
                });
                break;
            case CaptureAnnotationKind.Text:
                var text = new TextBlock
                {
                    Text = annotation.Text,
                    Foreground = brush,
                    FontSize = annotation.Size,
                    FontWeight = FontWeights.SemiBold,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(text, annotation.Start.X);
                Canvas.SetTop(text, annotation.Start.Y);
                _overlay.Children.Add(text);
                break;
            case CaptureAnnotationKind.Stamp:
                var stamp = new TextBlock
                {
                    Text = annotation.Text,
                    FontFamily = new FontFamily("Segoe UI Emoji"),
                    FontSize = annotation.Size,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(stamp, annotation.Start.X - annotation.Size / 2);
                Canvas.SetTop(stamp, annotation.Start.Y - annotation.Size / 2);
                _overlay.Children.Add(stamp);
                break;
        }
    }

    private static Polygon ArrowHead(CaptureAnnotation annotation, Brush brush)
    {
        var angle = Math.Atan2(annotation.End.Y - annotation.Start.Y,
            annotation.End.X - annotation.Start.X);
        var length = Math.Max(13, annotation.Thickness * 4);
        var left = new Point(annotation.End.X - length * Math.Cos(angle - Math.PI / 6),
            annotation.End.Y - length * Math.Sin(angle - Math.PI / 6));
        var right = new Point(annotation.End.X - length * Math.Cos(angle + Math.PI / 6),
            annotation.End.Y - length * Math.Sin(angle + Math.PI / 6));
        return new Polygon
        {
            Points = new PointCollection([annotation.End, left, right]),
            Fill = brush,
            IsHitTestVisible = false
        };
    }

    private string? PromptForText()
    {
        var input = new TextBox { MinWidth = 310, Margin = new Thickness(0, 9, 0, 16) };
        input.SetResourceReference(StyleProperty, "Lab.Field");
        var dialog = CreatePrompt("Inserir texto", 410, 205);
        var panel = (StackPanel)dialog.Content;
        panel.Children.Add(new TextBlock { Text = "Texto da anotação" });
        panel.Children.Add(input);
        var insert = new Button { Content = "Inserir", HorizontalAlignment = HorizontalAlignment.Right };
        insert.SetResourceReference(StyleProperty, "Lab.PrimaryButton");
        insert.Click += (_, _) => dialog.DialogResult = true;
        panel.Children.Add(insert);
        dialog.Loaded += (_, _) => input.Focus();
        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    private string? PromptForStamp()
    {
        string? selected = null;
        var dialog = CreatePrompt("Inserir emoji", 430, 185);
        var panel = (StackPanel)dialog.Content;
        panel.Children.Add(new TextBlock { Text = "Escolha um emoji para posicionar" });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        foreach (var value in new[] { "😀", "👍", "✅", "⭐", "❤️", "⚠️" })
        {
            var button = new Button
            {
                Content = value,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 22,
                Width = 52,
                Height = 44,
                Margin = new Thickness(0, 0, 8, 0)
            };
            button.SetResourceReference(StyleProperty, "Lab.Button");
            button.Click += (_, _) => { selected = value; dialog.DialogResult = true; };
            row.Children.Add(button);
        }
        panel.Children.Add(row);
        dialog.ShowDialog();
        return selected;
    }

    private Window CreatePrompt(string title, double width, double height)
    {
        var dialog = new Window
        {
            Title = title,
            Owner = Window.GetWindow(this),
            Width = width,
            Height = height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (Brush)Application.Current.FindResource("Lab.raised"),
            Foreground = (Brush)Application.Current.FindResource("Lab.text"),
            Content = new StackPanel { Margin = new Thickness(24) }
        };
        dialog.SourceInitialized += (_, _) => ThemeService.ApplyToWindow(dialog);
        return dialog;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Redo();
            e.Handled = true;
        }
    }

    private Point Clamp(Point point) => new(
        Math.Clamp(point.X, 0, Math.Max(1, _overlay.Width)),
        Math.Clamp(point.Y, 0, Math.Max(1, _overlay.Height)));

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _source?.Dispose();
        _source = null;
    }

    private static BitmapSource ToBitmapSource(DrawingBitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr value);
}
