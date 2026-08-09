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

    public DrawingBitmap? EditedBitmap { get; private set; }

    public RegionCaptureWindow(bool includeCursor = false)
    {
        Title = "Selecionar e editar regiÃ£o";
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
                Text = "Selecione uma regiÃ£o e edite sem sair desta tela  Â·  Esc cancela",
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
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        tools.Children.Add(ToolbarButton(
            "Capturar",
            "Finalizar usando a regra configurada",
            primary: true,
            (_, _) => Complete()));
        tools.Children.Add(Separator());
        tools.Children.Add(ToolButton("Seta", "Desenhar seta", CaptureAnnotationKind.Arrow));
        tools.Children.Add(ToolButton("Marca-texto", "RealÃ§ar uma Ã¡rea", CaptureAnnotationKind.Highlighter));
        tools.Children.Add(ToolButton("RetÃ¢ngulo", "Desenhar retÃ¢ngulo", CaptureAnnotationKind.Rectangle));
        tools.Children.Add(ToolButton("Elipse", "Desenhar elipse", CaptureAnnotationKind.Ellipse));
        tools.Children.Add(ToolButton("LÃ¡pis", "Desenho livre", CaptureAnnotationKind.Pencil));
        tools.Children.Add(ToolButton("Texto", "Inserir texto", CaptureAnnotationKind.Text));
        tools.Children.Add(ToolButton("NÃºmero", "Inserir marcador numerado", CaptureAnnotationKind.Number));
        root.Children.Add(tools);

        var options = new StackPanel
        {
            Orientation = Orientation.Horizontal,
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
        options.Children.Add(ToolbarButton("Refazer seleÃ§Ã£o", "Selecionar novamente (R)", false, (_, _) => ResetSelection()));
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
        _toolbar.Visibility = Visibility.Collapsed;
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
      ×ßw¶‰žËkºwµçUÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹ÉÉ½Üè4(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹!¥¡±¥¡Ñ•Èè4(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡¹•Ü1¥¹”4(€€€€€€€€€€€€€€€ì4(€€€€€€€€€€€€€€€€€€€`Ä€ô…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`°4(€€€€€€€€€€€€€€€€€€€dÄ€ô…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d°4(€€€€€€€€€€€€€€€€€€€`È€ô…¹¹½Ñ…Ñ¥½¸¹¹¹`°4(€€€€€€€€€€€€€€€€€€€dÈ€ô…¹¹½Ñ…Ñ¥½¸¹¹¹d°4(€€€€€€€€€€€€€€€€€€€MÑÉ½­”€ô‰ÉÕÍ °4(€€€€€€€€€€€€€€€€€€€MÑÉ½­•Q¡¥­¹•ÍÌ€ôÑ¡¥­¹•ÍÌ°4(€€€€€€€€€€€€€€€€€€€MÑÉ½­•MÑ…ÉÑ1¥¹•…À€ôA•¹1¥¹•…À¹I½Õ¹°4(€€€€€€€€€€€€€€€€€€€MÑÉ½­•¹‘1¥¹•…À€ôA•¹1¥¹•…À¹I½Õ¹°4(€€€€€€€€€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”4(€€€€€€€€€€€€€€€ô¤ì4(€€€€€€€€€€€€€€€¥˜€¡…¹¹½Ñ…Ñ¥½¸¹-¥¹€ôô…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹ÉÉ½Ü¤4(€€€€€€€€€€€€€€€ì4(€€€€€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡ÉÉ½Ý!•…¡…¹¹½Ñ…Ñ¥½¸°‰ÉÕÍ ¤¤ì4(€€€€€€€€€€€€€€€ô4(€€€€€€€€€€€€€€€‰É•…¬ì4(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹I•Ñ…¹±”è4(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹±±¥ÁÍ”è4(€€€€€€€€€€€€€€€Ù…ÈÍ¡…Á”€ô…¹¹½Ñ…Ñ¥½¸¹-¥¹€ôô…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹I•Ñ…¹±”4(€€€€€€€€€€€€€€€€€€€€ü€¡M¡…Á”¥¹•ÜI•Ñ…¹±” ¤4(€€€€€€€€€€€€€€€€€€€€è¹•Ü±±¥ÁÍ” ¤ì4(€€€€€€€€€€€€€€€Í¡…Á”¹MÑÉ½­”€ô‰ÉÕÍ ì4(€€€€€€€€€€€€€€€Í¡…Á”¹MÑÉ½­•Q¡¥­¹•ÍÌ€ôÑ¡¥­¹•ÍÌì4(€€€€€€€€€€€€€€€Í¡…Á”¹]¥‘Ñ €ô5…Ñ ¹‰Ì¡…¹¹½Ñ…Ñ¥½¸¹¹¹`€´…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`¤ì4(€€€€€€€€€€€€€€€Í¡…Á”¹!•¥¡Ð€ô5…Ñ ¹‰Ì¡…¹¹½Ñ…Ñ¥½¸¹¹¹d€´…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d¤ì4(€€€€€€€€€€€€€€€Í¡…Á”¹%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”ì4(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡Í¡…Á”°5…Ñ ¹5¥¸¡…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`°…¹¹½Ñ…Ñ¥½¸¹¹¹`¤¤ì4(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡Í¡…Á”°5…Ñ ¹5¥¸¡…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d°…¹¹½Ñ…Ñ¥½¸¹¹¹d¤¤ì4(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡Í¡…Á”¤ì4(€€€€€€€€€€€€€€€‰É•…¬ì4(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹A•¹¥°è4(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡¹•ÜA½±å±¥¹”4(€€€€€€€€€€€€€€€ì4(€€€€€€€€€€€€€€€€€€€A½¥¹ÑÌ€ô¹•ÜA½¥¹Ñ½±±•Ñ¥½¸¡…¹¹½Ñ…Ñ¥½¸¹A½¥¹ÑÌ¤°4(€€€€€€€€€€€€€€€€€€€MÑÉ½­”€ô‰ÉÕÍ °4(€€€€€€€€€€€€€€€€€€€MÑÉ½­•Q¡¥­¹•ÍÌ€ôÑ¡¥­¹•ÍÌ°4(€€€€€€€€€€€€€€€€€€€MÑÉ½­•1¥¹•)½¥¸€ôA•¹1¥¹•)½¥¸¹I½Õ¹°4(€€€€€€€€€€€€€€€€€€€MÑÉ½­•MÑ…ÉÑ1¥¹•…À€ôA•¹1¥¹•…À¹I½Õ¹°4(€€€€€€€€€€€€€€€€€€€MÑÉ½­•¹‘1¥¹•…À€ôA•¹1¥¹•…À¹I½Õ¹°4(€€€€€€€€€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”4(€€€€€€€€€€€€€€€ô¤ì4(€€€€€€€€€€€€€€€‰É•…¬ì4(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹Q•áÐè4(€€€€€€€€€€€€€€€Ù…ÈÑ•áÐ€ô¹•ÜQ•áÑ	±½¬4(€€€€€€€€€€€€€€€ì4(€€€€€€€€€€€€€€€€€€€Q•áÐ€ô…¹¹½Ñ…Ñ¥½¸¹Q•áÐ°4(€€€€€€€€€€€€€€€€€€€½É•É½Õ¹€ô‰ÉÕÍ °4(€€€€€€€€€€€€€€€€€€€½¹ÑM¥é”€ô€ÄÜ°4(€€€€€€€€€€€€€€€€€€€½¹Ñ]•¥¡Ð€ô½¹Ñ]•¥¡ÑÌ¹	½±°4(€€€€€€€€€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”4(€€€€€€€€€€€€€€€ôì4(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡Ñ•áÐ°…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`¤ì4(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡Ñ•áÐ°…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d¤ì4(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡Ñ•áÐ¤ì4(€€€€€€€€€€€€€€€‰É•…¬ì4(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹9Õµ‰•Èè4(€€€€€€€€€€€€€€€Ù…È‰…‘”€ô¹•Ü	½É‘•È4(€€€€€€€€€€€€€€€ì4(€€€€€€€€€€€€€€€€€€€]¥‘Ñ €ô€ÌÀ°4(€€€€€€€€€€€€€€€€€€€!•¥¡Ð€ô€ÌÀ°4(€€€€€€€€€€€€€€€€€€€½É¹•ÉI…‘¥ÕÌ€ô¹•Ü½É¹•ÉI…‘¥ÕÌ ÄÔ¤°4(€€€€€€€€€€€€€€€€€€€	…­É½Õ¹€ô‰ÉÕÍ °4(€€€€€€€€€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”°4(€€€€€€€€€€€€€€€€€€€¡¥±€ô¹•ÜQ•áÑ	±½¬4(€€€€€€€€€€€€€€€€€€€ì4(€€€€€€€€€€€€€€€€€€€€€€€Q•áÐ€ô…¹¹½Ñ…Ñ¥½¸¹Q•áÐ°4(€€€€€€€€€€€€€€€€€€€€€€€½É•É½Õ¹€ô	ÉÕÍ¡•Ì¹]¡¥Ñ”°4(€€€€€€€€€€€€€€€€€€€€€€€½¹Ñ]•¥¡Ð€ô½¹Ñ]•¥¡ÑÌ¹	½±°4(€€€€€€€€€€€€€€€€€€€€€€€!½É¥é½¹Ñ…±±¥¹µ•¹Ð€ô!½É¥é½¹Ñ…±±¥¹µ•¹Ð¹•¹Ñ•È°4(€€€€€€€€€€€€€€€€€€€€€€€Y•ÉÑ¥…±±¥¹µ•¹Ð€ôY•ÉÑ¥…±±¥¹µ•¹Ð¹•¹Ñ•È4(€€€€€€€€€€€€€€€€€€€ô4(€€€€€€€€€€€€€€€ôì4(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡‰…‘”°…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`€´€ÄÔ¤ì4(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡‰…‘”°…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d€´€ÄÔ¤ì4(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡‰…‘”¤ì4(€€€€€€€€€€€€€€€‰É•…¬ì4(€€€€€€€ô4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒA½±å½¸ÉÉ½Ý!•…¡…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¸…¹¹½Ñ…Ñ¥½¸°	ÉÕÍ ‰ÉÕÍ ¤4(€€€ì4(€€€€€€€Ù…È…¹±”€ô5…Ñ ¹Ñ…¸È 4(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹d€´…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d°4(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹`€´…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`¤ì4(€€€€€€€½¹ÍÐ‘½Õ‰±”±•¹Ñ €ô€ÄÔì4(€€€€€€€Ù…È±•™Ð€ô¹•ÜA½¥¹Ð 4(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹`€´±•¹Ñ €¨5…Ñ ¹½Ì¡…¹±”€´5…Ñ ¹A$€¼€Ø¤°4(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹d€´±•¹Ñ €¨5…Ñ ¹M¥¸¡…¹±”€´5…Ñ ¹A$€¼€Ø¤¤ì4(€€€€€€€Ù…ÈÉ¥¡Ð€ô¹•ÜA½¥¹Ð 4(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹`€´±•¹Ñ €¨5…Ñ ¹½Ì¡…¹±”€¬5…Ñ ¹A$€¼€Ø¤°4(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹d€´±•¹Ñ €¨5…Ñ ¹M¥¸¡…¹±”€¬5…Ñ ¹A$€¼€Ø¤¤ì4(€€€€€€€É•ÑÕÉ¸¹•ÜA½±å½¸4(€€€€€€€ì4(€€€€€€€€€€€A½¥¹ÑÌ€ô¹•ÜA½¥¹Ñ½±±•Ñ¥½¸¡m…¹¹½Ñ…Ñ¥½¸¹¹°±•™Ð°É¥¡Ñt¤°4(€€€€€€€€€€€¥±°€ô‰ÉÕÍ °4(€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”4(€€€€€€€ôì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥UÁ‘…Ñ•Q½½±M•±•Ñ¥½¸ ¤4(€€€ì4(€€€€€€€™½É•… €¡Ù…È€¡Ñ½½°°‰ÕÑÑ½¸¤¥¸}Ñ½½±	ÕÑÑ½¹Ì¤4(€€€€€€€ì4(€€€€€€€€€€€Ù…ÈÍ•±•Ñ•€ôÑ½½°€ôô}Ñ½½°ì4(€€€€€€€€€€€‰ÕÑÑ½¸¹	…­É½Õ¹€ô	ÉÕÍ ¡Í•±•Ñ•4(€€€€€€€€€€€€€€€€ü}¥Í…É¬€ü€ˆŒÁÕØàˆ€è€ˆÙàˆ4(€€€€€€€€€€€€€€€€è}¥Í…É¬€ü€ˆŒÄàÈÈÉˆ€è€ˆˆ¤ì4(€€€€€€€€€€€‰ÕÑÑ½¸¹	½É‘•É	ÉÕÍ €ô	ÉÕÍ ¡Í•±•Ñ•4(€€€€€€€€€€€€€€€€ü€ˆŒÉ	åˆ4(€€€€€€€€€€€€€€€€è}¥Í…É¬€ü€ˆŒÐÈÔÀÕˆ€è€ˆ	Õˆ¤ì4(€€€€€€€€€€€‰ÕÑÑ½¸¹½É•É½Õ¹€ô	ÉÕÍ  4(€€€€€€€€€€€€€€€}¥Í…É¬€ü€ˆÕáˆ€èÍ•±•Ñ•€ü€ˆŒÀÜÕØØˆ€è€ˆŒÈÔÌÄÍˆ¤ì4(€€€€€€€ô4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥=¹AÉ•Ù¥•Ý-•å½Ý¸¡½‰©•ÐÍ•¹‘•È°-•åÙ•¹ÑÉÌ”¤4(€€€ì4(€€€€€€€¥˜€¡”¹-•ä€ôô-•ä¹Í…Á”¤4(€€€€€€€ì4(€€€€€€€€€€€¥…±½I•ÍÕ±Ð€ô™…±Í”ì4(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì4(€€€€€€€ô4(€€€€€€€•±Í”¥˜€¡”¹-•ä€ôô-•ä¹¹Ñ•È€˜˜4(€€€€€€€€€€€€€€€€}Í•±•Ñ¥½¹I•…‘ä€˜˜4(€€€€€€€€€€€€€€€€”¹=É¥¥¹…±M½ÕÉ”¥Ì¹½ÐQ•áÑ	½à¤4(€€€€€€€ì4(€€€€€€€€€€€½µÁ±•Ñ” ¤ì4(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì4(€€€€€€€ô4(€€€€€€€•±Í”¥˜€¡”¹-•ä€ôô-•ä¹H€˜˜4(€€€€€€€€€€€€€€€€}Í•±•Ñ¥½¹I•…‘ä€˜˜4(€€€€€€€€€€€€€€€€-•å‰½…É¹5½‘¥™¥•ÉÌ€ôô5½‘¥™¥•É-•åÌ¹9½¹”¤4(€€€€€€€ì4(€€€€€€€€€€€I•Í•ÑM•±•Ñ¥½¸ ¤ì4(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì4(€€€€€€€ô4(€€€€€€€•±Í”¥˜€¡”¹-•ä€ôô-•ä¹h€˜˜4(€€€€€€€€€€€€€€€€-•å‰½…É¹5½‘¥™¥•ÉÌ¹!…Í±…œ¡5½‘¥™¥•É-•åÌ¹½¹ÑÉ½°¤¤4(€€€€€€€ì4(€€€€€€€€€€€U¹‘¼ ¤ì4(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì4(€€€€€€€ô4(€€€€€€€•±Í”¥˜€¡”¹-•ä€ôô-•ä¹d€˜˜4(€€€€€€€€€€€€€€€€-•å‰½…É¹5½‘¥™¥•ÉÌ¹!…Í±…œ¡5½‘¥™¥•É-•åÌ¹½¹ÑÉ½°¤¤4(€€€€€€€ì4(€€€€€€€€€€€I•‘¼ ¤ì4(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì4(€€€€€€€ô4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥½µÁ±•Ñ” ¤4(€€€ì4(€€€€€€€¥˜€ …}Í•±•Ñ¥½¹I•…‘ä¤4(€€€€€€€ì4(€€€€€€€€€€€É•ÑÕÉ¸ì4(€€€€€€€ô4(4(€€€€€€€ÕÍ¥¹œÙ…ÈÉ½À€ôÉ½ÁÉ½é•¹M•±•Ñ¥½¸ ¤ì4(€€€€€€€‘¥Ñ•‘	¥Ñµ…Àü¹¥ÍÁ½Í” ¤ì4(€€€€€€€‘¥Ñ•‘	¥Ñµ…À€ô…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹I•¹‘•É•È¹I•¹‘•È 4(€€€€€€€€€€€É½À°4(€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹Ì°4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹]¥‘Ñ °4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹!•¥¡Ð¤ì4(€€€€€€€¥…±½I•ÍÕ±Ð€ôÑÉÕ”ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”É…Ý¥¹	¥Ñµ…ÀÉ½ÁÉ½é•¹M•±•Ñ¥½¸ ¤4(€€€ì4(€€€€€€€Ù…ÈÍ…±•`€ô}‘•Í­Ñ½Á	¥Ñµ…À¹]¥‘Ñ €¼5…Ñ ¹5…à Å°]¥‘Ñ ¤ì4(€€€€€€€Ù…ÈÍ…±•d€ô}‘•Í­Ñ½Á	¥Ñµ…À¹!•¥¡Ð€¼5…Ñ ¹5…à Å°!•¥¡Ð¤ì4(€€€€€€€Ù…È±•™Ð€ô5…Ñ ¹±…µÀ 4(€€€€€€€€€€€€¡¥¹Ð¥5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹1•™Ð€¨Í…±•`¤°4(€€€€€€€€€€€€À°4(€€€€€€€€€€€}‘•Í­Ñ½Á	¥Ñµ…À¹]¥‘Ñ €´€Ä¤ì4(€€€€€€€Ù…ÈÑ½À€ô5…Ñ ¹±…µÀ 4(€€€€€€€€€€€€¡¥¹Ð¥5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹Q½À€¨Í…±•d¤°4(€€€€€€€€€€€€À°4(€€€€€€€€€€€}‘•Í­Ñ½Á	¥Ñµ…À¹!•¥¡Ð€´€Ä¤ì4(€€€€€€€Ù…ÈÝ¥‘Ñ €ô5…Ñ ¹±…µÀ 4(€€€€€€€€€€€€¡¥¹Ð¥5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹]¥‘Ñ €¨Í…±•`¤°4(€€€€€€€€€€€€Ä°4(€€€€€€€€€€€}‘•Í­Ñ½Á	¥Ñµ…À¹]¥‘Ñ €´±•™Ð¤ì4(€€€€€€€Ù…È¡•¥¡Ð€ô5…Ñ ¹±…µÀ 4(€€€€€€€€€€€€¡¥¹Ð¥5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹!•¥¡Ð€¨Í…±•d¤°4(€€€€€€€€€€€€Ä°4(€€€€€€€€€€€}‘•Í­Ñ½Á	¥Ñµ…À¹!•¥¡Ð€´Ñ½À¤ì4(€€€€€€€É•ÑÕÉ¸}‘•Í­Ñ½Á	¥Ñµ…À¹±½¹” 4(€€€€€€€€€€€¹•ÜMåÍÑ•´¹É…Ý¥¹œ¹I•Ñ…¹±”¡±•™Ð°Ñ½À°Ý¥‘Ñ °¡•¥¡Ð¤°4(€€€€€€€€€€€É…Ý¥¹A¥á•±½Éµ…Ð¹½Éµ…ÐÌÉ‰ÁÁÉˆ¤ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥I•Í•ÑM•±•Ñ¥½¸ ¤4(€€€ì4(€€€€€€€}‘É…¥¹œ€ô™…±Í”ì4(€€€€€€€}‘É…Ý¥¹œ€ô™…±Í”ì4(€€€€€€€}Í•±•Ñ¥½¹I•…‘ä€ô™…±Í”ì4(€€€€€€€I•±•…Í•5½ÕÍ•…ÁÑÕÉ” ¤ì4(€€€€€€€}Í•±•Ñ¥½¸¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•ì4(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•ì4(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹±•…È ¤ì4(€€€€€€€}Í¥é•	…‘”¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•ì4(€€€€€€€}Ñ½½±‰…È¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•ì4(€€€€€€€M•Ñ!…¹‘±•ÍY¥Í¥‰¥±¥Ñä¡Y¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•¤ì4(€€€€€€€}…¹¹½Ñ…Ñ¥½¹Ì¹±•…È ¤ì4(€€€€€€€}É•‘¼¹±•…È ¤ì4(€€€€€€€}¹•áÑ9Õµ‰•È€ô€Äì4(€€€€€€€ÕÉÍ½È€ôÕÉÍ½ÉÌ¹É½ÍÌì4(€€€€€€€UÁ‘…Ñ•M¡…‘”¡‘•™…Õ±Ð¤ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥UÁ‘…Ñ•M•±•Ñ¥½¸¡A½¥¹Ð•¹¤4(€€€ì4(€€€€€€€}±½…±M•±•Ñ¥½¸€ô9½Éµ…±¥é”¡}ÍÑ…ÉÐ°•¹¤ì4(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}Í•±•Ñ¥½¸°}±½…±M•±•Ñ¥½¸¹1•™Ð¤ì4(€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡}Í•±•Ñ¥½¸°}±½…±M•±•Ñ¥½¸¹Q½À¤ì4(€€€€€€€}Í•±•Ñ¥½¸¹]¥‘Ñ €ô}±½…±M•±•Ñ¥½¸¹]¥‘Ñ ì4(€€€€€€€}Í•±•Ñ¥½¸¹!•¥¡Ð€ô}±½…±M•±•Ñ¥½¸¹!•¥¡Ðì4(€€€€€€€UÁ‘…Ñ•M¡…‘”¡}±½…±M•±•Ñ¥½¸¤ì4(4(€€€€€€€¥˜€¡}Í¥é•	…‘”¹¡¥±¥ÌQ•áÑ	±½¬Í¥é”¤4(€€€€€€€ì4(€€€€€€€€€€€Ù…ÈÍ…±•`€ô}‘•Í­Ñ½Á	¥Ñµ…À¹]¥‘Ñ €¼5…Ñ ¹5…à Å°]¥‘Ñ ¤ì4(€€€€€€€€€€€Ù…ÈÍ…±•d€ô}‘•Í­Ñ½Á	¥Ñµ…À¹!•¥¡Ð€¼5…Ñ ¹5…à Å°!•¥¡Ð¤ì4(€€€€€€€€€€€Í¥é”¹Q•áÐ€ô4(€€€€€€€€€€€€€€€€‰í5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹]¥‘Ñ €¨Í…±•`¤é8Áôƒ\€ˆ€¬4(€€€€€€€€€€€€€€€€‰í5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹!•¥¡Ð€¨Í…±•d¤é8Áôˆì4(€€€€€€€ô4(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}Í¥é•	…‘”°}±½…±M•±•Ñ¥½¸¹1•™Ð¤ì4(€€€€€€€…¹Ù…Ì¹M•ÑQ½À 4(€€€€€€€€€€€}Í¥é•	…‘”°4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹Q½À€ø€ÐÀ4(€€€€€€€€€€€€€€€€ü}±½…±M•±•Ñ¥½¸¹Q½À€´€ÌÐ4(€€€€€€€€€€€€€€€€è}±½…±M•±•Ñ¥½¸¹	½ÑÑ½´€¬€à¤ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥A½Í¥Ñ¥½¹¹¹½Ñ…Ñ¥½¹1…å•È ¤4(€€€ì4(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}…¹¹½Ñ…Ñ¥½¹1…å•È°}±½…±M•±•Ñ¥½¸¹1•™Ð¤ì4(€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡}…¹¹½Ñ…Ñ¥½¹1…å•È°}±½…±M•±•Ñ¥½¸¹Q½À¤ì4(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹]¥‘Ñ €ô}±½…±M•±•Ñ¥½¸¹]¥‘Ñ ì4(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹!•¥¡Ð€ô}±½…±M•±•Ñ¥½¸¹!•¥¡Ðì4(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹Y¥Í¥‰±”ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”A½¥¹ÐQ½¹¹½Ñ…Ñ¥½¹A½¥¹Ð¡A½¥¹Ð…¹Ù…ÍA½¥¹Ð¤€ôø¹•Ü 4(€€€€€€€5…Ñ ¹±…µÀ¡…¹Ù…ÍA½¥¹Ð¹`€´}±½…±M•±•Ñ¥½¸¹1•™Ð°€À°}±½…±M•±•Ñ¥½¸¹]¥‘Ñ ¤°4(€€€€€€€5…Ñ ¹±…µÀ¡…¹Ù…ÍA½¥¹Ð¹d€´}±½…±M•±•Ñ¥½¸¹Q½À°€À°}±½…±M•±•Ñ¥½¸¹!•¥¡Ð¤¤ì4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥UÁ‘…Ñ•M¡…‘”¡I•Ð±•…È¤4(€€€ì4(€€€€€€€Ù…ÈÝ¥‘Ñ €ô5…Ñ ¹5…à À°ÑÕ…±]¥‘Ñ €ø€À€üÑÕ…±]¥‘Ñ €è]¥‘Ñ ¤ì4(€€€€€€€Ù…È¡•¥¡Ð€ô5…Ñ ¹5…à À°ÑÕ…±!•¥¡Ð€ø€À€üÑÕ…±!•¥¡Ð€è!•¥¡Ð¤ì4(€€€€€€€¥˜€¡±•…È¹%ÍµÁÑäñð±•…È¹]¥‘Ñ €ðô€Àñð±•…È¹!•¥¡Ð€ðô€À¤4(€€€€€€€ì4(€€€€€€€€€€€A±…”¡}Í¡…‘•ÍlÁt°€À°€À°Ý¥‘Ñ °¡•¥¡Ð¤ì4(€€€€€€€€€€€™½È€¡Ù…È¥¹‘•à€ô€Äì¥¹‘•à€ð}Í¡…‘•Ì¹1•¹Ñ ì¥¹‘•à¬¬¤4(€€€€€€€€€€€ì4(€€€€€€€€€€€€€€€A±…”¡}Í¡…‘•Ím¥¹‘•át°€À°€À°€À°€À¤ì4(€€€€€€€€€€€ô4(€€€€€€€€€€€É•ÑÕÉ¸ì4(€€€€€€€ô4(4(€€€€€€€A±…”¡}Í¡…‘•ÍlÁt°€À°€À°Ý¥‘Ñ °±•…È¹Q½À¤ì4(€€€€€€€A±…”¡}Í¡…‘•ÍlÅt°€À°±•…È¹	½ÑÑ½´°Ý¥‘Ñ °¡•¥¡Ð€´±•…È¹	½ÑÑ½´¤ì4(€€€€€€€A±…”¡}Í¡…‘•ÍlÉt°€À°±•…È¹Q½À°±•…È¹1•™Ð°±•…È¹!•¥¡Ð¤ì4(€€€€€€€A±…” 4(€€€€€€€€€€€}Í¡…‘•ÍlÍt°4(€€€€€€€€€€€±•…È¹I¥¡Ð°4(€€€€€€€€€€€±•…È¹Q½À°4(€€€€€€€€€€€Ý¥‘Ñ €´±•…È¹I¥¡Ð°4(€€€€€€€€€€€±•…È¹!•¥¡Ð¤ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥A½Í¥Ñ¥½¹!…¹‘±•Ì ¤4(€€€ì4(€€€€€€€Ù…Èà€ô¹•Ýmt4(€€€€€€€ì4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹1•™Ð°4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹1•™Ð€¬€¡}±½…±M•±•Ñ¥½¸¹]¥‘Ñ €¼€È¤°4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹I¥¡Ð4(€€€€€€€ôì4(€€€€€€€Ù…Èä€ô¹•Ýmt4(€€€€€€€ì4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹Q½À°4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹Q½À€¬€¡}±½…±M•±•Ñ¥½¸¹!•¥¡Ð€¼€È¤°4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹	½ÑÑ½´4(€€€€€€€ôì4(€€€€€€€Ù…ÈÁ½Í¥Ñ¥½¹Ì€ô¹•Ýmt4(€€€€€€€ì4(€€€€€€€€€€€¹•ÜA½¥¹Ð¡álÁt°ålÁt¤°¹•ÜA½¥¹Ð¡álÅt°ålÁt¤°4(€€€€€€€€€€€¹•ÜA½¥¹Ð¡álÉt°ålÁt¤°¹•ÜA½¥¹Ð¡álÁt°ålÅt¤°4(€€€€€€€€€€€¹•ÜA½¥¹Ð¡álÉt°ålÅt¤°¹•ÜA½¥¹Ð¡álÁt°ålÉt¤°4(€€€€€€€€€€€¹•ÜA½¥¹Ð¡álÅt°ålÉt¤°¹•ÜA½¥¹Ð¡álÉt°ålÉt¤4(€€€€€€€ôì4(€€€€€€€™½È€¡Ù…È¥¹‘•à€ô€Àì¥¹‘•à€ð}¡…¹‘±•Ì¹1•¹Ñ ì¥¹‘•à¬¬¤4(€€€€€€€ì4(€€€€€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}¡…¹‘±•Ím¥¹‘•át°Á½Í¥Ñ¥½¹Ím¥¹‘•át¹`€´€Ô¤ì4(€€€€€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡}¡…¹‘±•Ím¥¹‘•át°Á½Í¥Ñ¥½¹Ím¥¹‘•át¹d€´€Ô¤ì4(€€€€€€€ô4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥A½Í¥Ñ¥½¹Q½½±‰…È ¤4(€€€ì4(€€€€€€€}Ñ½½±‰…È¹5•…ÍÕÉ”¡¹•ÜM¥é”¡‘½Õ‰±”¹A½Í¥Ñ¥Ù•%¹™¥¹¥Ñä°‘½Õ‰±”¹A½Í¥Ñ¥Ù•%¹™¥¹¥Ñä¤¤ì4(€€€€€€€Ù…È‘•Í¥É•€ô}Ñ½½±‰…È¹•Í¥É•‘M¥é”ì4(€€€€€€€Ù…ÈÝ¥‘Ñ €ôÑÕ…±]¥‘Ñ €ø€À€üÑÕ…±]¥‘Ñ €è]¥‘Ñ ì4(€€€€€€€Ù…È¡•¥¡Ð€ôÑÕ…±!•¥¡Ð€ø€À€üÑÕ…±!•¥¡Ð€è!•¥¡Ðì4(€€€€€€€Ù…È±•™Ð€ô5…Ñ ¹±…µÀ 4(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹1•™Ð€¬€ ¡}±½…±M•±•Ñ¥½¸¹]¥‘Ñ €´‘•Í¥É•¹]¥‘Ñ ¤€¼€È¤°4(€€€€€€€€€€€€ÄÈ°4(€€€€€€€€€€€5…Ñ ¹5…à ÄÈ°Ý¥‘Ñ €´‘•Í¥É•¹]¥‘Ñ €´€ÄÈ¤¤ì4(€€€€€€€Ù…È‰•±½Ü€ô}±½…±M•±•Ñ¥½¸¹	½ÑÑ½´€¬€ÄÐì4(€€€€€€€Ù…ÈÑ½À€ô‰•±½Ü€¬‘•Í¥É•¹!•¥¡Ð€ðô¡•¥¡Ð€´€ÄÈ4(€€€€€€€€€€€€ü‰•±½Ü4(€€€€€€€€€€€€è5…Ñ ¹5…à ÄÈ°}±½…±M•±•Ñ¥½¸¹Q½À€´‘•Í¥É•¹!•¥¡Ð€´€ÄÐ¤ì4(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}Ñ½½±‰…È°±•™Ð¤ì4(€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡}Ñ½½±‰…È°Ñ½À¤ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”Ù½¥M•Ñ!…¹‘±•ÍY¥Í¥‰¥±¥Ñä¡Y¥Í¥‰¥±¥ÑäÙ¥Í¥‰¥±¥Ñä¤4(€€€ì4(€€€€€€€™½É•… €¡Ù…È¡…¹‘±”¥¸}¡…¹‘±•Ì¤4(€€€€€€€ì4(€€€€€€€€€€€¡…¹‘±”¹Y¥Í¥‰¥±¥Ñä€ôÙ¥Í¥‰¥±¥Ñäì4(€€€€€€€ô4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”‰½½°%ÍQ½½±‰…ÉM½ÕÉ”¡•Á•¹‘•¹å=‰©•ÐüÍ½ÕÉ”¤4(€€€ì4(€€€€€€€Ý¡¥±”€¡Í½ÕÉ”¥Ì¹½Ð¹Õ±°¤4(€€€€€€€ì4(€€€€€€€€€€€¥˜€¡I•™•É•¹•ÅÕ…±Ì¡Í½ÕÉ”°}Ñ½½±‰…È¤¤4(€€€€€€€€€€€ì4(€€€€€€€€€€€€€€€É•ÑÕÉ¸ÑÉÕ”ì4(€€€€€€€€€€€ô4(€€€€€€€€€€€Í½ÕÉ”€ôY¥ÍÕ…±QÉ••!•±Á•È¹•ÑA…É•¹Ð¡Í½ÕÉ”¤ì4(€€€€€€€ô4(€€€€€€€É•ÑÕÉ¸™…±Í”ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒI•Ñ…¹±”M¡…‘” ¤€ôø¹•Ü ¤4(€€€ì4(€€€€€€€¥±°€ô¹•ÜM½±¥‘½±½É	ÉÕÍ ¡½±½È¹É½µÉˆ ÄÔÀ°€Ì°€ÄÈ°€ÈÈ¤¤°4(€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”4(€€€ôì4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ	½É‘•È!…¹‘±” ¤€ôø¹•Ü ¤4(€€€ì4(€€€€€€€]¥‘Ñ €ô€ÄÀ°4(€€€€€€€!•¥¡Ð€ô€ÄÀ°4(€€€€€€€½É¹•ÉI…‘¥ÕÌ€ô¹•Ü½É¹•ÉI…‘¥ÕÌ Ô¤°4(€€€€€€€	…­É½Õ¹€ô	ÉÕÍ¡•Ì¹]¡¥Ñ”°4(€€€€€€€	½É‘•É	ÉÕÍ €ô¹•ÜM½±¥‘½±½É	ÉÕÍ ¡½±½È¹É½µIˆ ÄÀ°€ÄØä°€ÄàÜ¤¤°4(€€€€€€€	½É‘•ÉQ¡¥­¹•ÍÌ€ô¹•ÜQ¡¥­¹•ÍÌ È¤°4(€€€€€€€Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•°4(€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”4(€€€ôì4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÙ½¥A±…” 4(€€€€€€€É…µ•Ý½É­±•µ•¹Ð•±•µ•¹Ð°4(€€€€€€€‘½Õ‰±”±•™Ð°4(€€€€€€€‘½Õ‰±”Ñ½À°4(€€€€€€€‘½Õ‰±”Ý¥‘Ñ °4(€€€€€€€‘½Õ‰±”¡•¥¡Ð¤4(€€€ì4(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡•±•µ•¹Ð°±•™Ð¤ì4(€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡•±•µ•¹Ð°Ñ½À¤ì4(€€€€€€€•±•µ•¹Ð¹]¥‘Ñ €ô5…Ñ ¹5…à À°Ý¥‘Ñ ¤ì4(€€€€€€€•±•µ•¹Ð¹!•¥¡Ð€ô5…Ñ ¹5…à À°¡•¥¡Ð¤ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒI•Ð9½Éµ…±¥é”¡A½¥¹ÐÍÑ…ÉÐ°A½¥¹Ð•¹¤€ôø¹•Ü 4(€€€€€€€5…Ñ ¹5¥¸¡ÍÑ…ÉÐ¹`°•¹¹`¤°4(€€€€€€€5…Ñ ¹5¥¸¡ÍÑ…ÉÐ¹d°•¹¹d¤°4(€€€€€€€5…Ñ ¹‰Ì¡•¹¹`€´ÍÑ…ÉÐ¹`¤°4(€€€€€€€5…Ñ ¹‰Ì¡•¹¹d€´ÍÑ…ÉÐ¹d¤¤ì4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒM½±¥‘½±½É	ÉÕÍ 	ÉÕÍ ¡ÍÑÉ¥¹œ½±½È¤€ôø4(€€€€€€€¹•Ü ¡½±½È¥½±½É½¹Ù•ÉÑ•È¹½¹Ù•ÉÑÉ½µMÑÉ¥¹œ¡½±½È¤¤ì4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÉ…Ý¥¹	¥Ñµ…À…ÁÑÕÉ•Y¥ÉÑÕ…±•Í­Ñ½Á	¥Ñµ…À ¤€ôø4(€€€€€€€…ÁÑÕÉ•Y¥ÉÑÕ…±•Í­Ñ½Á	¥Ñµ…À¡™…±Í”¤ì4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÉ…Ý¥¹	¥Ñµ…À…ÁÑÕÉ•Y¥ÉÑÕ…±•Í­Ñ½Á	¥Ñµ…À¡‰½½°¥¹±Õ‘•ÕÉÍ½È¤4(€€€ì4(€€€€€€€Ù…ÈÙ¥ÉÑÕ…±MÉ••¸€ôMåÍÑ•´¹]¥¹‘½ÝÌ¹½ÉµÌ¹MåÍÑ•µ%¹™½Éµ…Ñ¥½¸¹Y¥ÉÑÕ…±MÉ••¸ì4(€€€€€€€É•ÑÕÉ¸…ÁÑÕÉ•M•ÉÙ¥”¹…ÁÑÕÉ•	¥Ñµ…À¡Ù¥ÉÑÕ…±MÉ••¸°¥¹±Õ‘•ÕÉÍ½È¤ì4(€€€ô4(4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ	¥Ñµ…ÁM½ÕÉ”Q½	¥Ñµ…ÁM½ÕÉ”¡É…Ý¥¹	¥Ñµ…À‰¥Ñµ…À¤4(€€€ì4(€€€€€€€Ù…È¡…¹‘±”€ô‰¥Ñµ…À¹•Ñ!‰¥Ñµ…À ¤ì4(€€€€€€€ÑÉä4(€€€€€€€ì4(€€€€€€€€€€€Ù…ÈÍ½ÕÉ”€ô%µ…¥¹œ¹É•…Ñ•	¥Ñµ…ÁM½ÕÉ•É½µ!	¥Ñµ…À 4(€€€€€€€€€€€€€€€¡…¹‘±”°4(€€€€€€€€€€€€€€€%¹ÑAÑÈ¹i•É¼°4(€€€€€€€€€€€€€€€%¹ÐÌÉI•Ð¹µÁÑä°4(€€€€€€€€€€€€€€€	¥Ñµ…ÁM¥é•=ÁÑ¥½¹Ì¹É½µµÁÑå=ÁÑ¥½¹Ì ¤¤ì4(€€€€€€€€€€€Í½ÕÉ”¹É••é” ¤ì4(€€€€€€€€€€€É•ÑÕÉ¸Í½ÕÉ”ì4(€€€€€€€ô4(€€€€€€€™¥¹…±±ä4(€€€€€€€ì4(€€€€€€€€€€€|€ô•±•Ñ•=‰©•Ð¡¡…¹‘±”¤ì4(€€€€€€€ô4(€€€ô4(4(€€€m±±%µÁ½ÉÐ ‰‘¤ÌÈ¹‘±°ˆ¥t4(€€€mÉ•ÑÕÉ¸è5…ÉÍ¡…±Ì¡U¹µ…¹…•‘QåÁ”¹	½½°¥t4(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ•áÑ•É¸‰½½°•±•Ñ•=‰©•Ð¡%¹ÑAÑÈ¡…¹‘±”¤ì4)ô4