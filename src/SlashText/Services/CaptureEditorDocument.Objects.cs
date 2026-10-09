using System.Drawing;
using System.Windows;
using Point = System.Windows.Point;
using Size = System.Drawing.Size;
using FontStyle = System.Drawing.FontStyle;

namespace SlashText.Services;

public sealed record CaptureEditableAnnotation(long Id, CaptureAnnotation Annotation, Rect Bounds,
    double ScaleX, double ScaleY, double OffsetX, double OffsetY);

public sealed partial class CaptureEditorDocument
{
    // Objects retain their creation coordinate space. Geometry commands are never
    // flattened or reordered when an older object is edited.
    public IReadOnlyList<CaptureEditableAnnotation> EditableAnnotations()
    {
        var output = new List<CaptureEditableAnnotation>();
        var dimensions = _source.Size;
        var finalSize = Dimensions;
        var geometry = _steps.Select((step, index) => (step, index)).Where(x => x.step is CropStep or ResizeStep).ToArray();
        for (var i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            if (step is CropStep crop) dimensions = crop.Bounds.Size;
            else if (step is ResizeStep resize) dimensions = new Size(resize.Width, resize.Height);
            else if (step is AnnotationStep annotation)
            {
                double sx = 1, sy = 1, tx = 0, ty = 0;
                var size = dimensions;
                var bounds = AnnotationBounds(annotation.Annotation);
                foreach (var (later, index) in geometry)
                {
                    if (index <= i) continue;
                    if (later is CropStep c)
                    {
                        bounds.Intersect(new Rect(c.Bounds.X, c.Bounds.Y, c.Bounds.Width, c.Bounds.Height));
                        if (!bounds.IsEmpty) bounds.Offset(-c.Bounds.X, -c.Bounds.Y);
                        tx -= c.Bounds.X; ty -= c.Bounds.Y; size = c.Bounds.Size;
                    }
                    else if (later is ResizeStep r)
                    {
                        var dx = (double)r.Width / size.Width; var dy = (double)r.Height / size.Height;
                        if (!bounds.IsEmpty) bounds = new Rect(bounds.X * dx, bounds.Y * dy, bounds.Width * dx, bounds.Height * dy);
                        sx *= dx; sy *= dy; tx *= dx; ty *= dy; size = new Size(r.Width, r.Height);
                    }
                }
                bounds.Intersect(new Rect(0, 0, finalSize.Width, finalSize.Height));
                output.Add(new(annotation.ObjectId == 0 ? annotation.Id : annotation.ObjectId,
                    annotation.Annotation.Transform(tx / sx, ty / sy, sx, sy), bounds, sx, sy, tx, ty));
            }
        }
        return output;
    }

    public CaptureEditableAnnotation? HitTestAnnotation(Point point, double tolerance = 4) =>
        EditableAnnotations().Reverse().FirstOrDefault(a => !a.Bounds.IsEmpty && Contains(a.Bounds, point, tolerance));

    private static bool Contains(Rect bounds, Point point, double tolerance)
    { bounds.Inflate(tolerance, tolerance); return bounds.Contains(point); }

    public bool MoveAnnotation(long id, double dx, double dy)
    {
        var view = EditableAnnotations().FirstOrDefault(a => a.Id == id);
        var index = FindAnnotation(id);
        if (view is null || index < 0 || !double.IsFinite(dx) || !double.IsFinite(dy) ||
            Math.Abs(dx) + Math.Abs(dy) < .01) return false;
        var current = (AnnotationStep)_steps[index];
        Remember(); _steps[index] = new AnnotationStep(++_nextId,
            current.Annotation.Transform(dx / view.ScaleX, dy / view.ScaleY), id);
        return true;
    }

    public bool UpdateAnnotation(long id, CaptureAnnotation currentCoordinates)
    {
        var view = EditableAnnotations().FirstOrDefault(a => a.Id == id);
        var index = FindAnnotation(id);
        if (view is null || index < 0 || currentCoordinates.Kind != view.Annotation.Kind) return false;
        // Invert geometric coordinates; style sizes use the same average scale as
        // the public presentation rather than averaging inverse scales.
        var factor = (float)((view.ScaleX + view.ScaleY) / 2);
        var value = currentCoordinates.Transform(-view.OffsetX, -view.OffsetY, 1 / view.ScaleX, 1 / view.ScaleY) with
        { Size = currentCoordinates.Size / factor, Thickness = currentCoordinates.Thickness / factor };
        Remember(); _steps[index] = new AnnotationStep(++_nextId, value, id); return true;
    }

    public bool DeleteAnnotation(long id)
    {
        var index = FindAnnotation(id); if (index < 0) return false;
        Remember(); _steps.RemoveAt(index); return true;
    }

    private int FindAnnotation(long id) => _steps.FindIndex(step => step is AnnotationStep a && (a.ObjectId == 0 ? a.Id : a.ObjectId) == id);

    private static Rect AnnotationBounds(CaptureAnnotation a)
    {
        if (a.Kind == CaptureAnnotationKind.Text)
        {
            using var bitmap = new Bitmap(1, 1); using var graphics = Graphics.FromImage(bitmap);
            using var font = new Font(a.FontFamily, Math.Max(11, a.Size),
                (a.Bold ? FontStyle.Bold : FontStyle.Regular) | (a.Italic ? FontStyle.Italic : FontStyle.Regular), GraphicsUnit.Pixel);
            var size = graphics.MeasureString(a.Text, font);
            var x = a.Start.X - (a.Alignment == "Center" ? size.Width / 2 : a.Alignment == "Right" ? size.Width : 0);
            return new Rect(x, a.Start.Y, Math.Max(1, size.Width), Math.Max(1, size.Height));
        }
        if (a.Kind is CaptureAnnotationKind.Stamp or CaptureAnnotationKind.Number)
        {
            var size = a.Kind == CaptureAnnotationKind.Stamp ? Math.Max(12, a.Size) : Math.Max(28, Math.Max(a.Thickness * 8, a.Size));
            return new Rect(a.Start.X - size / 2, a.Start.Y - size / 2, size, size);
        }
        var points = a.Kind == CaptureAnnotationKind.Pencil && a.Points.Count > 0 ? a.Points : [a.Start, a.End];
        var left = points.Min(p => p.X); var top = points.Min(p => p.Y);
        var bounds = new Rect(left, top, Math.Max(1, points.Max(p => p.X) - left), Math.Max(1, points.Max(p => p.Y) - top));
        var padding = a.Kind == CaptureAnnotationKind.Arrow ? Math.Max(12, a.Thickness * 4) : Math.Max(3, a.Thickness / 2);
        bounds.Inflate(padding, padding); return bounds;
    }
}
