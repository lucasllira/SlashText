namespace SlashText.Services;

public sealed class DebounceGate(TimeSpan interval)
{
    private readonly long _minimumTicks = Math.Max(1, (long)(interval.TotalSeconds * System.Diagnostics.Stopwatch.Frequency));
    private long _lastAccepted;

    public bool TryAccept(long timestamp)
    {
        while (true)
        {
            var previous = Volatile.Read(ref _lastAccepted);
            if (previous != 0 && timestamp - previous < _minimumTicks)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _lastAccepted, timestamp, previous) == previous)
            {
                return true;
            }
        }
    }
}
