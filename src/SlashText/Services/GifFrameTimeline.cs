namespace SlashText.Services;

internal readonly record struct GifFrameTiming(int FrameIndex, int DelayCentiseconds);

internal static class GifFrameTimeline
{
    // Allocate one common stopped-clock budget, including every 20 ms minimum.
    // Clamping independent intervals adds those minima repeatedly under jitter.
    internal static IReadOnlyList<GifFrameTiming> Build(IReadOnlyList<TimeSpan> starts, TimeSpan end)
    {
        if (starts.Count == 0 || starts[0] != TimeSpan.Zero || end < TimeSpan.Zero)
            throw new ArgumentException("Invalid GIF clock boundaries");
        for (var i = 1; i < starts.Count; i++)
            if (starts[i] < starts[i - 1] || starts[i] > end)
                throw new ArgumentException("Invalid GIF clock boundaries");

        var budget = Math.Max(2, checked((int)Math.Round(end.TotalMilliseconds / 10d)));
        var count = Math.Min(starts.Count, budget / 2);
        var result = new GifFrameTiming[count];
        var previous = 0;
        var previousIndex = 0;
        for (var i = 1; i < count; i++)
        {
            // Only when the clock cannot hold every minimum, select an ordered
            // subset. Keep both endpoints whenever at least two images fit.
            var index = (int)((long)i * (starts.Count - 1) / (count - 1));
            var ideal = checked((int)Math.Round(starts[index].TotalMilliseconds / 10d));
            var boundary = Math.Clamp(ideal, previous + 2, budget - 2 * (count - i));
            result[i - 1] = new(previousIndex, boundary - previous);
            previous = boundary;
            previousIndex = index;
        }
        result[^1] = new(previousIndex, budget - previous);
        return result;
    }
}
