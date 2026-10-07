using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace SlashText.Services;

public enum CaptureAnnotationKind
{
    Arrow,
    Highlighter,
    Rectangle,
    Ellipse,
    Line,
    Pencil,
    Text,
    Number,
    Stamp,
    Blur,
    Pixelate
}

public sealed class CaptureAnnotation
{
    public CaptureAnnotationKind Kind { get; init; }
    public System.Windows.Point Start { get; init; }
    public System.Windows.Point End { get; init; }
    public List<System.Windows.Point> Points { get; init; } = [];
    public int Argb { get; init; } = Color.Red.ToArgb();
    public int? OutlineArgb { get; init; } = Color.Red.ToArgb();
    public int? FillArgb { get; init; }
    public float Thickness { get; init; } = 4;
    public float Opacity { get; init; } = 1;
    public float Size { get; init; } = 32;
    public bool Bold { get; init; } = true;
    public bool Italic { get; init; }
    public string FontFamily { get; init; } = "Segoe UI";
    public int PrivacyStrength { get; init; }
    public string Alignment { get; init; } = "Left";
    public string Text { get; init; } = string.Empty;

    public bool HasVisibleShapeStyle =>
        FillArgb.HasValue || OutlineArgb.HasValue ||
        Kind is not CaptureAnnotationKind.Rectangle and
            not CaptureAnnotationKind.Ellipse;
}

public static class CaptureAnnotationRenderer
{
    public static Bitmap Render(
        Bitmap source,
        IReadOnlyList<CaptureAnnotation> annotations,
        double previewWidth,
        double previewHeight)
    {
        var output = new Bitmap(source);
        if (previewWidth <= 0 || previewHeight <= 0)
        {
            return output;
        }

        try { Apply(output, annotations, previewWidth, previewHeight); return output; }
        catch { output.Dispose(); throw; }
    }

    // Only the document owns this bitmap. Applying an append in place avoids
    // copying the entire capture and decoding all previous stamps again.
    internal static void Apply(Bitmap output, IReadOnlyList<CaptureAnnotation> annotations,
        double previewWidth, double previewHeight)
    {
        if (previewWidth <= 0 || previewHeight <= 0) return;
        var scaleX = output.Width / previewWidth;
        var scaleY = output.Height / previewHeight;
        foreach (var annotation in annotations)
        {
            if (annotation.Kind is CaptureAnnotationKind.Blur or CaptureAnnotationKind.Pixelate)
            {
                ApplyPrivacyEffect(output, annotation, scaleX, scaleY);
                continue;
            }
            using var graphics = Graphics.FromImage(output);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            Draw(graphics, annotation, scaleX, scaleY);
        }
    }

    private static void ApplyPrivacyEffect(
        Bitmap output,
        CaptureAnnotation annotation,
        double scaleX,
        double scaleY)
    {
        var region = Rectangle.Round(Normalize(
            Scale(annotation.Start, scaleX, scaleY),
            Scale(annotation.End, scaleX, scaleY)));
        region.Intersect(new Rectangle(0, 0, output.Width, output.Height));
        if (region.Width < 2 || region.Height < 2)
        {
            return;
        }
        var divisor = annotation.PrivacyStrength > 0
            ? Math.Clamp(annotation.PrivacyStrength, 6, 40)
            : annotation.Kind == CaptureAnnotationKind.Pixelate ? 18 : 7;
        var smallWidth = Math.Max(1, region.Width / divisor);
        var smallHeight = Math.Max(1, region.Height / divisor);
        using var small = new Bitmap(smallWidth, smallHeight);
        using (var down = Graphics.FromImage(small))
        {
            down.InterpolationMode = InterpolationMode.HighQualityBilinear;
            down.DrawImage(
                output,
                new Rectangle(0, 0, small.Width, small.Height),
                region,
                GraphicsUnit.Pixel);
        }
        using var up = Graphics.FromImage(output);
        up.InterpolationMode = annotation.Kind == CaptureAnnotationKind.Pixelate
            ? InterpolationMode.NearestNeighbor
            : InterpolationMode.HighQualityBicubic;
        up.PixelOffsetMode = annotation.Kind == CaptureAnnotationKind.Pixelate
            ? PixelOffsetMode.Half
            : PixelOffsetMode.HighQuality;
        up.DrawImage(small, region);
    }

    private static void Draw(
        Graphics graphics,
        CaptureAnnotation annotation,
        double scaleX,
        double scaleY)
    {
        var start = Scale(annotation.Start, scaleX, scaleY);
        var end = Scale(annotation.End, scaleX, scaleY);
        var thickness = Math.Max(
            1f,
            annotation.Thickness * (float)((scaleX + scaleY) / 2d));
        var color = WithOpacity(
            Color.FromArgb(annotation.OutlineArgb ?? annotation.Argb),
            annotation.Kind == CaptureAnnotationKind.Highlighter && annotation.Opacity >= .99f
                ? .35f
                : annotation.Opacity);
        if (annotation.Kind == CaptureAnnotationKind.Highlighter)
        {
            thickness *= 4;
        }

        using var pen = new Pen(color, thickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        switch (annotation.Kind)
        {
            case CaptureAnnotationKind.Arrow:
                graphics.DrawLine(pen, start, end);
                DrawArrowHead(graphics, color, start, end, thickness);
                break;
            case CaptureAnnotationKind.Highlighter:
                graphics.DrawLine(pen, start, end);
                break;
            case CaptureAnnotationKind.Line:
                graphics.DrawLine(pen, start, end);
                break;
            case CaptureAnnotationKind.Rectangle:
                DrawShape(graphics, annotation, Normalize(start, end), ellipse: false, pen);
                break;
            case CaptureAnnotationKind.Ellipse:
                DrawShape(graphics, annotation, Normalize(start, end), ellipse: true, pen);
                break;
            case CaptureAnnotationKind.Pencil:
                var points = annotation.Points
                    .Select(point => Scale(point, scaleX, scaleY))
                    .ToArray();
                if (points.Length > 1)
                {
                    graphics.DrawLines(pen, points);
                }
                break;
            case CaptureAnnotationKind.Text:
                using (var font = new Font(
                           string.IsNullOrWhiteSpace(annotation.FontFamily) ? "Segoe UI" : annotation.FontFamily,
                           Math.Max(11f, annotation.Size * (float)scaleY),
                           (annotation.Bold ? FontStyle.Bold : FontStyle.Regular) | (annotation.Italic ? FontStyle.Italic : FontStyle.Regular),
                           GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(color))
                {
                    using var format = new StringFormat
                    {
                        Alignment = annotation.Alignment switch
                        {
                            "Center" => StringAlignment.Center,
                            "Right" => StringAlignment.Far,
                            _ => StringAlignment.Near
                        }
                    };
                    graphics.DrawString(annotation.Text, font, brush, start, format);
                }
                break;
            case CaptureAnnotationKind.Number:
                DrawNumber(graphics, annotation.Text, color, start,
                    Math.Max(thickness, annotation.Size * (float)((scaleX + scaleY) / 16d)));
                break;
            case CaptureAnnotationKind.Stamp:
                DrawStamp(graphics, annotation, start, scaleX, scaleY);
                break;
        }
    }

    private static void DrawShape(
        Graphics graphics,
        CaptureAnnotation annotation,
        RectangleF bounds,
        bool ellipse,
        Pen outline)
    {
        if (annotation.FillArgb is int fillArgb)
        {
            using var fill = new SolidBrush(WithOpacity(Color.FromArgb(fillArgb), annotation.Opacity));
            if (ellipse) graphics.FillEllipse(fill, bounds);
            else graphics.FillRectangle(fill, bounds);
        }
        if (annotation.OutlineArgb.HasValue)
        {
            if (ellipse) graphics.DrawEllipse(outline, bounds);
            else graphics.DrawRectangle(outline, bounds);
        }
    }

    private static void DrawStamp(
        Graphics graphics,
        CaptureAnnotation annotation,
        PointF center,
        double scaleX,
        double scaleY)
    {
        var pixels = Math.Max(12, (int)Math.Ceiling(
            annotation.Size * ((scaleX + scaleY) / 2d)));
        using var stamp = NotoEmojiCatalog.CreateBitmap(annotation.Text);
        var fit = pixels / (float)Math.Max(stamp.Width, stamp.Height);
        var width = stamp.Width * fit;
        var height = stamp.Height * fit;
        var destination = new RectangleF(
            center.X - width / 2,
            center.Y - height / 2,
            width,
            height);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(stamp, destination);
    }

    private static Color WithOpacity(Color color, float opacity) =>
        Color.FromArgb(
            (int)Math.Round(color.A * Math.Clamp(opacity, 0, 1)),
            color.R,
            color.G,
            color.B);

    private static void DrawArrowHead(
        Graphics graphics,
        Color color,
        PointF start,
        PointF end,
        float thickness)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var length = Math.Max(12f, thickness * 4f);
        var left = new PointF(
            end.X - length * (float)Math.Cos(angle - Math.PI / 6),
            end.Y - length * (float)Math.Sin(angle - Math.PI / 6));
        var right = new PointF(
            end.X - length * (float)Math.Cos(angle + Math.PI / 6),
            end.Y - length * (float)Math.Sin(angle + Math.PI / 6));
        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, [end, left, right]);
    }

    private static void DrawNumber(
        Graphics graphics,
        string text,
        Color color,
        PointF center,
        float thickness)
    {
        var size = Math.Max(28f, thickness * 8f);
        var bounds = new RectangleF(
            center.X - size / 2,
            center.Y - size / 2,
            size,
            size);
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, bounds);
        using var font = new Font(
            "Segoe UI",
            size * .56f,
            FontStyle.Bold,
            GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(text, font, textBrush, bounds, format);
    }

    private static PointF Scale(
        System.Windows.Point point,
        double scaleX,
        double scaleY) =>
        new((float)(point.X * scaleX), (float)(point.Y * scaleY));

    private static RectangleF Normalize(PointF start, PointF end) =>
        new(
            Math.Min(start.X, end.X),
            Math.Min(start.Y, end.Y),
            Math.Abs(end.X - start.X),
            Math.Abs(end.Y - start.Y));
}
