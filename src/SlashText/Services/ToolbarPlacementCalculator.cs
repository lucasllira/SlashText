using System.Windows;

namespace SlashText.Services;

public enum ToolbarPlacementSide
{
    Below,
    Above,
    InsideBottom,
    InsideTop,
    Safe
}

public enum ToolbarLayoutMode
{
    Normal,
    Compact
}

public readonly record struct ToolbarPlacement(
    Rect Bounds,
    ToolbarPlacementSide Side,
    ToolbarLayoutMode Mode,
    int ExpectedRows,
    double MaximumWidth);

public enum AnchoredPopoverSide
{
    Below,
    Above,
    Safe
}

public readonly record struct AnchoredPopoverPlacement(
    Rect Bounds,
    AnchoredPopoverSide Side);

public static class AnchoredPopoverPlacementCalculator
{
    public static AnchoredPopoverPlacement Calculate(
        Rect anchor,
        Rect workingArea,
        Size finalSize,
        double margin = 12,
        double gap = 8,
        double dpiScale = 1)
    {
        var scaledMargin = Math.Max(1, margin * Math.Max(1, dpiScale));
        var scaledGap = Math.Max(1, gap * Math.Max(1, dpiScale));
        var width = Math.Min(
            Math.Max(1, finalSize.Width),
            Math.Max(1, workingArea.Width - (scaledMargin * 2)));
        var height = Math.Min(
            Math.Max(1, finalSize.Height),
            Math.Max(1, workingArea.Height - (scaledMargin * 2)));
        var left = Clamp(
            anchor.Left,
            workingArea.Left + scaledMargin,
            workingArea.Right - scaledMargin - width);

        var belowTop = anchor.Bottom + scaledGap;
        if (belowTop + height <= workingArea.Bottom - scaledMargin)
        {
            return Result(left, belowTop, width, height, AnchoredPopoverSide.Below);
        }

        var aboveTop = anchor.Top - scaledGap - height;
        if (aboveTop >= workingArea.Top + scaledMargin)
        {
            return Result(left, aboveTop, width, height, AnchoredPopoverSide.Above);
        }

        return Result(
            left,
            Clamp(
                belowTop,
                workingArea.Top + scaledMargin,
                workingArea.Bottom - scaledMargin - height),
            width,
            height,
            AnchoredPopoverSide.Safe);
    }

    private static AnchoredPopoverPlacement Result(
        double left,
        double top,
        double width,
        double height,
        AnchoredPopoverSide side) =>
        new(new Rect(left, top, width, height), side);

    private static double Clamp(double value, double minimum, double maximum) =>
        maximum <= minimum ? minimum : Math.Clamp(value, minimum, maximum);
}

public static class ToolbarPlacementCalculator
{
    public static ToolbarPlacement Calculate(
        Rect selection,
        Rect workingArea,
        Size finalSize,
        double naturalWidth = 0,
        double margin = 12,
        double gap = 14,
        double dpiScale = 1)
    {
        var scaledMargin = Math.Max(1, margin * Math.Max(1, dpiScale));
        var scaledGap = Math.Max(1, gap * Math.Max(1, dpiScale));
        var availableWidth = Math.Max(1, workingArea.Width - (scaledMargin * 2));
        var availableHeight = Math.Max(1, workingArea.Height - (scaledMargin * 2));
        var width = Math.Min(Math.Max(1, finalSize.Width), availableWidth);
        var height = Math.Min(Math.Max(1, finalSize.Height), availableHeight);
        var effectiveNaturalWidth = naturalWidth > 0 ? naturalWidth : finalSize.Width;
        var expectedRows = Math.Max(1, (int)Math.Ceiling(Math.Max(width, effectiveNaturalWidth) / availableWidth));
        var mode = expectedRows > 1 || effectiveNaturalWidth > availableWidth
            ? ToolbarLayoutMode.Compact
            : ToolbarLayoutMode.Normal;
        var centeredLeft = Clamp(
            selection.Left + ((selection.Width - width) / 2),
            workingArea.Left + scaledMargin,
            workingArea.Right - scaledMargin - width);

        var belowTop = selection.Bottom + scaledGap;
        if (belowTop + height <= workingArea.Bottom - scaledMargin)
        {
            return Result(centeredLeft, belowTop, width, height, ToolbarPlacementSide.Below, mode, expectedRows, availableWidth);
        }

        var aboveTop = selection.Top - scaledGap - height;
        if (aboveTop >= workingArea.Top + scaledMargin)
        {
            return Result(centeredLeft, aboveTop, width, height, ToolbarPlacementSide.Above, mode, expectedRows, availableWidth);
        }

        var insideBottom = selection.Bottom - scaledGap - height;
        if (insideBottom >= workingArea.Top + scaledMargin &&
            insideBottom + height <= workingArea.Bottom - scaledMargin)
        {
            return Result(centeredLeft, insideBottom, width, height, ToolbarPlacementSide.InsideBottom, mode, expectedRows, availableWidth);
        }

        var insideTop = selection.Top + scaledGap;
        if (insideTop >= workingArea.Top + scaledMargin &&
            insideTop + height <= workingArea.Bottom - scaledMargin)
        {
            return Result(centeredLeft, insideTop, width, height, ToolbarPlacementSide.InsideTop, mode, expectedRows, availableWidth);
        }

        return Result(
            Clamp(centeredLeft, workingArea.Left + scaledMargin, workingArea.Right - scaledMargin - width),
            Clamp(workingArea.Bottom - scaledMargin - height, workingArea.Top + scaledMargin, workingArea.Bottom - scaledMargin - height),
            width,
            height,
            ToolbarPlacementSide.Safe,
            mode,
            expectedRows,
            availableWidth);
    }

    private static ToolbarPlacement Result(
        double left,
        double top,
        double width,
        double height,
        ToolbarPlacementSide side,
        ToolbarLayoutMode mode,
        int expectedRows,
        double maximumWidth) =>
        new(new Rect(left, top, width, height), side, mode, expectedRows, maximumWidth);

    private static double Clamp(double value, double minimum, double maximum) =>
        maximum <= minimum ? minimum : Math.Clamp(value, minimum, maximum);
}
