using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SlashText.Services;

namespace SlashText.Views;

public sealed partial class RegionCaptureWindow
{
    private int _selectedObjectIndex = -1;
    private Point? _objectDragStart;
    private Vector _objectDragDelta;
    private readonly Rectangle _objectOutline = new()
    {
        StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 2 },
        IsHitTestVisible = false, Fill = Brushes.Transparent
    };

    internal CaptureRegionSession? CompletedSession { get; private set; }

    private CaptureRegionSession CreateRegionSession()
    {
        using var crop = CropFrozenSelection();
        var sx = crop.Width / _localSelection.Width; var sy = crop.Height / _localSelection.Height;
        return new CaptureRegionSession(crop, _annotationHistory.Items.Select(a => a.Transform(
            _annotationOrigin.X - _localSelection.X, _annotationOrigin.Y - _localSelection.Y, sx, sy)));
    }

    // Both the canvas and annotation-layer handlers use this route. Text input
    // retains its caret; only committed annotations can be moved.
    private bool TryBeginObjectMove(Point canvasPoint, bool bypass, bool captureMouse = true)
    {
        if (!_pilotVisuals || !_selectionReady || bypass || !_localSelection.Contains(canvasPoint)) return false;
        var point = ToAnnotationPoint(canvasPoint);
        var index = _annotationHistory.HitTest(point);
        if (index < 0) return false;
        var kind = _annotationHistory.Items[index].Kind;
        if (kind is not (CaptureAnnotationKind.Text or CaptureAnnotationKind.Stamp) ||
            !_selectMode && _tool != kind) return false;
        _selectedObjectIndex = index; _objectDragStart = point; _objectDragDelta = new Vector();
        _drawing = false; HideContextWindow(reactivateOverlay: false);
        if (captureMouse) { Focus(); CaptureMouse(); }
        _annotationLayer.Cursor = Cursors.SizeAll;
        ShowObjectSelection(); return true;
    }

    private void UpdateObjectMove(Point canvasPoint)
    {
        if (_objectDragStart is not Point start) return;
        _objectDragDelta = ToAnnotationPoint(canvasPoint) - start;
        // Only the outline changes while dragging; expensive effects are
        // composed once on release, and the entire gesture is one undo step.
        ShowObjectSelection();
    }

    private void FinishObjectMove(bool commit)
    {
        var delta = _objectDragDelta;
        _objectDragStart = null; _objectDragDelta = new Vector();
        if (commit) _annotationHistory.Move(_selectedObjectIndex, delta.X, delta.Y);
        ReleaseMouseCapture(); Rebuild(); UpdateHistoryButtons();
        _annotationLayer.Cursor = _selectMode ? Cursors.SizeAll : _tool == CaptureAnnotationKind.Text ? Cursors.IBeam : Cursors.Cross;
    }

    private void ClearObjectSelection()
    {
        var wasDragging = _objectDragStart.HasValue;
        _objectDragStart = null; _objectDragDelta = new Vector(); _selectedObjectIndex = -1;
        if (wasDragging && IsMouseCaptured) ReleaseMouseCapture();
        _annotationLayer.Children.Remove(_objectOutline);
    }

    private void ShowObjectSelection()
    {
        _annotationLayer.Children.Remove(_objectOutline);
        if (_selectedObjectIndex < 0 || _selectedObjectIndex >= _annotationHistory.Items.Count) return;
        var bounds = CaptureEditorDocument.AnnotationBounds(_annotationHistory.Items[_selectedObjectIndex]);
        bounds.Offset(_annotationOrigin.X - _localSelection.X + _objectDragDelta.X,
            _annotationOrigin.Y - _localSelection.Y + _objectDragDelta.Y);
        bounds.Intersect(new Rect(0, 0, _localSelection.Width, _localSelection.Height));
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return;
        _objectOutline.SetResourceReference(Shape.StrokeProperty, "Lab.accent");
        _objectOutline.Width = bounds.Width; _objectOutline.Height = bounds.Height;
        System.Windows.Controls.Canvas.SetLeft(_objectOutline, bounds.Left);
        System.Windows.Controls.Canvas.SetTop(_objectOutline, bounds.Top);
        _annotationLayer.Children.Add(_objectOutline);
    }

    private bool HandleObjectMoveKey(KeyEventArgs e)
    {
        if (_selectedObjectIndex < 0) return false;
        if (e.Key == Key.Escape)
        {
            if (_objectDragStart.HasValue) FinishObjectMove(commit: false);
            else ClearObjectSelection();
            e.Handled = true; return true;
        }
        if (_objectDragStart.HasValue || Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ||
            e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return false;
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        // Convert one output pixel to the overlay's logical coordinate space.
        var dx = (e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0) * Width / _desktopBitmap.Width;
        var dy = (e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0) * Height / _desktopBitmap.Height;
        if (_annotationHistory.Move(_selectedObjectIndex, dx, dy)) { Rebuild(); UpdateHistoryButtons(); }
        e.Handled = true; return true;
    }

    internal bool BeginObjectMoveForEvidence(Point point, CaptureAnnotationKind? tool = null, bool bypass = false)
    {
        _selectMode = tool is null; if (tool.HasValue) _tool = tool.Value;
        return TryBeginObjectMove(point, bypass, captureMouse: false);
    }
    internal void EndObjectMoveForEvidence(Point point, bool commit = true)
    { UpdateObjectMove(point); FinishObjectMove(commit); }
    internal IReadOnlyList<CaptureAnnotation> AnnotationsForEvidence => _annotationHistory.Items;
    internal void UndoObjectMoveForEvidence() => Undo();
    internal void RedoObjectMoveForEvidence() => Redo();
    internal FrameworkElement SelectionSurfaceForEvidence => _canvas;
    internal CaptureRegionSession SessionForEvidence() => CreateRegionSession();
}
