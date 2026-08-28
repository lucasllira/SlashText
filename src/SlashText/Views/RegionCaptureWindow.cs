using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SlashText.Services;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using Forms = System.Windows.Forms;

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
        Stroke = new SolidColorBrush(Color.FromRgb(39, 200, 218)),
        StrokeThickness = 2,
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
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(7),
        Padding = new Thickness(9, 5, 9, 5),
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false
    };
    private readonly Border _toolbar;
    private readonly Window _toolbarWindow;
    private readonly Border[] _handles;
    private readonly DrawingBitmap _desktopBitmap;
    private readonly List<CaptureAnnotation> _annotations = [];
    private readonly Stack<CaptureAnnotation> _redo = new();
    private readonly Dictionary<CaptureAnnotationKind, Button> _toolButtons = [];
    private readonly List<Point> _pencilPoints = [];
    private readonly bool _isDark = ThemeService.IsDark;
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
    private Grid _toolbarLayout = null!;
    private bool _toolbarPositionPending;

    public DrawingBitmap? EditedBitmap { get; private set; }

    public RegionCaptureWindow(bool includeCursor = false)
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

        _desktopBitmap = CaptureVirtualDesktopBitmap(includeCursor);
        _sizeBadge.Background = Brush(
            _isDark ? "#F2121922" : "#F8FFFFFF");
        _sizeBadge.BorderBrush = Brush(
            _isDark ? "#6EFFFFFF" : "#CBD5DF");
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
            Background = Brush(_isDark ? "#F2121922" : "#F8FFFFFF"),
            BorderBrush = Brush(_isDark ? "#64FFFFFF" : "#CBD5DF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(14, 9, 14, 9),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "Selecione uma região e edite sem sair desta tela  ·  Esc cancela",
                Foreground = Brush(_isDark ? "#F5F8FA" : "#17212B"),
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13
            }
        };
        Canvas.SetLeft(help, 24);
        Canvas.SetTop(help, 24);
        _canvas.Children.Add(help);
        _canvas.Children.Add(_selection);

        _annotationLayer.ClipToBounds = true;
        _annotationLayer.Cursor = Cursors.Cross;
        _annotationLayer.MouseLeftButtonDown += OnAnnotationMouseDown;
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
            Foreground = Brush(_isDark ? "#F5F8FA" : "#17212B"),
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12
        };
        _canvas.Children.Add(_sizeBadge);

        _toolbar = BuildToolbar();
        _toolbarWindow = new Window
        {
            Title = "Ferramentas de captura",
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            SizeToContent = SizeToContent.Height,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            Opacity = 0,
            Content = _toolbar
        };
        _toolbarWindow.DpiChanged += (_, _) => RequestToolbarPosition();
        Content = _canvas;

        Loaded += (_, _) =>
        {
            _toolbarWindow.Owner = this;
            UpdateShade(default);
        };
        Closed += (_, _) =>
        {
            if (_toolbarWindow.IsVisible)
            {
                _toolbarWindow.Close();
            }
            _desktopBitmap.Dispose();
        };
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private Border BuildToolbar()
    {
        var root = new Grid();
        _toolbarLayout = root;
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var tools = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        tools.Children.Add(ToolbarButton(
            "Capturar",
            "Finalizar usando a regra configurada",
            primary: true,
            (_, _) => Complete()));
        tools.Children.Add(Separator());
        tools.Children.Add(ToolButton("Seta", "Desenhar seta", CaptureAnnotationKind.Arrow));
        tools.Children.Add(ToolButton("Marca-texto", "Realçar uma área", CaptureAnnotationKind.Highlighter));
        tools.Children.Add(ToolButton("Retângulo", "Desenhar retângulo", CaptureAnnotationKind.Rectangle));
        tools.Children.Add(ToolButton("Elipse", "Desenhar elipse", CaptureAnnotationKind.Ellipse));
        tools.Children.Add(ToolButton("Lápis", "Desenho livre", CaptureAnnotationKind.Pencil));
        tools.Children.Add(ToolButton("Texto", "Inserir texto", CaptureAnnotationKind.Text));
        tools.Children.Add(ToolButton("Número", "Inserir marcador numerado", CaptureAnnotationKind.Number));
        root.Children.Add(tools);

        var options = new WrapPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 3, 0, 0)
        };
        options.Children.Add(new TextBlock
        {
            Text = "Cor",
            Foreground = Brush(_isDark ? "#F5F8FA" : "#25313D"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(7, 0, 5, 0),
            FontSize = 12
        });
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
                    ? Brush("#78828C")
                    : Brush(_isDark ? "#64FFFFFF" : "#94A3AF"),
                BorderThickness = new Thickness(2),
                ToolTip = $"Cor {color.Name}",
                Tag = color.ToArgb(),
                Cursor = Cursors.Hand
            };
            choice.Click += (_, _) => _color = (int)choice.Tag;
            options.Children.Add(choice);
        }

        var thickness = new ComboBox
        {
            Width = 72,
            Height = 34,
            Margin = new Thickness(7, 4, 3, 4),
            ToolTip = "Espessura",
            SelectedIndex = 1,
            Foreground = Brush(_isDark ? "#F5F8FA" : "#17212B"),
            Background = Brush(_isDark ? "#1E2834" : "#FFFFFF"),
            BorderBrush = Brush(_isDark ? "#42505E" : "#CBD5DF")
        };
        foreach (var value in new[] { 2f, 4f, 8f, 12f })
        {
            thickness.Items.Add(new ComboBoxItem
            {
                Content = $"{value:0} px",
                Tag = value,
                Foreground = Brush(_isDark ? "#F5F8FA" : "#17212B"),
                Background = Brush(_isDark ? "#1E2834" : "#FFFFFF")
            });
        }
        thickness.SelectionChanged += (_, _) =>
        {
            if (thickness.SelectedItem is ComboBoxItem { Tag: float value })
            {
                _thickness = value;
            }
        };
        options.Children.Add(thickness);
        options.Children.Add(Separator());
        options.Children.Add(ToolbarButton("Desfazer", "Desfazer (Ctrl+Z)", false, (_, _) => Undo()));
        options.Children.Add(ToolbarButton("Refazer", "Refazer (Ctrl+Y)", false, (_, _) => Redo()));
        options.Children.Add(ToolbarButton("Refazer seleção", "Selecionar novamente (R)", false, (_, _) => ResetSelection()));
        options.Children.Add(ToolbarButton("Cancelar", "Cancelar captura (Esc)", false, (_, _) => DialogResult = false));
        Grid.SetRow(options, 1);
        root.Children.Add(options);

        var toolbar = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = Brush(_isDark ? "#FA121922" : "#FCF8FAFC"),
            BorderBrush = Brush(_isDark ? "#6EFFFFFF" : "#C6D1DA"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(7),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 5,
                Opacity = _isDark ? .32 : .16,
                Color = Colors.Black
            },
            Child = root
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

    private Button ToolbarButton(
        string text,
        string toolTip,
        bool primary,
        RoutedEventHandler click)
    {
        var button = new Button
        {
            Content = text,
            ToolTip = toolTip,
            MinWidth = primary ? 104 : 58,
            Height = 38,
            Margin = new Thickness(2),
            Padding = primary
                ? new Thickness(15, 5, 15, 5)
                : new Thickness(8, 5, 8, 5),
            Foreground = primary
                ? Brushes.White
                : Brush(_isDark ? "#F5F8FA" : "#25313D"),
            Background = primary
                ? Brush("#0AA9BB")
                : Brush(_isDark ? "#18222D" : "#FFFFFF"),
            BorderBrush = primary
                ? Brush("#2BC9DA")
                : Brush(_isDark ? "#42505E" : "#CBD5DF"),
            BorderThickness = new Thickness(1),
            FontSize = 12,
            FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
            Cursor = Cursors.Hand
        };
        button.Click += click;
        return button;
    }

    private Border Separator() => new()
    {
        Width = 1,
        Height = 26,
        Margin = new Thickness(5, 7, 5, 7),
        Background = Brush(_isDark ? "#42505E" : "#D5DDE4")
    };

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled ||
            e.ChangedButton != MouseButton.Left ||
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
        _toolbarWindow.Hide();
        SetHandlesVisibility(Visibility.Collapsed);
        _selection.Visibility = Visibility.Visible;
        _sizeBadge.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_start);
        e.Handled = true;
    }

    private void OnAnnotationMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_selectionReady ||
            e.ChangedButton != MouseButton.Left ||
            IsTextInputSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var local = e.GetPosition(_annotationLayer);
        var canvasPoint = new Point(
            _localSelection.Left + local.X,
            _localSelection.Top + local.Y);
        BeginAnnotation(canvasPoint);
        e.Handled = true;
    }

    private static bool IsTextInputSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is TextBox)
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
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
        _toolbar.Visibility = Visibility.Visible;
        RequestToolbarPosition();
        UpdateToolSelection();
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
            Foreground = Brush(_isDark ? "#FFFFFF" : "#17212B"),
            CaretBrush = Brush(_isDark ? "#FFFFFF" : "#17212B"),
            Background = Brush(_isDark ? "#F0121922" : "#F8FFFFFF"),
            BorderBrush = Brush("#2BC9DA"),
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
            button.Background = Brush(selected
                ? _isDark ? "#0D5C68" : "#DDF6F8"
                : _isDark ? "#18222D" : "#FFFFFF");
            button.BorderBrush = Brush(selected
                ? "#2BC9DA"
                : _isDark ? "#42505E" : "#CBD5DF");
            button.Foreground = Brush(
                _isDark ? "#F5F8FA" : selected ? "#075A66" : "#25313D");
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

        _toolbarWindow.Hide();
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
        _toolbarWindow.Hide();
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

    private void RequestToolbarPosition()
    {
        if (!_selectionReady || _toolbarPositionPending)
        {
            return;
        }
        _toolbarPositionPending = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _toolbarPositionPending = false;
            PositionToolbarAfterLayout();
        }));
    }

    private void PositionToolbarAfterLayout()
    {
        if (!_selectionReady)
        {
            return;
        }

        var selectionPixels = SelectionInPhysicalPixels();
        var monitor = MonitorWorkAreaProvider.FromSelection(selectionPixels);
        var marginPixels = Math.Max(1, 12 * monitor.DpiScaleX);
        var maximumWidthPixels = Math.Max(1, monitor.WorkAreaPixels.Width - (marginPixels * 2));
        var maximumWidthDips = maximumWidthPixels / monitor.DpiScaleX;

        _toolbar.Width = double.NaN;
        _toolbar.MaxWidth = double.PositiveInfinity;
        _toolbarLayout.Width = double.NaN;
        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var naturalDips = _toolbar.DesiredSize;

        var finalWidthDips = Math.Min(naturalDips.Width, maximumWidthDips);
        _toolbar.Width = finalWidthDips;
        _toolbar.MaxWidth = finalWidthDips;
        _toolbarLayout.Width = Math.Max(1, finalWidthDips -
            _toolbar.Padding.Left - _toolbar.Padding.Right -
            _toolbar.BorderThickness.Left - _toolbar.BorderThickness.Right);
        _toolbarWindow.Width = finalWidthDips;
        _toolbarWindow.Opacity = 0;
        if (!_toolbarWindow.IsVisible)
        {
            _toolbarWindow.Show();
        }
        _toolbarWindow.UpdateLayout();

        var finalDips = new Size(
            Math.Max(1, _toolbar.ActualWidth),
            Math.Max(1, _toolbar.ActualHeight));
        var finalPixels = new Size(
            Math.Ceiling(finalDips.Width * monitor.DpiScaleX),
            Math.Ceiling(finalDips.Height * monitor.DpiScaleY));
        var placement = ToolbarPlacementCalculator.Calculate(
            selectionPixels,
            monitor.WorkAreaPixels,
            finalPixels,
            naturalDips.Width * monitor.DpiScaleX,
            dpiScale: Math.Max(monitor.DpiScaleX, monitor.DpiScaleY));

        var handle = new WindowInteropHelper(_toolbarWindow).Handle;
        _ = SetWindowPos(
            handle,
            new nint(-1),
            (int)Math.Round(placement.Bounds.Left),
            (int)Math.Round(placement.Bounds.Top),
            Math.Max(1, (int)Math.Ceiling(placement.Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(placement.Bounds.Height)),
            SwpNoActivate | SwpShowWindow);
        _toolbarWindow.Opacity = 1;

        SafeDiagnosticLog.Write("capture.toolbar-positioned", new Dictionary<string, object?>
        {
            ["selectionPixels"] = RectDescription(selectionPixels),
            ["workAreaPixels"] = RectDescription(monitor.WorkAreaPixels),
            ["dpiScaleX"] = monitor.DpiScaleX,
            ["dpiScaleY"] = monitor.DpiScaleY,
            ["naturalWidthDips"] = naturalDips.Width,
            ["finalWidthDips"] = finalDips.Width,
            ["finalHeightDips"] = finalDips.Height,
            ["finalBoundsPixels"] = RectDescription(placement.Bounds),
            ["placement"] = placement.Side.ToString(),
            ["layoutMode"] = placement.Mode.ToString(),
            ["expectedRows"] = placement.ExpectedRows
        });
    }

    private Rect SelectionInPhysicalPixels()
    {
        var virtualScreen = Forms.SystemInformation.VirtualScreen;
        var canvasWidth = Math.Max(1, _canvas.ActualWidth);
        var canvasHeight = Math.Max(1, _canvas.ActualHeight);
        var scaleX = virtualScreen.Width / canvasWidth;
        var scaleY = virtualScreen.Height / canvasHeight;
        return new Rect(
            virtualScreen.Left + (_localSelection.Left * scaleX),
            virtualScreen.Top + (_localSelection.Top * scaleY),
            _localSelection.Width * scaleX,
            _localSelection.Height * scaleY);
    }

    private static string RectDescription(Rect rectangle) =>
        $"{rectangle.Left:0},{rectangle.Top:0},{rectangle.Width:0},{rectangle.Height:0}";

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

    private static SolidColorBrush Brush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));

    private static DrawingBitmap CaptureVirtualDesktopBitmap() =>
        CaptureVirtualDesktopBitmap(false);

    private static DrawingBitmap CaptureVirtualDesktopBitmap(bool includeCursor)
    {
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        return CaptureService.CaptureBitmap(virtualScreen, includeCursor);
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

    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
