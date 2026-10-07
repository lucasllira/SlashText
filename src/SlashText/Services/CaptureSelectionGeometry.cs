using System.Windows;

namespace SlashText.Services;

/// <summary>Selection coordinates are overlay DIPs; the native placement/export boundary converts to physical pixels.</summary>
public static class CaptureSelectionGeometry
{
    public static Rect Move(Rect start, Vector delta, Rect bounds) => new(
        Math.Clamp(start.Left + delta.X, bounds.Left, Math.Max(bounds.Left, bounds.Right - start.Width)),
        Math.Clamp(start.Top + delta.Y, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - start.Height)),
        Math.Min(start.Width, bounds.Width), Math.Min(start.Height, bounds.Height));

    // NW, N, NE, W, E, SW, S, SE. Keep the opposite edge fixed, never invert a handle.
    public static Rect Resize(Rect start, int handle, Vector delta, Rect bounds, double minimum = 8)
    {
        if (handle is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(handle));
        var left = start.Left; var top = start.Top; var right = start.Right; var bottom = start.Bottom;
        if (handle is 0 or 3 or 5) left = Math.Clamp(left + delta.X, bounds.Left, right - Math.Min(minimum, start.Width));
        if (handle is 2 or 4 or 7) right = Math.Clamp(right + delta.X, left + Math.Min(minimum, start.Width), bounds.Right);
        if (handle is 0 or 1 or 2) top = Math.Clamp(top + delta.Y, bounds.Top, bottom - Math.Min(minimum, start.Height));
        if (handle is 5 or 6 or 7) bottom = Math.Clamp(bottom + delta.Y, top + Math.Min(minimum, start.Height), bounds.Bottom);
        return new Rect(new Point(left, top), new Point(right, bottom));
    }
}
