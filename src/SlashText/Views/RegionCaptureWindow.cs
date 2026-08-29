using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private readonly CaptureAnnotationHistory _annotationHistory = new();
    private readonly Dictionary<CaptureAnnotationKind, ToggleButton> _toolButtons = [];
    private readonly List<Point> _pencilPoints = [];
    private readonly bool _isDark = ThemeService.IsDark;
    private Point _start;
    private Point _annotationStart;
    private Rect _localSelection;
    private bool _dragging;
    private bool _drawing;
    private bool _selectionReady;
    private readonly CaptureToolSelection _toolSelection =
        new(CaptureAnnotationKind.Arrow);
    private CaptureAnnotationKind _tool
    {
        get => _toolSelection.Selected;
        set => _toolSelection.Select(value);
    }
    private int _color = DrawingColor.Red.ToArgb();
    private float _thickness = 4;
    private float _opacity = 1;
    private int? _fillArgb;
    private int? _outlineArgb = DrawingColor.Red.ToArgb();
    private float _annotationSize = 32;
    private bool _textBold = true;
    private string _selectedStamp = "❤️";
    private int _nextNumber = 1;
    private Grid _toolbarLayout = null!;
    private bool _toolbarPositionPending;
    private Window? _contextWindow;
    private MonitorWorkArea _activeMonitor;
    private Rect _toolbarBoundsPixels;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _eraseButton = null!;
    private Button _reselectButton = null!;
    private Button _cancelButton = null!;
    private Button _overflowButton = null!;
    private Border _captureSeparator = null!;
    private Border _actionSeparator = null!;
    private bool _compactToolbar;

    public DrawingBitmap? EditedBitmap { get; private set; }
    public CaptureEditorOutput RequestedOutput { get; private set; } = CaptureEditorOutput.Default;

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
            HideContextWindow(reactivateOverlay: false);
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
        var root = new Grid
        {
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _toolbarLayout = root;
        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        tools.Children.Add(BuildCaptureSplitButton());
        _captureSeparator = Separator();
        tools.Children.Add(_captureSeparator);
        tools.Children.Add(ToolButton("CaptureIconArrow", "Seta", CaptureAnnotationKind.Arrow));
        tools.Children.Add(ToolButton("CaptureIconHighlighter", "Marca-texto", CaptureAnnotationKind.Highlighter));
        tools.Children.Add(ToolButton("CaptureIconShapes", "Formas", CaptureAnnotationKind.Rectangle));
        tools.Children.Add(ToolButton("CaptureIconPencil", "Lápis", CaptureAnnotationKind.Pencil));
        tools.Children.Add(ToolButton("CaptureIconText", "Texto", CaptureAnnotationKind.Text));
        tools.Children.Add(ToolButton("CaptureIconNumber", "Número", CaptureAnnotationKind.Number));
        _eraseButton = IconButton("CaptureIconEraser", "Apagar todas as marcações", (_, _) => ClearAllAnnotations());
        tools.Children.Add(_eraseButton);
        _actionSeparator = Separator();
        tools.Children.Add(_actionSeparator);
        _undoButton = IconButton("CaptureIconUndo", "Desfazer (Ctrl+Z)", (_, _) => Undo());
        _redoButton = IconButton("CaptureIconRedo", "Refazer (Ctrl+Y)", (_, _) => Redo());
        tools.Children.Add(_undoButton);
        tools.Children.Add(_redoButton);
        _reselectButton = IconButton("CaptureIconReselect", "Refazer seleção (R)", (_, _) => ResetSelection());
        tools.Children.Add(_reselectButton);
        _overflowButton = IconButton("CaptureIconMore", "Mais ferramentas", (_, _) => ShowOverflowMenu());
        _overflowButton.Visibility = Visibility.Collapsed;
        tools.Children.Add(_overflowButton);
        _cancelButton = IconButton("CaptureIconClose", "Cancelar captura (Esc)", (_, _) => DialogResult = false);
        tools.Children.Add(_cancelButton);
        root.Children.Add(tools);

        var toolbar = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = ResourceBrush("CaptureToolbarSurfaceBrush"),
            BorderBrush = ResourceBrush("CaptureToolbarBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(6, 4, 6, 4),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 3,
                Opacity = .28,
                Color = Colors.Black
            },
            Child = root
        };
        return toolbar;
    }

    private ToggleButton ToolButton(
        string geometryKey,
        string toolTip,
        CaptureAnnotationKind tool)
    {
        var button = new ToggleButton
        {
            Style = (Style)FindResource("CaptureToolbarToggleButton"),
            Content = Icon(geometryKey),
            ToolTip = toolTip
        };
        AutomationProperties.SetName(button, toolTip);
        button.Click += (_, _) =>
        {
            var repeated = _tool == tool;
            _tool = tool;
            Cursor = tool == CaptureAnnotationKind.Text ? Cursors.IBeam : Cursors.Cross;
            UpdateToolSelection();
            if (repeated || tool is CaptureAnnotationKind.Rectangle or
                    CaptureAnnotationKind.Text or CaptureAnnotationKind.Number)
            {
                ShowToolContext(tool);
            }
        };
        _toolButtons[tool] = button;
        return button;
    }

    private Button IconButton(
        string geometryKey,
        string toolTip,
        RoutedEventHandler click)
    {
        var button = new Button
        {
            Style = (Style)FindResource("CaptureToolbarIconButton"),
            Content = Icon(geometryKey),
            ToolTip = toolTip,
        };
        AutomationProperties.SetName(button, toolTip);
        button.Click += click;
        return button;
    }

    private Border BuildCaptureSplitButton()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var capture = new Button
        {
            Content = "Capturar",
            Width = 88,
            Height = 36,
            Background = ResourceBrush("CaptureToolbarAccentBrush"),
            Foreground = Brushes.White,
            BorderBrush = Brushes.Transparent,
            FontWeight = FontWeights.SemiBold,
            Padding = new Thickness(12, 5, 10, 5),
            ToolTip = "Concluir conforme configuração"
        };
        AutomationProperties.SetName(capture, "Capturar conforme configuração");
        capture.Click += (_, _) => Complete(CaptureEditorOutput.Default);
        var menu = new Button
        {
            Content = new Path
            {
                Data = Geometry.Parse("M3,6 L7,10 L11,6"),
                Stroke = Brushes.White,
                StrokeThickness = 1.6,
                Stretch = Stretch.Uniform
            },
            Width = 30,
            Height = 36,
            Background = ResourceBrush("CaptureToolbarAccentBrush"),
            Foreground = Brushes.White,
            BorderBrush = ResourceBrush("CaptureToolbarSelectedBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Padding = new Thickness(8),
            ToolTip = "Opções de captura"
        };
        AutomationProperties.SetName(menu, "Abrir opções de captura");
        menu.Click += (_, _) => ShowCaptureMenu();
        panel.Children.Add(capture);
        panel.Children.Add(menu);
        return new Border
        {
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = panel
        };
    }

    private static Path Icon(string geometryKey) => new()
    {
        Data = (Geometry)Application.Current.FindResource(geometryKey),
        Stroke = ResourceBrush("CaptureToolbarTextBrush"),
        StrokeThickness = 1.6,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Stretch = Stretch.Uniform,
        Width = 18,
        Height = 18,
        IsHitTestVisible = false
    };

    private static SolidColorBrush ResourceBrush(string key) =>
        (SolidColorBrush)Application.Current.FindResource(key);

    private Border Separator() => new()
    {
        Width = 1,
        Height = 24,
        Margin = new Thickness(5, 6, 5, 6),
        Background = ResourceBrush("CaptureToolbarBorderBrush")
    };

    private void ShowCaptureMenu()
    {
        var panel = ContextStack(220);
        panel.Children.Add(ContextAction("Concluir conforme configuração", () => Complete(CaptureEditorOutput.Default)));
        panel.Children.Add(ContextAction("Copiar", () => Complete(CaptureEditorOutput.Clipboard)));
        panel.Children.Add(ContextAction("Salvar", () => Complete(CaptureEditorOutput.File)));
        ShowContextWindow(panel, 220);
    }

    private void ShowOverflowMenu()
    {
        var panel = ContextStack(260);
        panel.Children.Add(ContextTitle("Ferramentas"));
        AddOverflowTool(panel, "Seta", CaptureAnnotationKind.Arrow);
        AddOverflowTool(panel, "Marca-texto", CaptureAnnotationKind.Highlighter);
        AddOverflowTool(panel, "Formas", CaptureAnnotationKind.Rectangle);
        AddOverflowTool(panel, "Lápis", CaptureAnnotationKind.Pencil);
        AddOverflowTool(panel, "Texto", CaptureAnnotationKind.Text);
        AddOverflowTool(panel, "Número", CaptureAnnotationKind.Number);
        panel.Children.Add(ContextAction("Apagar todas as marcações", ClearAllAnnotations));
        panel.Children.Add(ContextAction("Refazer seleção", ResetSelection));
        ShowContextWindow(panel, 260);
    }

    private void AddOverflowTool(
        Panel panel,
        string label,
        CaptureAnnotationKind tool)
    {
        panel.Children.Add(ContextAction(label, () =>
        {
            _tool = tool;
            Cursor = tool == CaptureAnnotationKind.Text ? Cursors.IBeam : Cursors.Cross;
            UpdateToolSelection();
            ShowToolContext(tool);
        }));
    }

    private void ShowToolContext(CaptureAnnotationKind tool)
    {
        FrameworkElement content = tool switch
        {
            CaptureAnnotationKind.Rectangle or CaptureAnnotationKind.Ellipse or
                CaptureAnnotationKind.Line => BuildShapesContext(),
            CaptureAnnotationKind.Stamp => BuildStampContext(),
            _ => BuildStrokeContext(tool)
        };
        ShowContextWindow(content, tool == CaptureAnnotationKind.Stamp ? 300 : 340);
    }

    private FrameworkElement BuildStrokeContext(CaptureAnnotationKind tool)
    {
        var panel = ContextStack(320);
        panel.Children.Add(ContextTitle(tool switch
        {
            CaptureAnnotationKind.Highlighter => "Marca-texto",
            CaptureAnnotationKind.Pencil => "Lápis",
            CaptureAnnotationKind.Text => "Texto",
            CaptureAnnotationKind.Number => "Número",
            _ => "Seta"
        }));
        var preview = new Line
        {
            X1 = 10,
            Y1 = 22,
            X2 = 280,
            Y2 = 22,
            Stroke = WpfBrush(_color, _opacity),
            StrokeThickness = tool == CaptureAnnotationKind.Highlighter ? _thickness * 4 : _thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };
        var previewCanvas = new Canvas { Height = 44, Margin = new Thickness(0, 5, 0, 5) };
        previewCanvas.Children.Add(preview);
        var colors = tool == CaptureAnnotationKind.Highlighter
            ? HighlightColors
            : AnnotationColors;
        panel.Children.Add(ColorPalette(colors, _color, color =>
        {
            _color = color;
            _outlineArgb = color;
            preview.Stroke = WpfBrush(color, _opacity);
        }));
        panel.Children.Add(LabeledSlider("Opacidade", 10, 100, _opacity * 100, value =>
        {
            _opacity = (float)(value / 100d);
            preview.Stroke = WpfBrush(_color, _opacity);
        }, "%"));
        if (tool == CaptureAnnotationKind.Text)
        {
            panel.Children.Add(LabeledSlider("Tamanho", 12, 64, _annotationSize, value =>
                _annotationSize = (float)value, " px"));
            var bold = new CheckBox
            {
                Content = "Negrito",
                IsChecked = _textBold,
                Foreground = ResourceBrush("CaptureToolbarTextBrush"),
                Margin = new Thickness(0, 7, 0, 0)
            };
            bold.Checked += (_, _) => _textBold = true;
            bold.Unchecked += (_, _) => _textBold = false;
            panel.Children.Add(bold);
        }
        else if (tool == CaptureAnnotationKind.Number)
        {
            panel.Children.Add(LabeledSlider("Tamanho", 24, 64, _annotationSize, value =>
                _annotationSize = (float)value, " px"));
            panel.Children.Add(ContextAction("Reiniciar numeração", () => _nextNumber = 1));
        }
        else
        {
            panel.Children.Add(LabeledSlider("Espessura", 2, 12, _thickness, value =>
            {
                _thickness = (float)value;
                preview.StrokeThickness = tool == CaptureAnnotationKind.Highlighter
                    ? _thickness * 4
                    : _thickness;
            }, " px"));
        }
        panel.Children.Add(previewCanvas);
        return panel;
    }

    private FrameworkElement BuildShapesContext()
    {
        var panel = ContextStack(360);
        panel.Children.Add(ContextTitle("Formas"));
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        row.Children.Add(ContextTool("Emoticons", CaptureAnnotationKind.Stamp));
        row.Children.Add(ContextTool("Retângulo", CaptureAnnotationKind.Rectangle));
        row.Children.Add(ContextTool("Elipse", CaptureAnnotationKind.Ellipse));
        row.Children.Add(ContextTool("Linha", CaptureAnnotationKind.Line));
        row.Children.Add(ContextTool("Seta", CaptureAnnotationKind.Arrow));
        panel.Children.Add(row);
        panel.Children.Add(ContextTitle("Preenchimento"));
        panel.Children.Add(ColorPalette(AnnotationColors, _fillArgb, color =>
        {
            _fillArgb = color == 0 ? null : color;
            if (!_fillArgb.HasValue && !_outlineArgb.HasValue) _outlineArgb = _color;
        }, allowNone: true));
        panel.Children.Add(ContextTitle("Contorno"));
        panel.Children.Add(ColorPalette(AnnotationColors, _outlineArgb, color =>
        {
            _outlineArgb = color == 0 ? null : color;
            if (!_fillArgb.HasValue && !_outlineArgb.HasValue) _fillArgb = _color;
            _color = _outlineArgb ?? _fillArgb ?? _color;
        }, allowNone: true));
        panel.Children.Add(LabeledSlider("Opacidade", 10, 100, _opacity * 100,
            value => _opacity = (float)(value / 100d), "%"));
        panel.Children.Add(LabeledSlider("Espessura", 2, 12, _thickness,
            value => _thickness = (float)value, " px"));
        return panel;
    }

    private FrameworkElement BuildStampContext()
    {
        var panel = ContextStack(280);
        panel.Children.Add(ContextTitle("Emoticons e carimbos"));
        var grid = new UniformGrid { Columns = 6 };
        foreach (var stamp in StampValues)
        {
            var value = stamp;
            var button = new Button
            {
                Content = value,
                FontFamily = new FontFamily("Segoe UI Emoji"),
                FontSize = 20,
                Width = 40,
                Height = 38,
                Margin = new Thickness(2),
                Background = value == _selectedStamp
                    ? ResourceBrush("CaptureToolbarSelectedBrush")
                    : Brushes.Transparent,
                Foreground = ResourceBrush("CaptureToolbarTextBrush"),
                BorderBrush = value == _selectedStamp
                    ? ResourceBrush("CaptureToolbarAccentBrush")
                    : Brushes.Transparent,
                ToolTip = $"Inserir {value}"
            };
            AutomationProperties.SetName(button, $"Emoticon {value}");
            button.Click += (_, _) =>
            {
                _selectedStamp = value;
                _tool = CaptureAnnotationKind.Stamp;
                UpdateToolSelection();
            };
            grid.Children.Add(button);
        }
        panel.Children.Add(grid);
        panel.Children.Add(LabeledSlider("Tamanho", 24, 64, _annotationSize,
            value => _annotationSize = (float)value, " px"));
        return panel;
    }

    private Button ContextTool(string label, CaptureAnnotationKind tool)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 58,
            Height = 32,
            Margin = new Thickness(2),
            Padding = new Thickness(7, 3, 7, 3),
            Background = _tool == tool
                ? ResourceBrush("CaptureToolbarSelectedBrush")
                : Brushes.Transparent,
            Foreground = ResourceBrush("CaptureToolbarTextBrush"),
            BorderBrush = _tool == tool
                ? ResourceBrush("CaptureToolbarAccentBrush")
                : ResourceBrush("CaptureToolbarBorderBrush")
        };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) =>
        {
            _tool = tool;
            UpdateToolSelection();
            if (tool == CaptureAnnotationKind.Stamp) ShowToolContext(tool);
        };
        return button;
    }

    private static StackPanel ContextStack(double width) => new()
    {
        Width = width,
        Margin = new Thickness(14)
    };

    private static TextBlock ContextTitle(string text) => new()
    {
        Text = text,
        Foreground = ResourceBrush("CaptureToolbarTextBrush"),
        FontWeight = FontWeights.SemiBold,
        FontSize = 13,
        Margin = new Thickness(0, 4, 0, 7)
    };

    private Button ContextAction(string label, Action action)
    {
        var button = new Button
        {
            Content = label,
            Height = 34,
            Margin = new Thickness(0, 2, 0, 2),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Brushes.Transparent,
            Foreground = ResourceBrush("CaptureToolbarTextBrush"),
            BorderBrush = Brushes.Transparent
        };
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => action();
        return button;
    }

    private FrameworkElement ColorPalette(
        IReadOnlyList<int> colors,
        int? selected,
        Action<int> select,
        bool allowNone = false)
    {
        var palette = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        if (allowNone)
        {
            var none = new Button
            {
                Content = "∅",
                Width = 30,
                Height = 30,
                Margin = new Thickness(3),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = ResourceBrush("CaptureToolbarSecondaryTextBrush"),
                BorderBrush = !selected.HasValue
                    ? ResourceBrush("CaptureToolbarAccentBrush")
                    : ResourceBrush("CaptureToolbarBorderBrush"),
                ToolTip = "Sem cor"
            };
            AutomationProperties.SetName(none, "Sem cor");
            none.Click += (_, _) => select(0);
            palette.Children.Add(none);
        }
        foreach (var value in colors)
        {
            var color = DrawingColor.FromArgb(value);
            var choice = new Button
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(3),
                Padding = new Thickness(3),
                Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)),
                BorderBrush = selected == value
                    ? ResourceBrush("CaptureToolbarAccentBrush")
                    : ResourceBrush("CaptureToolbarBorderBrush"),
                BorderThickness = new Thickness(selected == value ? 3 : 1),
                ToolTip = $"Selecionar cor {color.Name}"
            };
            AutomationProperties.SetName(choice, $"Cor {color.Name}");
            choice.Click += (_, _) => select(value);
            palette.Children.Add(choice);
        }
        return palette;
    }

    private FrameworkElement LabeledSlider(
        string label,
        double minimum,
        double maximum,
        double current,
        Action<double> changed,
        string suffix)
    {
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
        grid.Children.Add(new TextBlock
        {
            Text = label,
            Foreground = ResourceBrush("CaptureToolbarTextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        });
        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = current,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Margin = new Thickness(5, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(slider, 1);
        grid.Children.Add(slider);
        var value = new TextBlock
        {
            Text = $"{current:0}{suffix}",
            Foreground = ResourceBrush("CaptureToolbarSecondaryTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(value, 2);
        grid.Children.Add(value);
        slider.ValueChanged += (_, _) =>
        {
            value.Text = $"{slider.Value:0}{suffix}";
            changed(slider.Value);
        };
        AutomationProperties.SetName(slider, label);
        return grid;
    }

    private void ShowContextWindow(FrameworkElement content, double width)
    {
        HideContextWindow(reactivateOverlay: false);
        var scaleY = Math.Max(1, _activeMonitor.DpiScaleY);
        var maximumHeightDips = Math.Max(
            180,
            (_activeMonitor.WorkAreaPixels.Height / scaleY) - 24);
        var scroller = new ScrollViewer
        {
            Content = content,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = maximumHeightDips
        };
        var border = new Border
        {
            Background = ResourceBrush("CaptureToolbarElevatedBrush"),
            BorderBrush = ResourceBrush("CaptureToolbarBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = scroller,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 3,
                Opacity = .3
            }
        };
        _contextWindow = new Window
        {
            Title = "Opções da ferramenta",
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            Owner = _toolbarWindow,
            Width = width,
            MaxHeight = maximumHeightDips,
            Opacity = SystemParameters.ClientAreaAnimation ? 0 : 1,
            Content = border
        };
        _contextWindow.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                HideContextWindow();
                args.Handled = true;
            }
        };
        _contextWindow.Deactivated += (_, _) => HideContextWindow();
        _contextWindow.Show();
        _contextWindow.UpdateLayout();
        PositionContextWindow();
        var animationDuration = CaptureMotion.Duration(
            SystemParameters.ClientAreaAnimation,
            160);
        if (animationDuration > TimeSpan.Zero)
        {
            _contextWindow.BeginAnimation(OpacityProperty, new DoubleAnimation(
                0,
                1,
                animationDuration));
        }
    }

    private void PositionContextWindow()
    {
        if (_contextWindow is null) return;
        var scaleX = Math.Max(1, _activeMonitor.DpiScaleX);
        var scaleY = Math.Max(1, _activeMonitor.DpiScaleY);
        var size = new Size(
            Math.Ceiling(_contextWindow.ActualWidth * scaleX),
            Math.Ceiling(_contextWindow.ActualHeight * scaleY));
        var placement = ToolbarPlacementCalculator.Calculate(
            _toolbarBoundsPixels,
            _activeMonitor.WorkAreaPixels,
            size,
            size.Width,
            gap: 8,
            dpiScale: Math.Max(scaleX, scaleY));
        var handle = new WindowInteropHelper(_contextWindow).Handle;
        _ = SetWindowPos(
            handle,
            new nint(-1),
            (int)Math.Round(placement.Bounds.Left),
            (int)Math.Round(placement.Bounds.Top),
            Math.Max(1, (int)Math.Ceiling(placement.Bounds.Width)),
            Math.Max(1, (int)Math.Ceiling(placement.Bounds.Height)),
            SwpNoActivate | SwpShowWindow);
    }

    private void HideContextWindow(bool reactivateOverlay = true)
    {
        if (_contextWindow is null) return;
        var window = _contextWindow;
        _contextWindow = null;
        window.Close();
        if (reactivateOverlay && IsVisible) Activate();
    }

    private static SolidColorBrush WpfBrush(int argb, float opacity)
    {
        var color = DrawingColor.FromArgb(argb);
        return new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)),
            color.R,
            color.G,
            color.B));
    }

    private static readonly int[] AnnotationColors =
    [
        DrawingColor.Black.ToArgb(), DrawingColor.White.ToArgb(), DrawingColor.Gray.ToArgb(),
        DrawingColor.Red.ToArgb(), DrawingColor.OrangeRed.ToArgb(), DrawingColor.Gold.ToArgb(),
        DrawingColor.LimeGreen.ToArgb(), DrawingColor.DeepSkyBlue.ToArgb(), DrawingColor.RoyalBlue.ToArgb(),
        DrawingColor.BlueViolet.ToArgb(), DrawingColor.DeepPink.ToArgb(), DrawingColor.Pink.ToArgb(),
        DrawingColor.LightGray.ToArgb(), DrawingColor.DarkGray.ToArgb(), DrawingColor.LightGreen.ToArgb(),
        DrawingColor.LightSkyBlue.ToArgb(), DrawingColor.Plum.ToArgb(), DrawingColor.Bisque.ToArgb()
    ];

    private static readonly int[] HighlightColors =
    [
        DrawingColor.Gold.ToArgb(), DrawingColor.LimeGreen.ToArgb(), DrawingColor.DeepSkyBlue.ToArgb(),
        DrawingColor.DeepPink.ToArgb(), DrawingColor.Orange.ToArgb(), DrawingColor.BlueViolet.ToArgb()
    ];

    private static readonly string[] StampValues =
    [
        "❤️", "⭐", "❓", "✅", "❌", "🔥",
        "👍", "👎", "👏", "🙌", "👀", "💯",
        "🙂", "😟", "😮", "😍", "😂", "😭"
    ];

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        HideContextWindow(reactivateOverlay: false);
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
                OutlineArgb = _outlineArgb ?? _color,
                Thickness = _thickness,
                Opacity = _opacity,
                Size = _annotationSize
            });
            return;
        }
        if (_tool == CaptureAnnotationKind.Stamp)
        {
            var half = _annotationSize / 2d;
            var contained = new Point(
                Math.Clamp(local.X, half, Math.Max(half, _localSelection.Width - half)),
                Math.Clamp(local.Y, half, Math.Max(half, _localSelection.Height - half)));
            Add(new CaptureAnnotation
            {
                Kind = CaptureAnnotationKind.Stamp,
                Start = contained,
                End = contained,
                Text = _selectedStamp,
                Size = _annotationSize
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
            OutlineArgb = _outlineArgb ?? _color,
            FillArgb = _fillArgb,
            Thickness = _thickness,
            Opacity = _tool == CaptureAnnotationKind.Highlighter
                ? Math.Min(_opacity, .38f)
                : _opacity,
            Size = _annotationSize
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
            OutlineArgb = _outlineArgb ?? _color,
            FillArgb = _fillArgb,
            Thickness = _thickness,
            Opacity = _tool == CaptureAnnotationKind.Highlighter
                ? Math.Min(_opacity, .38f)
                : _opacity,
            Size = _annotationSize
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
                OutlineArgb = _outlineArgb ?? _color,
                Thickness = _thickness,
                Opacity = _opacity,
                Size = _annotationSize,
                Bold = _textBold
            });
        }
    }

    private void Add(CaptureAnnotation annotation)
    {
        _annotationHistory.Add(annotation);
        Rebuild();
        UpdateHistoryButtons();
    }

    private void Undo()
    {
        if (_annotationHistory.Undo()) Rebuild();
        UpdateHistoryButtons();
    }

    private void Redo()
    {
        if (_annotationHistory.Redo()) Rebuild();
        UpdateHistoryButtons();
    }

    private void ClearAllAnnotations()
    {
        if (_annotationHistory.ClearAll()) Rebuild();
        UpdateHistoryButtons();
    }

    private void Rebuild(CaptureAnnotation? pending = null)
    {
        _annotationLayer.Children.Clear();
        foreach (var annotation in _annotationHistory.Items)
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
        var color = DrawingColor.FromArgb(annotation.OutlineArgb ?? annotation.Argb);
        var brush = new SolidColorBrush(
            Color.FromArgb(color.A, color.R, color.G, color.B));
        brush.Opacity = Math.Clamp(annotation.Opacity, 0, 1);
        var thickness = annotation.Kind == CaptureAnnotationKind.Highlighter
            ? annotation.Thickness * 4
            : annotation.Thickness;
        if (annotation.Kind == CaptureAnnotationKind.Highlighter)
        {
            brush.Opacity = annotation.Opacity >= .99 ? .38 : annotation.Opacity;
        }

        switch (annotation.Kind)
        {
            case CaptureAnnotationKind.Arrow:
            case CaptureAnnotationKind.Highlighter:
            case CaptureAnnotationKind.Line:
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
                shape.Stroke = annotation.OutlineArgb.HasValue || !annotation.FillArgb.HasValue
                    ? brush
                    : Brushes.Transparent;
                if (annotation.FillArgb is int fillArgb)
                {
                    var fillColor = DrawingColor.FromArgb(fillArgb);
                    shape.Fill = new SolidColorBrush(Color.FromArgb(
                        (byte)Math.Round(fillColor.A * Math.Clamp(annotation.Opacity, 0, 1)),
                        fillColor.R,
                        fillColor.G,
                        fillColor.B));
                }
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
                    FontSize = annotation.Size,
                    FontWeight = annotation.Bold ? FontWeights.Bold : FontWeights.Normal,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(text, annotation.Start.X);
                Canvas.SetTop(text, annotation.Start.Y);
                _annotationLayer.Children.Add(text);
                break;
            case CaptureAnnotationKind.Number:
                var badge = new Border
                {
                    Width = annotation.Size,
                    Height = annotation.Size,
                    CornerRadius = new CornerRadius(annotation.Size / 2),
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
                Canvas.SetLeft(badge, annotation.Start.X - annotation.Size / 2);
                Canvas.SetTop(badge, annotation.Start.Y - annotation.Size / 2);
                _annotationLayer.Children.Add(badge);
                break;
            case CaptureAnnotationKind.Stamp:
                var stamp = new TextBlock
                {
                    Text = annotation.Text,
                    FontFamily = new FontFamily("Segoe UI Emoji"),
                    FontSize = annotation.Size,
                    IsHitTestVisible = false
                };
                stamp.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(stamp, annotation.Start.X - stamp.DesiredSize.Width / 2);
                Canvas.SetTop(stamp, annotation.Start.Y - stamp.DesiredSize.Height / 2);
                _annotationLayer.Children.Add(stamp);
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
            var representsShape = tool == CaptureAnnotationKind.Rectangle &&
                _tool is CaptureAnnotationKind.Rectangle or CaptureAnnotationKind.Ellipse or
                    CaptureAnnotationKind.Line or CaptureAnnotationKind.Arrow or CaptureAnnotationKind.Stamp;
            button.IsChecked = tool == _tool || representsShape;
            if (button.Content is Path icon)
            {
                icon.Stroke = button.IsChecked == true
                    ? ResourceBrush("CaptureToolbarAccentBrush")
                    : ResourceBrush("CaptureToolbarTextBrush");
            }
        }
        if (_compactToolbar)
        {
            ApplyToolbarDensity(compact: true);
            RequestToolbarPosition();
        }
        UpdateHistoryButtons();
    }

    private void ApplyToolbarDensity(bool compact)
    {
        _compactToolbar = compact;
        foreach (var (tool, button) in _toolButtons)
        {
            button.Visibility = !compact || RepresentsActiveTool(tool)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        _eraseButton.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _reselectButton.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _captureSeparator.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _actionSeparator.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _overflowButton.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
    }

    private bool RepresentsActiveTool(CaptureAnnotationKind toolbarTool) =>
        toolbarTool == _tool ||
        toolbarTool == CaptureAnnotationKind.Rectangle &&
        _tool is CaptureAnnotationKind.Rectangle or CaptureAnnotationKind.Ellipse or
            CaptureAnnotationKind.Line or CaptureAnnotationKind.Stamp;

    private void UpdateHistoryButtons()
    {
        if (_undoButton is null || _redoButton is null || _eraseButton is null) return;
        _undoButton.IsEnabled = _annotationHistory.CanUndo;
        _redoButton.IsEnabled = _annotationHistory.CanRedo;
        _eraseButton.IsEnabled = _annotationHistory.Items.Count > 0;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_contextWindow is not null)
            {
                HideContextWindow();
                e.Handled = true;
                return;
            }
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter &&
                 _selectionReady &&
                 e.OriginalSource is not TextBox)
        {
            Complete(CaptureEditorOutput.Default);
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

    private void Complete(CaptureEditorOutput requestedOutput)
    {
        if (!_selectionReady)
        {
            return;
        }

        _toolbarWindow.Hide();
        HideContextWindow();
        RequestedOutput = requestedOutput;
        using var crop = CropFrozenSelection();
        EditedBitmap?.Dispose();
        EditedBitmap = CaptureAnnotationRenderer.Render(
            crop,
            _annotationHistory.Items,
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
        _annotationHistory.Reset();
        HideContextWindow();
        UpdateHistoryButtons();
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
        _activeMonitor = monitor;
        var marginPixels = Math.Max(1, 12 * monitor.DpiScaleX);
        var maximumWidthPixels = Math.Max(1, monitor.WorkAreaPixels.Width - (marginPixels * 2));
        var maximumWidthDips = maximumWidthPixels / monitor.DpiScaleX;

        // A faixa normal mede cerca de 640 DIPs. Em áreas menores, comandos
        // secundários migram para um overflow explícito; nunca são cortados.
        ApplyToolbarDensity(
            compact: CaptureToolbarLayoutPolicy.ShouldUseCompactMode(maximumWidthDips));

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
        _toolbarBoundsPixels = placement.Bounds;

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
