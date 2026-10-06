using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using SlashText.Services;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingColor = System.Drawing.Color;

namespace SlashText.Views;

/// <summary>One in-page image session in normal and expanded layouts.</summary>
public sealed class CaptureWorkbenchEditor : UserControl, IDisposable
{
    private readonly Image _image = new() { Stretch = Stretch.Fill };
    private readonly Canvas _overlay = new() { Background = Brushes.Transparent };
    private readonly Grid _surface = new() { ClipToBounds = true };
    private readonly Viewbox _viewbox = new() { Stretch = Stretch.Uniform };
    private readonly ScrollViewer _viewport = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        CanContentScroll = false
    };
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(24) };
    private readonly List<Point> _pencilPoints = [];
    private CaptureEditorDocument? _document;
    private DrawingBitmap? _previewBitmap;
    private CaptureAnnotation? _pending;
    private CaptureAnnotationKind? _tool;
    private Point _start;
    private Point? _panStart;
    private double _panLeft, _panTop, _zoom = 1d;
    private bool _drawing, _cropTool;
    private Rect? _crop;
    private int _color = DrawingColor.FromArgb(232, 78, 96).ToArgb();
    private float _thickness = 4;

    public event EventHandler? StateChanged;
    public event EventHandler? SaveCopyRequested;
    public event EventHandler? ExitRequested;
    public bool HasImage => _document is not null;
    public bool CanUndo => _document?.CanUndo == true;
    public bool CanRedo => _document?.CanRedo == true;
    public bool HasUnsavedChanges => _document?.HasUnsavedChanges == true;
    public bool HasPendingCrop => _crop.HasValue;
    public bool IsCropTool => _cropTool;
    public CaptureAnnotationKind? SelectedTool => _tool;
    public CaptureEditorDocument? Document => _document;
    public double Zoom => _zoom;
    public string AnnotationText { get; set; } = "Anotação";
    public string TextFontFamily { get; set; } = "Segoe UI";
    public float TextSize { get; set; } = 24;
    public bool TextBold { get; set; }
    public bool TextItalic { get; set; }
    public string TextAlignment { get; set; } = "Left";
    public string SelectedStamp { get; set; } = "👍";
    public float StampSize { get; set; } = 48;
    public int PrivacyStrength { get; set; } = 16;
    public int? ShapeFill { get; set; }
    public bool ShapeOutline { get; set; } = true;
    public float AnnotationOpacity { get; set; } = 1;

    public CaptureWorkbenchEditor()
    {
        Focusable = true;
        _surface.Children.Add(_image);
        _surface.Children.Add(_overlay);
        _overlay.MouseLeftButtonDown += PointerDown;
        _overlay.MouseMove += PointerMove;
        _overlay.MouseLeftButtonUp += PointerUp;
        _overlay.LostMouseCapture += (_, _) => { if (_drawing || _panStart.HasValue) CancelGesture(); };
        _viewbox.Child = _surface;
        _viewbox.HorizontalAlignment = HorizontalAlignment.Center;
        _viewbox.VerticalAlignment = VerticalAlignment.Center;
        _viewport.Content = _viewbox;
        _viewport.SizeChanged += (_, _) => UpdateZoom();
        Content = _viewport;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseWheel += (_, e) =>
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || !HasImage) return;
            SetZoom(Math.Clamp(_zoom + (e.Delta > 0 ? .25 : -.25), .25, 4));
            e.Handled = true;
        };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            if (_pending is not null && _previewBitmap is not null)
            {
                using var rendered = CaptureAnnotationRenderer.Render(
                    _previewBitmap, [_pending], _previewBitmap.Width, _previewBitmap.Height);
                _image.Source = ToBitmapSource(rendered);
            }
        };
    }

    public void SetZoom(double zoom)
    {
        if (!double.IsFinite(zoom) || zoom < .25 || zoom > 4)
            throw new ArgumentOutOfRangeException(nameof(zoom));
        _zoom = zoom;
        UpdateZoom();
        NotifyStateChanged();
    }

    private void UpdateZoom()
    {
        if (!HasImage || _viewport.ActualWidth <= 0 || _viewport.ActualHeight <= 0) return;
        var width = Math.Max(1d, _viewport.ActualWidth - SystemParameters.VerticalScrollBarWidth);
        var height = Math.Max(1d, _viewport.ActualHeight - SystemParameters.HorizontalScrollBarHeight);
        var fit = Math.Min(width / _surface.Width, height / _surface.Height);
        _viewbox.Width = _surface.Width * fit * _zoom;
        _viewbox.Height = _surface.Height * fit * _zoom;
    }

    public void LoadImage(string path)
    {
        using var source = new DrawingBitmap(path);
        // Never flatten animation into an editable still image.
        if (source.RawFormat.Guid == System.Drawing.Imaging.ImageFormat.Gif.Guid)
            throw new NotSupportedException("GIF é uma gravação; sua edição ficará para outra etapa.");
        LoadImage(source);
    }

    public void LoadImage(DrawingBitmap bitmap)
    {
        var next = new CaptureEditorDocument(bitmap);
        _document?.Dispose();
        _document = next;
        _tool = null;
        _cropTool = false;
        _crop = null;
        CancelGesture();
        RefreshPreview();
        _viewport.ScrollToHome();
        NotifyStateChanged();
    }

    public void Clear()
    {
        CancelGesture();
        _document?.Dispose(); _document = null;
        _previewBitmap?.Dispose(); _previewBitmap = null;
        _image.Source = null;
        _overlay.Children.Clear();
        _tool = null; _cropTool = false; _crop = null;
        NotifyStateChanged();
    }

    public void SelectTool(CaptureAnnotationKind? tool)
    {
        CancelGesture(); _crop = null; _cropTool = false; _tool = tool;
        _overlay.Cursor = tool switch { null => Cursors.Hand, CaptureAnnotationKind.Text => Cursors.IBeam, _ => Cursors.Cross };
        _overlay.Children.Clear(); NotifyStateChanged();
    }
    public void SelectCrop()
    {
        SelectTool(null); _cropTool = true; _overlay.Cursor = Cursors.Cross; NotifyStateChanged();
    }
    public void SetColor(int argb) { _color = argb; NotifyStateChanged(); }
    public void SetThickness(float thickness) => _thickness = Math.Clamp(thickness, 1, 24);
    public void Undo() { CancelCrop(); _document?.Undo(); RefreshPreview(); NotifyStateChanged(); }
    public void Redo() { CancelCrop(); _document?.Redo(); RefreshPreview(); NotifyStateChanged(); }
    public void MarkSaved() { _document?.MarkSaved(); NotifyStateChanged(); }
    public void DiscardChanges() { CancelCrop(); _document?.DiscardChanges(); RefreshPreview(); NotifyStateChanged(); }
    public DrawingBitmap Render()
    {
        if (_document is null) throw new InvalidOperationException("Nenhuma imagem carregada.");
        if (HasPendingCrop) throw new InvalidOperationException("Aplique ou cancele o recorte antes de copiar/salvar.");
        return _document.Render();
    }
    public bool ApplyCrop()
    {
        if (_document is null || !_crop.HasValue || !_document.Crop(_crop.Value)) return false;
        CancelCrop(); _cropTool = false; RefreshPreview(); NotifyStateChanged(); return true;
    }
    public void CancelCrop()
    {
        CancelGesture(); _crop = null; _overlay.Children.Clear(); NotifyStateChanged();
    }
    public void ResizeImage(int width, int height)
    {
        if (_document is null) return;
        CancelCrop(); _document.Resize(width, height); RefreshPreview(); NotifyStateChanged();
    }

    public void InsertAnnotation(CaptureAnnotation annotation)
    {
        if (_document is null) throw new InvalidOperationException("Nenhuma imagem carregada.");
        _document.AddAnnotation(annotation); RefreshPreview(); NotifyStateChanged();
    }
    public void InsertStamp(string value, Point center, float size = 48)
    {
        if (!NotoEmojiCatalog.TryGet(value, out _)) throw new ArgumentException("Emoji não disponível.", nameof(value));
        InsertAnnotation(new CaptureAnnotation { Kind = CaptureAnnotationKind.Stamp, Start = Clamp(center), End = Clamp(center), Text = value, Size = size });
    }

    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (_document is null || e.ChangedButton != MouseButton.Left) return;
        Focus(); e.Handled = true;
        _start = Clamp(e.GetPosition(_overlay));
        if (_cropTool) { _drawing = true; _crop = new Rect(_start, _start); _overlay.CaptureMouse(); return; }
        if (_tool is null)
        {
            _panStart = e.GetPosition(_viewport); _panLeft = _viewport.HorizontalOffset; _panTop = _viewport.VerticalOffset;
            _overlay.CaptureMouse(); return;
        }
        if (_tool == CaptureAnnotationKind.Text)
        {
            if (!string.IsNullOrWhiteSpace(AnnotationText)) InsertAnnotation(CreateAnnotation(_start));
            return;
        }
        if (_tool == CaptureAnnotationKind.Stamp) { InsertStamp(SelectedStamp, _start, StampSize); return; }
        if (_tool == CaptureAnnotationKind.Number) { InsertAnnotation(CreateAnnotation(_start)); return; }
        _drawing = true; _pencilPoints.Clear(); _pencilPoints.Add(_start); _overlay.CaptureMouse();
    }

    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (_panStart is Point origin)
        {
            var point = e.GetPosition(_viewport);
            _viewport.ScrollToHorizontalOffset(_panLeft - point.X + origin.X);
            _viewport.ScrollToVerticalOffset(_panTop - point.Y + origin.Y);
            return;
        }
        if (!_drawing) return;
        var end = Clamp(e.GetPosition(_overlay));
        if (_cropTool)
        {
            _crop = new Rect(_start, end); ShowCrop(); NotifyStateChanged(); return;
        }
        if (_tool == CaptureAnnotationKind.Pencil) _pencilPoints.Add(end);
        _pending = CreateAnnotation(end);
        if (!_previewTimer.IsEnabled) _previewTimer.Start();
    }

    private void PointerUp(object sender, MouseButtonEventArgs e)
    {
        if (_panStart.HasValue) { _panStart = null; _overlay.ReleaseMouseCapture(); return; }
        if (!_drawing) return;
        var end = Clamp(e.GetPosition(_overlay));
        _drawing = false; _overlay.ReleaseMouseCapture(); _previewTimer.Stop(); _pending = null;
        if (_cropTool) { _crop = new Rect(_start, end); ShowCrop(); NotifyStateChanged(); return; }
        if (_tool == CaptureAnnotationKind.Pencil) _pencilPoints.Add(end);
        if (_tool == CaptureAnnotationKind.Pencil || (end - _start).Length > 2)
            InsertAnnotation(CreateAnnotation(end));
        else RefreshPreview();
    }

    private CaptureAnnotation CreateAnnotation(Point end) => new()
    {
        Kind = _tool ?? CaptureAnnotationKind.Arrow, Start = _start, End = end, Points = [.. _pencilPoints],
        Argb = _color, OutlineArgb = ShapeOutline || _tool is not (CaptureAnnotationKind.Rectangle or CaptureAnnotationKind.Ellipse) ? _color : null,
        FillArgb = ShapeFill, Thickness = _thickness, Opacity = _tool == CaptureAnnotationKind.Highlighter ? .35f * AnnotationOpacity : AnnotationOpacity,
        Size = _tool == CaptureAnnotationKind.Number ? 32 : TextSize,
        Bold = TextBold, Italic = TextItalic, FontFamily = TextFontFamily, Alignment = TextAlignment,
        PrivacyStrength = PrivacyStrength, Text = _tool == CaptureAnnotationKind.Number ? (_document?.NextNumber ?? 1).ToString() : AnnotationText
    };

    private void CancelGesture()
    {
        _previewTimer.Stop(); _pending = null; _drawing = false; _panStart = null;
        _overlay.ReleaseMouseCapture();
        if (_previewBitmap is not null) _image.Source = ToBitmapSource(_previewBitmap);
    }

    private void RefreshPreview()
    {
        if (_document is null) return;
        var next = _document.Render();
        _previewBitmap?.Dispose(); _previewBitmap = next;
        _surface.Width = _image.Width = _overlay.Width = next.Width;
        _surface.Height = _image.Height = _overlay.Height = next.Height;
        _image.Source = ToBitmapSource(next);
        _overlay.Children.Clear(); UpdateZoom();
    }

    private void ShowCrop()
    {
        _overlay.Children.Clear();
        if (_crop is not Rect crop) return;
        var outline = new Rectangle { Width = crop.Width, Height = crop.Height, Fill = Brushes.Transparent, StrokeThickness = 2 / Math.Max(.05, _viewbox.Width / _surface.Width), StrokeDashArray = new DoubleCollection([4, 3]), IsHitTestVisible = false };
        outline.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Lab.accent");
        Canvas.SetLeft(outline, crop.Left); Canvas.SetTop(outline, crop.Top); _overlay.Children.Add(outline);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsVisible || !IsEnabled) return;
        if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        { if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) Redo(); else Undo(); e.Handled = true; }
        else if (e.Key == Key.Y && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Redo(); e.Handled = true; }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { SaveCopyRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
        else if (e.Key == Key.Escape) { if (HasPendingCrop) CancelCrop(); else ExitRequested?.Invoke(this, EventArgs.Empty); e.Handled = true; }
    }

    private Point Clamp(Point point) => new(Math.Clamp(point.X, 0, Math.Max(1, _surface.Width)), Math.Clamp(point.Y, 0, Math.Max(1, _surface.Height)));
    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
    public void Dispose() { _previewTimer.Stop(); _document?.Dispose(); _document = null; _previewBitmap?.Dispose(); _previewBitmap = null; }

    private static BitmapSource ToBitmapSource(DrawingBitmap bitmap)
    {
        using var stream = new System.IO.MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
        return image;
    }
}
