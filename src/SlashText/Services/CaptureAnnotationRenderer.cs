using System.Drawing;
using System.Drawing.Drawing2D;

namespace SlashText.Services;

public enum CaptureAnnotationKind
{
    Arrow,
    Highlighter,
    Rectangle,
    Ellipse,
    Pencil,
    Text,
    Number
}

public sealed class CaptureAnnotation
{
    public CaptureAnnotationKind Kind { get; init; }
    public System.Windows.Point Start { get; init; }
    public System.Windows.Point End { get; init; }
    public List<System.Windows.Point> Points { get; init; } = [];
    public int Argb { get; init; } = Color.Red.ToArgb();
    public float Thickness { get; init; } = 4;
    public string Text { get; init; } = string.Empty;
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

        var scaleX = source.Width / previewWidth;
        var scaleY = source.Height / previewHeight;
        using var graphics = Graphics.FromImage(output);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint =
            System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        foreach (var annotation in annotations)
        {
            Draw(graphics, annotation, scaleX, scaleY);
        }
        return output;
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
        var color = Color.FromArgb(annotation.Argb);
        if (annotation.Kind == CaptureAnnotationKind.Highlighter)
        {
            color = Color.FromArgb(90, color.R, color.G, color.B);
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
            case CaptureAnnotationKind.Rectangle:
                graphics.DrawRectangle(pen, Normalize(start, end));
                break;
            case CaptureAnnotationKind.Ellipse:
                graphics.DrawEllipse(pen, Normalize(start, end));
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
                           "Segoe UI",
                           Math.Max(11f, 17f * (float)scaleY),
                           FontStyle.Bold,
                           GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(color))
                {
                    graphics.DrawString(annotation.Text, font, brush, start);
                }
                break;
            case CaptureAnnotationKind.Number:
                DrawNumber(graphics, annotation.Text, color, start, thickness);
                break;
        }
    }

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
