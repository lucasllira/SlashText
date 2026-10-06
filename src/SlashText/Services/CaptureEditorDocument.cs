using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using WpfRect = System.Windows.Rect;

namespace SlashText.Services;

/// <summary>
/// One non-destructive image session. Commands use current image pixels, never
/// screen/DIP coordinates. Layout, zoom and saving must not recreate this object.
/// </summary>
public sealed class CaptureEditorDocument : IDisposable
{
    private abstract record Step(long Id);
    private sealed record AnnotationStep(long Id, CaptureAnnotation Annotation) : Step(Id);
    private sealed record CropStep(long Id, Rectangle Bounds) : Step(Id);
    private sealed record ResizeStep(long Id, int Width, int Height) : Step(Id);
    private readonly Bitmap _source;
    private readonly List<Step> _steps = [];
    private readonly Stack<Step> _redo = new();
    private Step[] _checkpoint = [];
    private long _nextId;

    public CaptureEditorDocument(Bitmap source) => _source = new Bitmap(source);
    public int OperationCount => _steps.Count;
    public bool CanUndo => _steps.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool HasUnsavedChanges => !_steps.Select(s => s.Id).SequenceEqual(_checkpoint.Select(s => s.Id));
    public int NextNumber => _steps.OfType<AnnotationStep>().Count(s => s.Annotation.Kind == CaptureAnnotationKind.Number) + 1;
    public Size Dimensions
    {
        get
        {
            var size = _source.Size;
            foreach (var step in _steps)
                size = step switch
                {
                    CropStep crop => crop.Bounds.Size,
                    ResizeStep resize => new Size(resize.Width, resize.Height),
                    _ => size
                };
            return size;
        }
    }

    public void AddAnnotation(CaptureAnnotation value)
    {
        // Snapshot caller-owned mutable point lists; neither UI nor redo may edit a command.
        Add(new AnnotationStep(++_nextId, new CaptureAnnotation
        {
            Kind = value.Kind, Start = value.Start, End = value.End, Points = [.. value.Points],
            Argb = value.Argb, OutlineArgb = value.OutlineArgb, FillArgb = value.FillArgb,
            Thickness = value.Thickness, Opacity = value.Opacity, Size = value.Size,
            Bold = value.Bold, Italic = value.Italic, Alignment = value.Alignment,
            FontFamily = value.FontFamily, Text = value.Text, PrivacyStrength = value.PrivacyStrength
        }));
    }

    public bool Crop(WpfRect selection)
    {
        var size = Dimensions;
        var left = Math.Clamp((int)Math.Floor(selection.Left), 0, size.Width);
        var top = Math.Clamp((int)Math.Floor(selection.Top), 0, size.Height);
        var right = Math.Clamp((int)Math.Ceiling(selection.Right), left, size.Width);
        var bottom = Math.Clamp((int)Math.Ceiling(selection.Bottom), top, size.Height);
        if (right - left < 8 || bottom - top < 8) return false;
        Add(new CropStep(++_nextId, Rectangle.FromLTRB(left, top, right, bottom)));
        return true;
    }

    public static bool ValidSize(int width, int height) =>
        width >= 8 && height >= 8 && width <= 32000 && height <= 32000 && (long)width * height <= 64000000;

    public void Resize(int width, int height)
    {
        if (!ValidSize(width, height)) throw new ArgumentOutOfRangeException(nameof(width), "Use 8–32.000 px por lado, até 64 megapixels.");
        Add(new ResizeStep(++_nextId, width, height));
    }

    private void Add(Step step) { _steps.Add(step); _redo.Clear(); }
    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Push(_steps[^1]); _steps.RemoveAt(_steps.Count - 1);
    }
    public void Redo() { if (CanRedo) _steps.Add(_redo.Pop()); }
    public void MarkSaved() => _checkpoint = [.. _steps];
    public void DiscardChanges() { _steps.Clear(); _steps.AddRange(_checkpoint); _redo.Clear(); }

    /// <summary>Preview, clipboard and export all call this same renderer.</summary>
    public Bitmap Render(CaptureAnnotation? pending = null)
    {
        Bitmap bitmap = new(_source);
        var annotations = new List<CaptureAnnotation>();
        void Flush()
        {
            if (annotations.Count == 0) return;
            var next = CaptureAnnotationRenderer.Render(bitmap, annotations, bitmap.Width, bitmap.Height);
            bitmap.Dispose(); bitmap = next; annotations.Clear();
        }
        try
        {
            foreach (var step in _steps)
            {
                if (step is AnnotationStep annotation) { annotations.Add(annotation.Annotation); continue; }
                Flush();
                Bitmap next;
                if (step is CropStep crop) next = bitmap.Clone(crop.Bounds, PixelFormat.Format32bppArgb);
                else if (step is ResizeStep resize)
                {
                    next = new Bitmap(resize.Width, resize.Height, PixelFormat.Format32bppArgb);
                    using var graphics = Graphics.FromImage(next);
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    using var attributes = new ImageAttributes();
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(bitmap, new Rectangle(0, 0, next.Width, next.Height),
                        0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, attributes);
                }
                else throw new InvalidOperationException("Comando de edição desconhecido.");
                bitmap.Dispose(); bitmap = next;
            }
            if (pending is not null) annotations.Add(pending);
            Flush();
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    public void Dispose() => _source.Dispose();
}
