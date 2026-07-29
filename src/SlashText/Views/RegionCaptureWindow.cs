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
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace SlashText.Views;

public sealed class RegionCaptureWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Canvas _annotationLayer = new()
    {
        Background = Brushes.Transparent,
        Visibility = Visibility.Collapsed
    };
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
        Background = new SolidColorBrush(Color.FromArgb(242, 18, 25, 34)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(7),
        Padding = new Thickness(9, 5, 9, 5),
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false
    };
    private readonly Border _toolbar;
    private readonly Border[] _handles;
    private readonly DrawingBitmap _desktopBitmap;
    private readonly List<CaptureAnnotation> _annotations = [];
    private readonly Stack<CaptureAnnotation> _redo = new();
    private readonly Dictionary<CaptureAnnotationKind, Button> _toolButtons = [];
    private readonly List<Point> _pencilPoints = [];
    private Point _start;
    private Point _annotationStart;
    private Rect _localSelection;
    private bool _dragging;
    private bool _drawing;
    private bool _selectionReady;
    private CaptureAnnotationKind _tool = CaptureAnnotationKind.Arrow;
    private int _color = DrawingColor.Red.ToArgb();
    private float _thickness = 4;
    private int _nextNumber = 1;

    public DrawingBitmap? EditedBitmap { get; private set; }

    public RegionCaptureWindow()
    {
        Title = "Selecionar e editar região";
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

        _desktopBitmap = CaptureVirtualDesktopBitmap();
        _canvas.Children.Add(new Image
        {
            Source = ToBitmapSource(_desktopBitmap),
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
            Background = new SolidColorBrush(Color.FromArgb(242, 18, 25, 34)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(14, 9, 14, 9),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "Selecione uma região e edite sem sair desta tela  ·  Esc cancela",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13
            }
        };
        Canvas.SetLeft(help, 24);
        Canvas.SetTop(help, 24);
        _canvas.Children.Add(help);
        _canvas.Children.Add(_selection);

        _annotationLayer.ClipToBounds = true;
        _canvas.Children.Add(_annotationLayer);

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
        Closed += (_, _) => _desktopBitmap.Dispose();
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private Border BuildToolbar()
    {
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(ToolbarButton(
            "Capturar",
            "Finalizar usando a regra configurada",
            primary: true,
            (_, _) => Complete()));
        tools.Children.Add(Separator());
        tools.Children.Add(ToolButton("↗", "Seta", CaptureAnnotationKind.Arrow));
        tools.Children.Add(ToolButton("▰", "Marca-texto", CaptureAnnotationKind.Highlighter));
        tools.Children.Add(ToolButton("□", "Retângulo", CaptureAnnotationKind.Rectangle));
        tools.Children.Add(ToolButton("○", "Círculo", CaptureAnnotationKind.Ellipse));
        tools.Children.Add(ToolButton("✎", "Lápis", CaptureAnnotationKind.Pencil));
        tools.Children.Add(ToolButton("T", "Texto", CaptureAnnotationKind.Text));
        tools.Children.Add(ToolButton("①", "Número", CaptureAnnotationKind.Number));
        tools.Children.Add(Separator());

        foreach (var color in new[]
                 {
                     DrawingColor.Red,
                     DrawingColor.Gold,
                     DrawingColor.DeepSkyBlue,
                     DrawingColor.LimeGreen,
                     DrawingColor.White,
                     DrawingColor.Black
                 })
        {
            var choice = new Button
            {
                Width = 24,
                Height = 24,
                Margin = new Thickness(3, 9, 3, 9),
                Padding = new Thickness(0),
                Background = new SolidColorBrush(
                    Color.FromArgb(color.A, color.R, color.G, color.B)),
                BorderBrush = color == DrawingColor.White
                    ? new SolidColorBrush(Color.FromRgb(120, 130, 140))
                    : new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                BorderThickness = new Thickness(2),
                ToolTip = $"Cor {color.Name}",
                Tag = color.ToArgb(),
                Cursor = Cursors.Hand
            };
            choice.Click += (_, _) => _color = (int)choice.Tag;
            tools.Children.Add(choice);
        }

        var thickness = new ComboBox
        {
            Width = 72,
            Height = 34,
            Margin = new Thickness(7, 4, 3, 4),
            ToolTip = "Espessura",
            SelectedIndex = 1,
            Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromRgb(30, 40, 52)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(66, 80, 94))
        };
        foreach (var value in new[] { 2f, 4f, 8f, 12f })
        {
            thickness.Items.Add(new ComboBoxItem
            {
                Content = $"{value:0} px",
                Tag = value,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(30, 40, 52))
            });
        }
        thickness.SelectionChanged += (_, _) =>
        {
            if (thickness.SelectedItem is ComboBoxItem { Tag: float value })
            {
                _thickness = value;
            }
        };
        tools.Children.Add(thickness);
        tools.Children.Add(Separator());
        tools.Children.Add(ToolbarButton("↶", "Desfazer (Ctrl+Z)", false, (_, _) => Undo()));
        tools.Children.Add(ToolbarButton("↷", "Refazer (Ctrl+Y)", false, (_, _) => Redo()));
        tools.Children.Add(ToolbarButton("⟳", "Selecionar novamente", false, (_, _) => ResetSelection()));
        tools.Children.Add(ToolbarButton("×", "Cancelar", false, (_, _) => DialogResult = false));

        var toolbar = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = new SolidColorBrush(Color.FromArgb(250, 18, 25, 34)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(6),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 5,
                Opacity = .38,
                Color = Colors.Black
            },
            Child = tools
        };
        return toolbar;
    }

    private Button ToolButton(
        string text,
        string toolTip,
        CaptureAnnotationKind tool)
    {
        var button = ToolbarButton(text, toolTip, false, (_, _) =>
        {
            _tool = tool;
            Cursor = tool == CaptureAnnotationKind.Text
                ? Cursors.IBeam
                : Cursors.Cross;
            UpdateToolSelection();
        });
        _toolButtons[tool] = button;
        return button;
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
            MinWidth = primary ? 104 : 38,
            Height = 38,
            Margin = new Thickness(2),
            Padding = primary
                ? new Thickness(15, 5, 15, 5)
                : new Thickness(8, 5, 8, 5),
            Foreground = Brushes.White,
            Background = primary
                ? new SolidColorBrush(Color.FromRgb(10, 169, 187))
                : new SolidColorBrush(Color.FromRgb(24, 34, 45)),
            BorderBrush = primary
                ? new SolidColorBrush(Color.FromRgb(43, 201, 218))
                : new SolidColorBrush(Color.FromRgb(66, 80, 94)),
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
        Background = new SolidColorBrush(Color.FromRgb(66, 80, 94))
    };

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            IsToolbarSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var point = e.GetPosition(_canvas);
        if (_selectionReady)
        {
            if (!_localSelection.Contains(point))
            {
                return;
            }
            BeginAnnotation(point);
            e.Handled = true;
            return;
        }

        _start = point;
        _dragging = true;
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
        var point = e.GetPosition(_canvas);
        if (_dragging)
        {
            UpdateSelection(point);
        }
        else if (_drawing)
        {
            UpdatePendingAnnotation(point);
        }
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_drawing)
        {
            FinishAnnotation(e.GetPosition(_canvas));
            e.Handled = true;
            return;
        }
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
        PositionAnnotationLayer();
        SetHandlesVisibility(Visibility.Visible);
        PositionHandles();
        PositionToolbar();
        UpdateToolSelection();
        _toolbar.Visibility = Visibility.Visible;
        Cursor = Cursors.Cross;
        e.Handled = true;
    }

    private void BeginAnnotation(Point canvasPoint)
    {
        var local = ToAnnotationPoint(canvasPoint);
        if (_tool == CaptureAnnotationKind.Text)
        {
            AddInlineTextEditor(local);
            return;
        }
        if (_tool == CaptureAnnotationKind.Number)
        {
            Add(new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Number,
                Start = local,
                End = local,
                Text = (_nextNumber++).ToString(),
                Argb = _color,
                Thickness = _thickness
            });
            return;
        }

        _annotationStart = local;
        _drawing = true;
        _pencilPoints.Clear();
        _pencilPoints.Add(local);
        CaptureMouse();
    }

    private void UpdatePendingAnnotation(Point canvasPoint)
    {
        var end = ToAnnotationPoint(canvasPoint);
        if (_tool == CaptureAnnotationKind.Pencil)
        {
            _pencilPoints.Add(end);
        }
        Rebuild(new CaptureAnnotation
        {
            Kind = _tool,
            Start = _annotationStart,
            End = end,
            Points = [.. _pencilPoints],
            Argb = _color,
            Thickness = _thickness
        });
    }

    private void FinishAnnotation(Point canvasPoint)
    {
        _drawing = false;
        ReleaseMouseCapture();
        var end = ToAnnotationPoint(canvasPoint);
        if (_tool == CaptureAnnotationKind.Pencil)
        {
            _pencilPoints.Add(end);
        }
        Add(new CaptureAnnotation
        {
            Kind = _tool,
            Start = _annotationStart,
            End = end,
            Points = [.. _pencilPoints],
            Argb = _color,
            Thickness = _thickness
        });
    }

    private void AddInlineTextEditor(Point point)
    {
        var input = new TextBox
        {
            MinWidth = 180,
            MaxWidth = Math.Max(180, _localSelection.Width - point.X),
            Padding = new Thickness(7, 5, 7, 5),
            FontSize = 16,
            Foreground = Brushes.White,
            CaretBrush = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(230, 18, 25, 34)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(43, 201, 218)),
            BorderThickness = new Thickness(1)
        };
        Canvas.SetLeft(input, point.X);
        Canvas.SetTop(input, point.Y);
        _annotationLayer.Children.Add(input);
        input.Focus();
        input.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                CommitInlineText(input, point);
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                _annotationLayer.Children.Remove(input);
                args.Handled = true;
            }
        };
        input.LostKeyboardFocus += (_, _) =>
        {
            if (_annotationLayer.Children.Contains(input))
            {
                CommitInlineText(input, point);
            }
        };
    }

    private void CommitInlineText(TextBox input, Point point)
    {
        _annotationLayer.Children.Remove(input);
        if (!string.IsNullOrWhiteSpace(input.Text))
        {
            Add(new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Text,
                Start = point,
                End = point,
                Text = input.Text.Trim(),
                Argb = _color,
                Thickness = _thickness
            });
        }
    }

    private void Add(CaptureAnnotation annotation)
    {
        _annotations.Add(annotation);
        _redo.Clear();
        Rebuild();
    }

    private void Undo()
    {
        if (_annotations.Count == 0)
        {
            return;
        }
        var last = _annotations[^1];
        _annotations.RemoveAt(_annotations.Count - 1);
        _redo.Push(last);
        Rebuild();
    }

    private void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }
        _annotations.Add(_redo.Pop());
        Rebuild();
    }

    private void Rebuild(CaptureAnnotation? pending = null)
    {
        _annotationLayer.Children.Clear();
        foreach (var annotation in _annotations)
        {
            AddVisual(annotation);
        }
        if (pending is not null)
        {
            AddVisual(pending);
        }
    }

    private void AddVisual(CaptureAnnotation annotation)
    {
        var color = DrawingColor.FromArgb(annotation.Argb);
        var brush = new SolidColorBrush(
            Color.FromArgb(color.A, color.R, color.G, color.B));
        var thickness = annotation.Kind == CaptureAnnotationKind.Highlighter
            ? annotation.Thickness * 4
            : annotation.Thickness;
        if (annotation.Kind == CaptureAnnotationKind.Highlighter)
        {
            brush.Opacity = .38;
        }

        switch (annotation.Kind)
        {
            case CaptureAnnotationKind.Arrow:
            case CaptureAnnotationKind.Highlighter:
                _annotationLayer.Children.Add(new Line
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
                {
                    _annotationLayer.Children.Add(ArrowHead(annotation, brush));
                }
                break;
            case CaptureAnnotationKind.Rectangle:
            case CaptureAnnotationKind.Ellipse:
                var shape = annotation.Kind == CaptureAnnotationKind.Rectangle
                    ? (Shape)new Rectangle()
                    : new Ellipse();
                shape.Stroke = brush;
                shape.StrokeThickness = thickness;
                shape.Width = Math.Abs(annotation.End.X - annotation.Start.X);
                shape.Height = Math.Abs(annotation.End.Y - annotation.Start.Y);
                shape.IsHitTestVisible = false;
                Canvas.SetLeft(shape, Math.Min(annotation.Start.X, annotation.End.X));
                Canvas.SetTop(shape, Math.Min(annotation.Start.Y, annotation.End.Y));
                _annotationLayer.Children.Add(shape);
                break;
            case CaptureAnnotationKind.Pencil:
                _annotationLayer.Children.Add(new Polyline
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
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(text, annotation.Start.X);
                Canvas.SetTop(text, annotation.Start.Y);
                _annotationLayer.Children.Add(text);
                break;
            case CaptureAnnotationKind.Number:
                var badge = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(15),
                    Background = brush,
                    IsHitTestVisible = false,
                    Child = new TextBlock
                    {
                        Text = annotation.Text,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                Canvas.SetLeft(badge, annotation.Start.X - 15);
                Canvas.SetTop(badge, annotation.Start.Y - 15);
                _annotationLayer.Children.Add(badge);
                break;
        }
    }

    private static Polygon ArrowHead(CaptureAnnotation annotation, Brush brush)
    {
        var angle = Math.Atan2(
            annotation.End.Y - annotation.Start.Y,
            annotation.End.X - annotation.Start.X);
        const double length = 15;
        var left = new Point(
            annotation.End.X - length * Math.Cos(angle - Math.PI / 6),
            annotation.End.Y - length * Math.Sin(angle - Math.PI / 6));
        var right = new Point(
            annotation.End.X - length * Math.Cos(angle + Math.PI / 6),
            annotation.End.Y - length * Math.Sin(angle + Math.PI / 6));
        return new Polygon
        {
            Points = new PointCollection([annotation.End, left, right]),
            Fill = brush,
            IsHitTestVisible = false
        };
    }

    private void UpdateToolSelection()
    {
        foreach (var (tool, button) in _toolButtons)
        {
            var selected = tool == _tool;
            button.Background = new SolidColorBrush(selected
                ? Color.FromRgb(13, 92, 104)
                : Color.FromRgb(24, 34, 45));
            button.BorderBrush = new SolidColorBrush(selected
                ? Color.FromRgb(43, 201, 218)
                : Color.FromRgb(66, 80, 94));
            button.Foreground = Brushes.White;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter &&
                 _selectionReady &&
                 e.OriginalSource is not TextBox)
        {
            Complete();
            e.Handled = true;
        }
        else if (e.Key == Key.R &&
                 _selectionReady &&
                 Keyboard.Modifiers == ModifierKeys.None)
        {
            ResetSelection();
            e.Handled = true;
        }
        else if (e.Key == Key.Z &&
                 Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y &&
                 Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            Redo();
            e.Handled = true;
        }
    }

    private void Complete()
    {
        if (!_selectionReady)
        {
            return;
        }

        using var crop = CropFrozenSelection();
        EditedBitmap?.Dispose();
        EditedBitmap = CaptureAnnotationRenderer.Render(
            crop,
            _annotations,
            _localSelection.Width,
            _localSelection.Height);
        DialogResult = true;
    }

    private DrawingBitmap CropFrozenSelection()
    {
        var scaleX = _desktopBitmap.Width / Math.Max(1d, Width);
        var scaleY = _desktopBitmap.Height / Math.Max(1d, Height);
        var left = Math.Clamp(
            (int)Math.Round(_localSelection.Left * scaleX),
            0,
            _desktopBitmap.Width - 1);
        var top = Math.Clamp(
            (int)Math.Round(_localSelection.Top * scaleY),
            0,
            _desktopBitmap.Height - 1);
        var width = Math.Clamp(
            (int)Math.Round(_localSelection.Width * scaleX),
            1,
            _desktopBitmap.Width - left);
        var height = Math.Clamp(
            (int)Math.Round(_localSelection.Height * scaleY),
            1,
            _desktopBitmap.Height - top);
        return _desktopBitmap.Clone(
            new System.Drawing.Rectangle(left, top, width, height),
            DrawingPixelFormat.Format32bppArgb);
    }

    private void ResetSelection()
    {
        _dragging = false;
        _drawing = false;
        _selectionReady = false;
        ReleaseMouseCapture();
        _selection.Visibility = Visibility.Collapsed;
        _annotationLayer.Visibility = Visibility.Collapsed;
        _annotationLayer.Children.Clear();
        _sizeBadge.Visibility = Visibility.Collapsed;
        _toolbar.Visibility = Visibility.Collapsed;
        SetHandlesVisibility(Visibility.Collapsed);
        _annotations.Clear();
        _redo.Clear();
        _nextNumber = 1;
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
            var scaleX = _desktopBitmap.Width / Math.Max(1d, Width);
            var scaleY = _desktopBitmap.Height / Math.Max(1d, Height);
            size.Text =
                $"{Math.Round(_localSelection.Width * scaleX):N0} × " +
                $"{Math.Round(_localSelection.Height * scaleY):N0}";
        }
        Canvas.SetLeft(_sizeBadge, _localSelection.Left);
        Canvas.SetTop(
            _sizeBadge,
            _localSelection.Top > 40
                ? _localSelection.Top - 34
                : _localSelection.Bottom + 8);
    }

    private void PositionAnnotationLayer()
    {
        Canvas.SetLeft(_annotationLayer, _localSelection.Left);
        Canvas.SetTop(_annotationLayer, _localSelection.Top);
        _annotationLayer.Width = _localSelection.Width;
        _annotationLayer.Height = _localSelection.Height;
        _annotationLayer.Visibility = Visibility.Visible;
    }

    private Point ToAnnotationPoint(Point canvasPoint) => new(
        Math.Clamp(canvasPoint.X - _localSelection.Left, 0, _localSelection.Width),
        Math.Clamp(canvasPoint.Y - _localSelection.Top, 0, _localSelection.Height));

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
            _localSelection.Left + ((_localSelection.Width - desired.Width) / 2),
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
        BorderBrush = new SolidColorBrush(Color.FromRgb(10, 169, 187)),
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

    private static Rect Normalize(Point start, Point end) => new(
        Math.Min(start.X, end.X),
        Math.Min(start.Y, end.Y),
        Math.Abs(end.X - start.X),
        Math.Abs(end.Y - start.Y));

    private static DrawingBitmap CaptureVirtualDesktopBitmap()
    {
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        var bitmap = new DrawingBitmap(
            Math.Max(1, virtualScreen.Width),
            Math.Max(1, virtualScreen.Height),
            DrawingPixelFormat.Format32bppPArgb);
        using var graphics = DrawingGraphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            virtualScreen.Left,
            virtualScreen.Top,
            0,
            0,
            bitmap.Size,
            System.Drawing.CopyPixelOperation.SourceCopy);
        return bitmap;
    }

    private static BitmapSource ToBitmapSource(DrawingBitmap bitmap)
    {
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
