using System.Drawing;

namespace SlashText.Services;

public static class QuickAccentPlacementCalculator
{
    public static Rectangle Place(
        Rectangle workingAreaPixels,
        Size desiredSizePixels,
        string position,
        int marginPixels = 16)
    {
        var width = Math.Min(Math.Max(1, desiredSizePixels.Width), workingAreaPixels.Width);
        var height = Math.Min(Math.Max(1, desiredSizePixels.Height), workingAreaPixels.Height);
        var left = workingAreaPixels.Left + Math.Max(0, (workingAreaPixels.Width - width) / 2);
        var top = position switch
        {
            "TopCenter" => workingAreaPixels.Top + marginPixels,
            "Center" => workingAreaPixels.Top + Math.Max(0, (workingAreaPixels.Height - height) / 2),
            _ => workingAreaPixels.Bottom - height - marginPixels
        };
        left = Math.Clamp(left, workingAreaPixels.Left, workingAreaPixels.Right - width);
        top = Math.Clamp(top, workingAreaPixels.Top, workingAreaPixels.Bottom - height);
        return new Rectangle(left, top, width, height);
    }
}
