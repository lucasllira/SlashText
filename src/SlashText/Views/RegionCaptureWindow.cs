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
                Opacity = .38,
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

    ×®4¶‰žËkºwµçQ½±½É	ÉÕÍ  (€€€€€€€€€€€½±½È¹É½µÉˆ¡½±½È¹°½±½È¹H°½±½È¹°½±½È¹¤¤ì(€€€€€€€Ù…ÈÑ¡¥­¹•ÍÌ€ô…¹¹½Ñ…Ñ¥½¸¹-¥¹€ôô…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹!¥¡±¥¡Ñ•È(€€€€€€€€€€€€ü…¹¹½Ñ…Ñ¥½¸¹Q¡¥­¹•ÍÌ€¨€Ð(€€€€€€€€€€€€è…¹¹½Ñ…Ñ¥½¸¹Q¡¥­¹•ÍÌì(€€€€€€€¥˜€¡…¹¹½Ñ…Ñ¥½¸¹-¥¹€ôô…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹!¥¡±¥¡Ñ•È¤(€€€€€€€ì(€€€€€€€€€€€‰ÉÕÍ ¹=Á…¥Ñä€ô€¸Ìàì(€€€€€€€ô((€€€€€€€ÍÝ¥Ñ €¡…¹¹½Ñ…Ñ¥½¸¹-¥¹¤(€€€€€€€ì(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹ÉÉ½Üè(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹!¥¡±¥¡Ñ•Èè(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡¹•Ü1¥¹”(€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€`Ä€ô…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`°(€€€€€€€€€€€€€€€€€€€dÄ€ô…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d°(€€€€€€€€€€€€€€€€€€€`È€ô…¹¹½Ñ…Ñ¥½¸¹¹¹`°(€€€€€€€€€€€€€€€€€€€dÈ€ô…¹¹½Ñ…Ñ¥½¸¹¹¹d°(€€€€€€€€€€€€€€€€€€€MÑÉ½­”€ô‰ÉÕÍ °(€€€€€€€€€€€€€€€€€€€MÑÉ½­•Q¡¥­¹•ÍÌ€ôÑ¡¥­¹•ÍÌ°(€€€€€€€€€€€€€€€€€€€MÑÉ½­•MÑ…ÉÑ1¥¹•…À€ôA•¹1¥¹•…À¹I½Õ¹°(€€€€€€€€€€€€€€€€€€€MÑÉ½­•¹‘1¥¹•…À€ôA•¹1¥¹•…À¹I½Õ¹°(€€€€€€€€€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”(€€€€€€€€€€€€€€€ô¤ì(€€€€€€€€€€€€€€€¥˜€¡…¹¹½Ñ…Ñ¥½¸¹-¥¹€ôô…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹ÉÉ½Ü¤(€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡ÉÉ½Ý!•…¡…¹¹½Ñ…Ñ¥½¸°‰ÉÕÍ ¤¤ì(€€€€€€€€€€€€€€€ô(€€€€€€€€€€€€€€€‰É•…¬ì(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹I•Ñ…¹±”è(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹±±¥ÁÍ”è(€€€€€€€€€€€€€€€Ù…ÈÍ¡…Á”€ô…¹¹½Ñ…Ñ¥½¸¹-¥¹€ôô…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹I•Ñ…¹±”(€€€€€€€€€€€€€€€€€€€€ü€¡M¡…Á”¥¹•ÜI•Ñ…¹±” ¤(€€€€€€€€€€€€€€€€€€€€è¹•Ü±±¥ÁÍ” ¤ì(€€€€€€€€€€€€€€€Í¡…Á”¹MÑÉ½­”€ô‰ÉÕÍ ì(€€€€€€€€€€€€€€€Í¡…Á”¹MÑÉ½­•Q¡¥­¹•ÍÌ€ôÑ¡¥­¹•ÍÌì(€€€€€€€€€€€€€€€Í¡…Á”¹]¥‘Ñ €ô5…Ñ ¹‰Ì¡…¹¹½Ñ…Ñ¥½¸¹¹¹`€´…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`¤ì(€€€€€€€€€€€€€€€Í¡…Á”¹!•¥¡Ð€ô5…Ñ ¹‰Ì¡…¹¹½Ñ…Ñ¥½¸¹¹¹d€´…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d¤ì(€€€€€€€€€€€€€€€Í¡…Á”¹%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”ì(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡Í¡…Á”°5…Ñ ¹5¥¸¡…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`°…¹¹½Ñ…Ñ¥½¸¹¹¹`¤¤ì(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡Í¡…Á”°5…Ñ ¹5¥¸¡…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d°…¹¹½Ñ…Ñ¥½¸¹¹¹d¤¤ì(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡Í¡…Á”¤ì(€€€€€€€€€€€€€€€‰É•…¬ì(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹A•¹¥°è(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡¹•ÜA½±å±¥¹”(€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€A½¥¹ÑÌ€ô¹•ÜA½¥¹Ñ½±±•Ñ¥½¸¡…¹¹½Ñ…Ñ¥½¸¹A½¥¹ÑÌ¤°(€€€€€€€€€€€€€€€€€€€MÑÉ½­”€ô‰ÉÕÍ °(€€€€€€€€€€€€€€€€€€€MÑÉ½­•Q¡¥­¹•ÍÌ€ôÑ¡¥­¹•ÍÌ°(€€€€€€€€€€€€€€€€€€€MÑÉ½­•1¥¹•)½¥¸€ôA•¹1¥¹•)½¥¸¹I½Õ¹°(€€€€€€€€€€€€€€€€€€€MÑÉ½­•MÑ…ÉÑ1¥¹•…À€ôA•¹1¥¹•…À¹I½Õ¹°(€€€€€€€€€€€€€€€€€€€MÑÉ½­•¹‘1¥¹•…À€ôA•¹1¥¹•…À¹I½Õ¹°(€€€€€€€€€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”(€€€€€€€€€€€€€€€ô¤ì(€€€€€€€€€€€€€€€‰É•…¬ì(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹Q•áÐè(€€€€€€€€€€€€€€€Ù…ÈÑ•áÐ€ô¹•ÜQ•áÑ	±½¬(€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€Q•áÐ€ô…¹¹½Ñ…Ñ¥½¸¹Q•áÐ°(€€€€€€€€€€€€€€€€€€€½É•É½Õ¹€ô‰ÉÕÍ °(€€€€€€€€€€€€€€€€€€€½¹ÑM¥é”€ô€ÄÜ°(€€€€€€€€€€€€€€€€€€€½¹Ñ]•¥¡Ð€ô½¹Ñ]•¥¡ÑÌ¹	½±°(€€€€€€€€€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”(€€€€€€€€€€€€€€€ôì(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡Ñ•áÐ°…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`¤ì(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡Ñ•áÐ°…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d¤ì(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡Ñ•áÐ¤ì(€€€€€€€€€€€€€€€‰É•…¬ì(€€€€€€€€€€€…Í”…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹-¥¹¹9Õµ‰•Èè(€€€€€€€€€€€€€€€Ù…È‰…‘”€ô¹•Ü	½É‘•È(€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€]¥‘Ñ €ô€ÌÀ°(€€€€€€€€€€€€€€€€€€€!•¥¡Ð€ô€ÌÀ°(€€€€€€€€€€€€€€€€€€€½É¹•ÉI…‘¥ÕÌ€ô¹•Ü½É¹•ÉI…‘¥ÕÌ ÄÔ¤°(€€€€€€€€€€€€€€€€€€€	…­É½Õ¹€ô‰ÉÕÍ °(€€€€€€€€€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”°(€€€€€€€€€€€€€€€€€€€¡¥±€ô¹•ÜQ•áÑ	±½¬(€€€€€€€€€€€€€€€€€€€ì(€€€€€€€€€€€€€€€€€€€€€€€Q•áÐ€ô…¹¹½Ñ…Ñ¥½¸¹Q•áÐ°(€€€€€€€€€€€€€€€€€€€€€€€½É•É½Õ¹€ô	ÉÕÍ¡•Ì¹]¡¥Ñ”°(€€€€€€€€€€€€€€€€€€€€€€€½¹Ñ]•¥¡Ð€ô½¹Ñ]•¥¡ÑÌ¹	½±°(€€€€€€€€€€€€€€€€€€€€€€€!½É¥é½¹Ñ…±±¥¹µ•¹Ð€ô!½É¥é½¹Ñ…±±¥¹µ•¹Ð¹•¹Ñ•È°(€€€€€€€€€€€€€€€€€€€€€€€Y•ÉÑ¥…±±¥¹µ•¹Ð€ôY•ÉÑ¥…±±¥¹µ•¹Ð¹•¹Ñ•È(€€€€€€€€€€€€€€€€€€€ô(€€€€€€€€€€€€€€€ôì(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡‰…‘”°…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`€´€ÄÔ¤ì(€€€€€€€€€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡‰…‘”°…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d€´€ÄÔ¤ì(€€€€€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹‘¡‰…‘”¤ì(€€€€€€€€€€€€€€€‰É•…¬ì(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒA½±å½¸ÉÉ½Ý!•…¡…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¸…¹¹½Ñ…Ñ¥½¸°	ÉÕÍ ‰ÉÕÍ ¤(€€€ì(€€€€€€€Ù…È…¹±”€ô5…Ñ ¹Ñ…¸È (€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹d€´…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹d°(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹`€´…¹¹½Ñ…Ñ¥½¸¹MÑ…ÉÐ¹`¤ì(€€€€€€€½¹ÍÐ‘½Õ‰±”±•¹Ñ €ô€ÄÔì(€€€€€€€Ù…È±•™Ð€ô¹•ÜA½¥¹Ð (€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹`€´±•¹Ñ €¨5…Ñ ¹½Ì¡…¹±”€´5…Ñ ¹A$€¼€Ø¤°(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹d€´±•¹Ñ €¨5…Ñ ¹M¥¸¡…¹±”€´5…Ñ ¹A$€¼€Ø¤¤ì(€€€€€€€Ù…ÈÉ¥¡Ð€ô¹•ÜA½¥¹Ð (€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹`€´±•¹Ñ €¨5…Ñ ¹½Ì¡…¹±”€¬5…Ñ ¹A$€¼€Ø¤°(€€€€€€€€€€€…¹¹½Ñ…Ñ¥½¸¹¹¹d€´±•¹Ñ €¨5…Ñ ¹M¥¸¡…¹±”€¬5…Ñ ¹A$€¼€Ø¤¤ì(€€€€€€€É•ÑÕÉ¸¹•ÜA½±å½¸(€€€€€€€ì(€€€€€€€€€€€A½¥¹ÑÌ€ô¹•ÜA½¥¹Ñ½±±•Ñ¥½¸¡m…¹¹½Ñ…Ñ¥½¸¹¹°±•™Ð°É¥¡Ñt¤°(€€€€€€€€€€€¥±°€ô‰ÉÕÍ °(€€€€€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”(€€€€€€€ôì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥UÁ‘…Ñ•Q½½±M•±•Ñ¥½¸ ¤(€€€ì(€€€€€€€™½É•… €¡Ù…È€¡Ñ½½°°‰ÕÑÑ½¸¤¥¸}Ñ½½±	ÕÑÑ½¹Ì¤(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÍ•±•Ñ•€ôÑ½½°€ôô}Ñ½½°ì(€€€€€€€€€€€‰ÕÑÑ½¸¹	…­É½Õ¹€ô	ÉÕÍ ¡Í•±•Ñ•(€€€€€€€€€€€€€€€€ü}¥Í…É¬€ü€ˆŒÁÕØàˆ€è€ˆÙàˆ(€€€€€€€€€€€€€€€€è}¥Í…É¬€ü€ˆŒÄàÈÈÉˆ€è€ˆˆ¤ì(€€€€€€€€€€€‰ÕÑÑ½¸¹	½É‘•É	ÉÕÍ €ô	ÉÕÍ ¡Í•±•Ñ•(€€€€€€€€€€€€€€€€ü€ˆŒÉ	åˆ(€€€€€€€€€€€€€€€€è}¥Í…É¬€ü€ˆŒÐÈÔÀÕˆ€è€ˆ	Õˆ¤ì(€€€€€€€€€€€‰ÕÑÑ½¸¹½É•É½Õ¹€ô	ÉÕÍ  (€€€€€€€€€€€€€€€}¥Í…É¬€ü€ˆÕáˆ€èÍ•±•Ñ•€ü€ˆŒÀÜÕØØˆ€è€ˆŒÈÔÌÄÍˆ¤ì(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥=¹AÉ•Ù¥•Ý-•å½Ý¸¡½‰©•ÐÍ•¹‘•È°-•åÙ•¹ÑÉÌ”¤(€€€ì(€€€€€€€¥˜€¡”¹-•ä€ôô-•ä¹Í…Á”¤(€€€€€€€ì(€€€€€€€€€€€¥…±½I•ÍÕ±Ð€ô™…±Í”ì(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì(€€€€€€€ô(€€€€€€€•±Í”¥˜€¡”¹-•ä€ôô-•ä¹¹Ñ•È€˜˜(€€€€€€€€€€€€€€€€}Í•±•Ñ¥½¹I•…‘ä€˜˜(€€€€€€€€€€€€€€€€”¹=É¥¥¹…±M½ÕÉ”¥Ì¹½ÐQ•áÑ	½à¤(€€€€€€€ì(€€€€€€€€€€€½µÁ±•Ñ” ¤ì(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì(€€€€€€€ô(€€€€€€€•±Í”¥˜€¡”¹-•ä€ôô-•ä¹H€˜˜(€€€€€€€€€€€€€€€€}Í•±•Ñ¥½¹I•…‘ä€˜˜(€€€€€€€€€€€€€€€€-•å‰½…É¹5½‘¥™¥•ÉÌ€ôô5½‘¥™¥•É-•åÌ¹9½¹”¤(€€€€€€€ì(€€€€€€€€€€€I•Í•ÑM•±•Ñ¥½¸ ¤ì(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì(€€€€€€€ô(€€€€€€€•±Í”¥˜€¡”¹-•ä€ôô-•ä¹h€˜˜(€€€€€€€€€€€€€€€€-•å‰½…É¹5½‘¥™¥•ÉÌ¹!…Í±…œ¡5½‘¥™¥•É-•åÌ¹½¹ÑÉ½°¤¤(€€€€€€€ì(€€€€€€€€€€€U¹‘¼ ¤ì(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì(€€€€€€€ô(€€€€€€€•±Í”¥˜€¡”¹-•ä€ôô-•ä¹d€˜˜(€€€€€€€€€€€€€€€€-•å‰½…É¹5½‘¥™¥•ÉÌ¹!…Í±…œ¡5½‘¥™¥•É-•åÌ¹½¹ÑÉ½°¤¤(€€€€€€€ì(€€€€€€€€€€€I•‘¼ ¤ì(€€€€€€€€€€€”¹!…¹‘±•€ôÑÉÕ”ì(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥½µÁ±•Ñ” ¤(€€€ì(€€€€€€€¥˜€ …}Í•±•Ñ¥½¹I•…‘ä¤(€€€€€€€ì(€€€€€€€€€€€É•ÑÕÉ¸ì(€€€€€€€ô((€€€€€€€ÕÍ¥¹œÙ…ÈÉ½À€ôÉ½ÁÉ½é•¹M•±•Ñ¥½¸ ¤ì(€€€€€€€‘¥Ñ•‘	¥Ñµ…Àü¹¥ÍÁ½Í” ¤ì(€€€€€€€‘¥Ñ•‘	¥Ñµ…À€ô…ÁÑÕÉ•¹¹½Ñ…Ñ¥½¹I•¹‘•É•È¹I•¹‘•È (€€€€€€€€€€€É½À°(€€€€€€€€€€€}…¹¹½Ñ…Ñ¥½¹Ì°(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹]¥‘Ñ °(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹!•¥¡Ð¤ì(€€€€€€€¥…±½I•ÍÕ±Ð€ôÑÉÕ”ì(€€€ô((€€€ÁÉ¥Ù…Ñ”É…Ý¥¹	¥Ñµ…ÀÉ½ÁÉ½é•¹M•±•Ñ¥½¸ ¤(€€€ì(€€€€€€€Ù…ÈÍ…±•`€ô}‘•Í­Ñ½Á	¥Ñµ…À¹]¥‘Ñ €¼5…Ñ ¹5…à Å°]¥‘Ñ ¤ì(€€€€€€€Ù…ÈÍ…±•d€ô}‘•Í­Ñ½Á	¥Ñµ…À¹!•¥¡Ð€¼5…Ñ ¹5…à Å°!•¥¡Ð¤ì(€€€€€€€Ù…È±•™Ð€ô5…Ñ ¹±…µÀ (€€€€€€€€€€€€¡¥¹Ð¥5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹1•™Ð€¨Í…±•`¤°(€€€€€€€€€€€€À°(€€€€€€€€€€€}‘•Í­Ñ½Á	¥Ñµ…À¹]¥‘Ñ €´€Ä¤ì(€€€€€€€Ù…ÈÑ½À€ô5…Ñ ¹±…µÀ (€€€€€€€€€€€€¡¥¹Ð¥5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹Q½À€¨Í…±•d¤°(€€€€€€€€€€€€À°(€€€€€€€€€€€}‘•Í­Ñ½Á	¥Ñµ…À¹!•¥¡Ð€´€Ä¤ì(€€€€€€€Ù…ÈÝ¥‘Ñ €ô5…Ñ ¹±…µÀ (€€€€€€€€€€€€¡¥¹Ð¥5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹]¥‘Ñ €¨Í…±•`¤°(€€€€€€€€€€€€Ä°(€€€€€€€€€€€}‘•Í­Ñ½Á	¥Ñµ…À¹]¥‘Ñ €´±•™Ð¤ì(€€€€€€€Ù…È¡•¥¡Ð€ô5…Ñ ¹±…µÀ (€€€€€€€€€€€€¡¥¹Ð¥5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹!•¥¡Ð€¨Í…±•d¤°(€€€€€€€€€€€€Ä°(€€€€€€€€€€€}‘•Í­Ñ½Á	¥Ñµ…À¹!•¥¡Ð€´Ñ½À¤ì(€€€€€€€É•ÑÕÉ¸}‘•Í­Ñ½Á	¥Ñµ…À¹±½¹” (€€€€€€€€€€€¹•ÜMåÍÑ•´¹É…Ý¥¹œ¹I•Ñ…¹±”¡±•™Ð°Ñ½À°Ý¥‘Ñ °¡•¥¡Ð¤°(€€€€€€€€€€€É…Ý¥¹A¥á•±½Éµ…Ð¹½Éµ…ÐÌÉ‰ÁÁÉˆ¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥I•Í•ÑM•±•Ñ¥½¸ ¤(€€€ì(€€€€€€€}‘É…¥¹œ€ô™…±Í”ì(€€€€€€€}‘É…Ý¥¹œ€ô™…±Í”ì(€€€€€€€}Í•±•Ñ¥½¹I•…‘ä€ô™…±Í”ì(€€€€€€€I•±•…Í•5½ÕÍ•…ÁÑÕÉ” ¤ì(€€€€€€€}Í•±•Ñ¥½¸¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•ì(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•ì(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹¡¥±‘É•¸¹±•…È ¤ì(€€€€€€€}Í¥é•	…‘”¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•ì(€€€€€€€}Ñ½½±‰…È¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•ì(€€€€€€€M•Ñ!…¹‘±•ÍY¥Í¥‰¥±¥Ñä¡Y¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•¤ì(€€€€€€€}…¹¹½Ñ…Ñ¥½¹Ì¹±•…È ¤ì(€€€€€€€}É•‘¼¹±•…È ¤ì(€€€€€€€}¹•áÑ9Õµ‰•È€ô€Äì(€€€€€€€ÕÉÍ½È€ôÕÉÍ½ÉÌ¹É½ÍÌì(€€€€€€€UÁ‘…Ñ•M¡…‘”¡‘•™…Õ±Ð¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥UÁ‘…Ñ•M•±•Ñ¥½¸¡A½¥¹Ð•¹¤(€€€ì(€€€€€€€}±½…±M•±•Ñ¥½¸€ô9½Éµ…±¥é”¡}ÍÑ…ÉÐ°•¹¤ì(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}Í•±•Ñ¥½¸°}±½…±M•±•Ñ¥½¸¹1•™Ð¤ì(€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡}Í•±•Ñ¥½¸°}±½…±M•±•Ñ¥½¸¹Q½À¤ì(€€€€€€€}Í•±•Ñ¥½¸¹]¥‘Ñ €ô}±½…±M•±•Ñ¥½¸¹]¥‘Ñ ì(€€€€€€€}Í•±•Ñ¥½¸¹!•¥¡Ð€ô}±½…±M•±•Ñ¥½¸¹!•¥¡Ðì(€€€€€€€UÁ‘…Ñ•M¡…‘”¡}±½…±M•±•Ñ¥½¸¤ì((€€€€€€€¥˜€¡}Í¥é•	…‘”¹¡¥±¥ÌQ•áÑ	±½¬Í¥é”¤(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÍ…±•`€ô}‘•Í­Ñ½Á	¥Ñµ…À¹]¥‘Ñ €¼5…Ñ ¹5…à Å°]¥‘Ñ ¤ì(€€€€€€€€€€€Ù…ÈÍ…±•d€ô}‘•Í­Ñ½Á	¥Ñµ…À¹!•¥¡Ð€¼5…Ñ ¹5…à Å°!•¥¡Ð¤ì(€€€€€€€€€€€Í¥é”¹Q•áÐ€ô(€€€€€€€€€€€€€€€€‰í5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹]¥‘Ñ €¨Í…±•`¤é8Áôƒ\€ˆ€¬(€€€€€€€€€€€€€€€€‰í5…Ñ ¹I½Õ¹¡}±½…±M•±•Ñ¥½¸¹!•¥¡Ð€¨Í…±•d¤é8Áôˆì(€€€€€€€ô(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}Í¥é•	…‘”°}±½…±M•±•Ñ¥½¸¹1•™Ð¤ì(€€€€€€€…¹Ù…Ì¹M•ÑQ½À (€€€€€€€€€€€}Í¥é•	…‘”°(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹Q½À€ø€ÐÀ(€€€€€€€€€€€€€€€€ü}±½…±M•±•Ñ¥½¸¹Q½À€´€ÌÐ(€€€€€€€€€€€€€€€€è}±½…±M•±•Ñ¥½¸¹	½ÑÑ½´€¬€à¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥A½Í¥Ñ¥½¹¹¹½Ñ…Ñ¥½¹1…å•È ¤(€€€ì(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}…¹¹½Ñ…Ñ¥½¹1…å•È°}±½…±M•±•Ñ¥½¸¹1•™Ð¤ì(€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡}…¹¹½Ñ…Ñ¥½¹1…å•È°}±½…±M•±•Ñ¥½¸¹Q½À¤ì(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹]¥‘Ñ €ô}±½…±M•±•Ñ¥½¸¹]¥‘Ñ ì(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹!•¥¡Ð€ô}±½…±M•±•Ñ¥½¸¹!•¥¡Ðì(€€€€€€€}…¹¹½Ñ…Ñ¥½¹1…å•È¹Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹Y¥Í¥‰±”ì(€€€ô((€€€ÁÉ¥Ù…Ñ”A½¥¹ÐQ½¹¹½Ñ…Ñ¥½¹A½¥¹Ð¡A½¥¹Ð…¹Ù…ÍA½¥¹Ð¤€ôø¹•Ü (€€€€€€€5…Ñ ¹±…µÀ¡…¹Ù…ÍA½¥¹Ð¹`€´}±½…±M•±•Ñ¥½¸¹1•™Ð°€À°}±½…±M•±•Ñ¥½¸¹]¥‘Ñ ¤°(€€€€€€€5…Ñ ¹±…µÀ¡…¹Ù…ÍA½¥¹Ð¹d€´}±½…±M•±•Ñ¥½¸¹Q½À°€À°}±½…±M•±•Ñ¥½¸¹!•¥¡Ð¤¤ì((€€€ÁÉ¥Ù…Ñ”Ù½¥UÁ‘…Ñ•M¡…‘”¡I•Ð±•…È¤(€€€ì(€€€€€€€Ù…ÈÝ¥‘Ñ €ô5…Ñ ¹5…à À°ÑÕ…±]¥‘Ñ €ø€À€üÑÕ…±]¥‘Ñ €è]¥‘Ñ ¤ì(€€€€€€€Ù…È¡•¥¡Ð€ô5…Ñ ¹5…à À°ÑÕ…±!•¥¡Ð€ø€À€üÑÕ…±!•¥¡Ð€è!•¥¡Ð¤ì(€€€€€€€¥˜€¡±•…È¹%ÍµÁÑäñð±•…È¹]¥‘Ñ €ðô€Àñð±•…È¹!•¥¡Ð€ðô€À¤(€€€€€€€ì(€€€€€€€€€€€A±…”¡}Í¡…‘•ÍlÁt°€À°€À°Ý¥‘Ñ °¡•¥¡Ð¤ì(€€€€€€€€€€€™½È€¡Ù…È¥¹‘•à€ô€Äì¥¹‘•à€ð}Í¡…‘•Ì¹1•¹Ñ ì¥¹‘•à¬¬¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€A±…”¡}Í¡…‘•Ím¥¹‘•át°€À°€À°€À°€À¤ì(€€€€€€€€€€€ô(€€€€€€€€€€€É•ÑÕÉ¸ì(€€€€€€€ô((€€€€€€€A±…”¡}Í¡…‘•ÍlÁt°€À°€À°Ý¥‘Ñ °±•…È¹Q½À¤ì(€€€€€€€A±…”¡}Í¡…‘•ÍlÅt°€À°±•…È¹	½ÑÑ½´°Ý¥‘Ñ °¡•¥¡Ð€´±•…È¹	½ÑÑ½´¤ì(€€€€€€€A±…”¡}Í¡…‘•ÍlÉt°€À°±•…È¹Q½À°±•…È¹1•™Ð°±•…È¹!•¥¡Ð¤ì(€€€€€€€A±…” (€€€€€€€€€€€}Í¡…‘•ÍlÍt°(€€€€€€€€€€€±•…È¹I¥¡Ð°(€€€€€€€€€€€±•…È¹Q½À°(€€€€€€€€€€€Ý¥‘Ñ €´±•…È¹I¥¡Ð°(€€€€€€€€€€€±•…È¹!•¥¡Ð¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥A½Í¥Ñ¥½¹!…¹‘±•Ì ¤(€€€ì(€€€€€€€Ù…Èà€ô¹•Ýmt(€€€€€€€ì(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹1•™Ð°(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹1•™Ð€¬€¡}±½…±M•±•Ñ¥½¸¹]¥‘Ñ €¼€È¤°(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹I¥¡Ð(€€€€€€€ôì(€€€€€€€Ù…Èä€ô¹•Ýmt(€€€€€€€ì(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹Q½À°(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹Q½À€¬€¡}±½…±M•±•Ñ¥½¸¹!•¥¡Ð€¼€È¤°(€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹	½ÑÑ½´(€€€€€€€ôì(€€€€€€€Ù…ÈÁ½Í¥Ñ¥½¹Ì€ô¹•Ýmt(€€€€€€€ì(€€€€€€€€€€€¹•ÜA½¥¹Ð¡álÁt°ålÁt¤°¹•ÜA½¥¹Ð¡álÅt°ålÁt¤°(€€€€€€€€€€€¹•ÜA½¥¹Ð¡álÉt°ålÁt¤°¹•ÜA½¥¹Ð¡álÁt°ålÅt¤°(€€€€€€€€€€€¹•ÜA½¥¹Ð¡álÉt°ålÅt¤°¹•ÜA½¥¹Ð¡álÁt°ålÉt¤°(€€€€€€€€€€€¹•ÜA½¥¹Ð¡álÅt°ålÉt¤°¹•ÜA½¥¹Ð¡álÉt°ålÉt¤(€€€€€€€ôì(€€€€€€€™½È€¡Ù…È¥¹‘•à€ô€Àì¥¹‘•à€ð}¡…¹‘±•Ì¹1•¹Ñ ì¥¹‘•à¬¬¤(€€€€€€€ì(€€€€€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}¡…¹‘±•Ím¥¹‘•át°Á½Í¥Ñ¥½¹Ím¥¹‘•át¹`€´€Ô¤ì(€€€€€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡}¡…¹‘±•Ím¥¹‘•át°Á½Í¥Ñ¥½¹Ím¥¹‘•át¹d€´€Ô¤ì(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥A½Í¥Ñ¥½¹Q½½±‰…È ¤(€€€ì(€€€€€€€}Ñ½½±‰…È¹5•…ÍÕÉ”¡¹•ÜM¥é”¡‘½Õ‰±”¹A½Í¥Ñ¥Ù•%¹™¥¹¥Ñä°‘½Õ‰±”¹A½Í¥Ñ¥Ù•%¹™¥¹¥Ñä¤¤ì(€€€€€€€Ù…È‘•Í¥É•€ô}Ñ½½±‰…È¹•Í¥É•‘M¥é”ì(€€€€€€€Ù…ÈÝ¥‘Ñ €ôÑÕ…±]¥‘Ñ €ø€À€üÑÕ…±]¥‘Ñ €è]¥‘Ñ ì(€€€€€€€Ù…È¡•¥¡Ð€ôÑÕ…±!•¥¡Ð€ø€À€üÑÕ…±!•¥¡Ð€è!•¥¡Ðì(€€€€€€€Ù…È±•™Ð€ô5…Ñ ¹±…µÀ (€€€€€€€€€€€}±½…±M•±•Ñ¥½¸¹1•™Ð€¬€ ¡}±½…±M•±•Ñ¥½¸¹]¥‘Ñ €´‘•Í¥É•¹]¥‘Ñ ¤€¼€È¤°(€€€€€€€€€€€€ÄÈ°(€€€€€€€€€€€5…Ñ ¹5…à ÄÈ°Ý¥‘Ñ €´‘•Í¥É•¹]¥‘Ñ €´€ÄÈ¤¤ì(€€€€€€€Ù…È‰•±½Ü€ô}±½…±M•±•Ñ¥½¸¹	½ÑÑ½´€¬€ÄÐì(€€€€€€€Ù…ÈÑ½À€ô‰•±½Ü€¬‘•Í¥É•¹!•¥¡Ð€ðô¡•¥¡Ð€´€ÄÈ(€€€€€€€€€€€€ü‰•±½Ü(€€€€€€€€€€€€è5…Ñ ¹5…à ÄÈ°}±½…±M•±•Ñ¥½¸¹Q½À€´‘•Í¥É•¹!•¥¡Ð€´€ÄÐ¤ì(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡}Ñ½½±‰…È°±•™Ð¤ì(€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡}Ñ½½±‰…È°Ñ½À¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”Ù½¥M•Ñ!…¹‘±•ÍY¥Í¥‰¥±¥Ñä¡Y¥Í¥‰¥±¥ÑäÙ¥Í¥‰¥±¥Ñä¤(€€€ì(€€€€€€€™½É•… €¡Ù…È¡…¹‘±”¥¸}¡…¹‘±•Ì¤(€€€€€€€ì(€€€€€€€€€€€¡…¹‘±”¹Y¥Í¥‰¥±¥Ñä€ôÙ¥Í¥‰¥±¥Ñäì(€€€€€€€ô(€€€ô((€€€ÁÉ¥Ù…Ñ”‰½½°%ÍQ½½±‰…ÉM½ÕÉ”¡•Á•¹‘•¹å=‰©•ÐüÍ½ÕÉ”¤(€€€ì(€€€€€€€Ý¡¥±”€¡Í½ÕÉ”¥Ì¹½Ð¹Õ±°¤(€€€€€€€ì(€€€€€€€€€€€¥˜€¡I•™•É•¹•ÅÕ…±Ì¡Í½ÕÉ”°}Ñ½½±‰…È¤¤(€€€€€€€€€€€ì(€€€€€€€€€€€€€€€É•ÑÕÉ¸ÑÉÕ”ì(€€€€€€€€€€€ô(€€€€€€€€€€€Í½ÕÉ”€ôY¥ÍÕ…±QÉ••!•±Á•È¹•ÑA…É•¹Ð¡Í½ÕÉ”¤ì(€€€€€€€ô(€€€€€€€É•ÑÕÉ¸™…±Í”ì(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒI•Ñ…¹±”M¡…‘” ¤€ôø¹•Ü ¤(€€€ì(€€€€€€€¥±°€ô¹•ÜM½±¥‘½±½É	ÉÕÍ ¡½±½È¹É½µÉˆ ÄÔÀ°€Ì°€ÄÈ°€ÈÈ¤¤°(€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”(€€€ôì((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ	½É‘•È!…¹‘±” ¤€ôø¹•Ü ¤(€€€ì(€€€€€€€]¥‘Ñ €ô€ÄÀ°(€€€€€€€!•¥¡Ð€ô€ÄÀ°(€€€€€€€½É¹•ÉI…‘¥ÕÌ€ô¹•Ü½É¹•ÉI…‘¥ÕÌ Ô¤°(€€€€€€€	…­É½Õ¹€ô	ÉÕÍ¡•Ì¹]¡¥Ñ”°(€€€€€€€	½É‘•É	ÉÕÍ €ô¹•ÜM½±¥‘½±½É	ÉÕÍ ¡½±½È¹É½µIˆ ÄÀ°€ÄØä°€ÄàÜ¤¤°(€€€€€€€	½É‘•ÉQ¡¥­¹•ÍÌ€ô¹•ÜQ¡¥­¹•ÍÌ È¤°(€€€€€€€Y¥Í¥‰¥±¥Ñä€ôY¥Í¥‰¥±¥Ñä¹½±±…ÁÍ•°(€€€€€€€%Í!¥ÑQ•ÍÑY¥Í¥‰±”€ô™…±Í”(€€€ôì((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÙ½¥A±…” (€€€€€€€É…µ•Ý½É­±•µ•¹Ð•±•µ•¹Ð°(€€€€€€€‘½Õ‰±”±•™Ð°(€€€€€€€‘½Õ‰±”Ñ½À°(€€€€€€€‘½Õ‰±”Ý¥‘Ñ °(€€€€€€€‘½Õ‰±”¡•¥¡Ð¤(€€€ì(€€€€€€€…¹Ù…Ì¹M•Ñ1•™Ð¡•±•µ•¹Ð°±•™Ð¤ì(€€€€€€€…¹Ù…Ì¹M•ÑQ½À¡•±•µ•¹Ð°Ñ½À¤ì(€€€€€€€•±•µ•¹Ð¹]¥‘Ñ €ô5…Ñ ¹5…à À°Ý¥‘Ñ ¤ì(€€€€€€€•±•µ•¹Ð¹!•¥¡Ð€ô5…Ñ ¹5…à À°¡•¥¡Ð¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒI•Ð9½Éµ…±¥é”¡A½¥¹ÐÍÑ…ÉÐ°A½¥¹Ð•¹¤€ôø¹•Ü (€€€€€€€5…Ñ ¹5¥¸¡ÍÑ…ÉÐ¹`°•¹¹`¤°(€€€€€€€5…Ñ ¹5¥¸¡ÍÑ…ÉÐ¹d°•¹¹d¤°(€€€€€€€5…Ñ ¹‰Ì¡•¹¹`€´ÍÑ…ÉÐ¹`¤°(€€€€€€€5…Ñ ¹‰Ì¡•¹¹d€´ÍÑ…ÉÐ¹d¤¤ì((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒM½±¥‘½±½É	ÉÕÍ 	ÉÕÍ ¡ÍÑÉ¥¹œ½±½È¤€ôø(€€€€€€€¹•Ü ¡½±½È¥½±½É½¹Ù•ÉÑ•È¹½¹Ù•ÉÑÉ½µMÑÉ¥¹œ¡½±½È¤¤ì((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÉ…Ý¥¹	¥Ñµ…À…ÁÑÕÉ•Y¥ÉÑÕ…±•Í­Ñ½Á	¥Ñµ…À ¤€ôø(€€€€€€€…ÁÑÕÉ•Y¥ÉÑÕ…±•Í­Ñ½Á	¥Ñµ…À¡™…±Í”¤ì((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥ŒÉ…Ý¥¹	¥Ñµ…À…ÁÑÕÉ•Y¥ÉÑÕ…±•Í­Ñ½Á	¥Ñµ…À¡‰½½°¥¹±Õ‘•ÕÉÍ½È¤(€€€ì(€€€€€€€Ù…ÈÙ¥ÉÑÕ…±MÉ••¸€ôMåÍÑ•´¹]¥¹‘½ÝÌ¹½ÉµÌ¹MåÍÑ•µ%¹™½Éµ…Ñ¥½¸¹Y¥ÉÑÕ…±MÉ••¸ì(€€€€€€€É•ÑÕÉ¸…ÁÑÕÉ•M•ÉÙ¥”¹…ÁÑÕÉ•	¥Ñµ…À¡Ù¥ÉÑÕ…±MÉ••¸°¥¹±Õ‘•ÕÉÍ½È¤ì(€€€ô((€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ	¥Ñµ…ÁM½ÕÉ”Q½	¥Ñµ…ÁM½ÕÉ”¡É…Ý¥¹	¥Ñµ…À‰¥Ñµ…À¤(€€€ì(€€€€€€€Ù…È¡…¹‘±”€ô‰¥Ñµ…À¹•Ñ!‰¥Ñµ…À ¤ì(€€€€€€€ÑÉä(€€€€€€€ì(€€€€€€€€€€€Ù…ÈÍ½ÕÉ”€ô%µ…¥¹œ¹É•…Ñ•	¥Ñµ…ÁM½ÕÉ•É½µ!	¥Ñµ…À (€€€€€€€€€€€€€€€¡…¹‘±”°(€€€€€€€€€€€€€€€%¹ÑAÑÈ¹i•É¼°(€€€€€€€€€€€€€€€%¹ÐÌÉI•Ð¹µÁÑä°(€€€€€€€€€€€€€€€	¥Ñµ…ÁM¥é•=ÁÑ¥½¹Ì¹É½µµÁÑå=ÁÑ¥½¹Ì ¤¤ì(€€€€€€€€€€€Í½ÕÉ”¹É••é” ¤ì(€€€€€€€€€€€É•ÑÕÉ¸Í½ÕÉ”ì(€€€€€€€ô(€€€€€€€™¥¹…±±ä(€€€€€€€ì(€€€€€€€€€€€|€ô•±•Ñ•=‰©•Ð¡¡…¹‘±”¤ì(€€€€€€€ô(€€€ô((€€€m±±%µÁ½ÉÐ ‰‘¤ÌÈ¹‘±°ˆ¥t(€€€mÉ•ÑÕÉ¸è5…ÉÍ¡…±Ì¡U¹µ…¹…•‘QåÁ”¹	½½°¥t(€€€ÁÉ¥Ù…Ñ”ÍÑ…Ñ¥Œ•áÑ•É¸‰½½°•±•Ñ•=‰©•Ð¡%¹ÑAÑÈ¡…¹‘±”¤ì)ô(