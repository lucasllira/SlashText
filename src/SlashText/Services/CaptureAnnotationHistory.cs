namespace SlashText.Services;

public sealed class CaptureAnnotationHistory
{
    private readonly List<CaptureAnnotation> _items = [];
    private readonly Stack<List<CaptureAnnotation>> _undo = new();
    private readonly Stack<List<CaptureAnnotation>> _redo = new();

    public IReadOnlyList<CaptureAnnotation> Items => _items;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public void Add(CaptureAnnotation annotation)
    {
        SaveUndoState();
        _items.Add(annotation);
    }

    public int HitTest(System.Windows.Point point, double tolerance = 4)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var bounds = CaptureEditorDocument.AnnotationBounds(_items[i]);
            bounds.Inflate(tolerance, tolerance);
            if (bounds.Contains(point)) return i;
        }
        return -1;
    }

    public bool Move(int index, double dx, double dy)
    {
        if (index < 0 || index >= _items.Count || !double.IsFinite(dx) || !double.IsFinite(dy) ||
            Math.Abs(dx) + Math.Abs(dy) < .01) return false;
        SaveUndoState();
        _items[index] = _items[index].Transform(dx, dy);
        return true;
    }

    public bool ClearAll()
    {
        if (_items.Count == 0) return false;
        SaveUndoState();
        _items.Clear();
        return true;
    }

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        _redo.Push([.. _items]);
        ReplaceWith(_undo.Pop());
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        _undo.Push([.. _items]);
        ReplaceWith(_redo.Pop());
        return true;
    }

    public void Reset()
    {
        _items.Clear();
        _undo.Clear();
        _redo.Clear();
    }

    private void SaveUndoState()
    {
        _undo.Push([.. _items]);
        _redo.Clear();
    }

    private void ReplaceWith(IEnumerable<CaptureAnnotation> annotations)
    {
        _items.Clear();
        _items.AddRange(annotations);
    }
}
