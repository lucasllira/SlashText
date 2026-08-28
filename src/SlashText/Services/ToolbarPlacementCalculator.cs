using System.Windows;

namespace SlashText.Services;

public enum ToolbarPlacementSide
{
    Below,
    Above,
    Right,
    Left,
    Safe
}

public readonly record struct ToolbarPlacement(
    Rect Bounds,
    ToolbarPlacementSide Side,
    double MaximumWidth);

public static class ToolbarPlacementCalculator
{
    public static ToolbarPlacement Calculate(
        Rect selection,
        Rect workingArea,
        Size desiredSize,
        double margin = 12,
        double gap = 14)
    {
        var availableWidth = Math.Max(1, workingArea.Width - (margin * 2));
        var width = Math.Min(Math.Max(1, desiredSize.Width), availableWidth);
        var height = Math.Min(Math.Max(1, desiredSize.Height), Math.Max(1, workingArea.Height - (margin * 2)));
        var centeredLeft = Clamp(
            selection.Left + ((selection.Width - width) / 2),
            workingArea.Left + margin,
            workingArea.Right - margin - width);

        var belowTop = selection.Bottom + gap;
        if (belowTop + height <= workingArea.Bottom - margin)
        {
            return Result(centeredLeft, belowTop, width, height, ToolbarPlacementSide.Below, availableWidth);
        }

        var aboveTop = selection.Top - gap - height;
        if (aboveTop >= workingArea.Top + margin)
        {
            return Result(centeredLeft, aboveTop, width, height, ToolbarPlacementSide.Above, availableWidth);
        }

        var centeredTop = Clamp(
            selection.Top + ((selection.Height - height) / 2),
            workingArea.Top + margin,
            workingArea.Bottom - margin - height);
        var rightLeft = selection.Right + gap;
        if (rightLeft + width <= workingArea.Right - margin)
        {
            return Result(rightLeft, centeredTop, width, height, ToolbarPlacementSide.Right, availableWidth);
        }

        var leftLeft = selection.Left - gap - width;
        if (leftLeft >= workingArea.Left + margin)
        {
            return Result(leftLeft, centeredTop, width, height, ToolbarPlacementSide.Left, availableWidth);
        }

        return Result(
            Clamp(centeredLeft, workingArea.Left + margin, workingArea.Right - margin - width),
            Clamp(workingArea.Bottom - margin - height, workingArea.Top + margin, workingArea.Bottom - margin - height),
            width,
            height,
            ToolbarPlacementSide.Safe,
            availableWidth);
    }

    private static ToolbarPlacement Result(
        double left,
        double top,
        double width,
        double height,
        ToolbarPlacementSide side,
        double maximumWidth) =>
        new(new Rect(left, top, width, height), side, maximumWidth);

    private static double Clamp(double value, double minimum, double maximum) =>
        maximum <= minimum ? minimum : Math.Clamp(value, minimum, maximum);
}
