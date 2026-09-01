namespace SlashText.Services;

public sealed class CaptureToolSelection
{
    public CaptureToolSelection(CaptureAnnotationKind initial) => Selected = initial;

    public CaptureAnnotationKind Selected { get; private set; }

    public void Select(CaptureAnnotationKind tool) => Selected = tool;

    public bool IsSelected(CaptureAnnotationKind tool) => Selected == tool;
}

public static class CaptureToolbarLayoutPolicy
{
    public const double NormalMinimumWidthDips = 620;

    public static bool ShouldUseCompactMode(double availableWidthDips) =>
        availableWidthDips < NormalMinimumWidthDips;
}

public static class CaptureMotion
{
    public static TimeSpan Duration(bool animationsEnabled, int milliseconds) =>
        animationsEnabled
            ? TimeSpan.FromMilliseconds(Math.Max(0, milliseconds))
            : TimeSpan.Zero;
}
