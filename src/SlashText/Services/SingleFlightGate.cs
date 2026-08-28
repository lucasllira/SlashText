namespace SlashText.Services;

public sealed class SingleFlightGate
{
    private int _active;

    public bool IsActive => Volatile.Read(ref _active) != 0;

    public IDisposable? TryEnter()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
        {
            return null;
        }

        return new Lease(this);
    }

    private void Exit() => Volatile.Write(ref _active, 0);

    private sealed class Lease(SingleFlightGate owner) : IDisposable
    {
        private SingleFlightGate? _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Exit();
    }
}
