namespace SlashText.Services;

internal sealed class UpdateNotificationGate
{
    private readonly object _sync = new();
    private string? _lastNotifiedVersion;

    public bool TryMark(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        lock (_sync)
        {
            if (string.Equals(
                    _lastNotifiedVersion,
                    version,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _lastNotifiedVersion = version;
            return true;
        }
    }

    public void Mark(string? version)
    {
        _ = TryMark(version);
    }
}
