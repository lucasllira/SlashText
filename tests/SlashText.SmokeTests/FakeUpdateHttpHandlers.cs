
sealed class FakePackageHttpHandler(byte[] package, byte[] checksum) : HttpMessageHandler
{
    public TimeSpan Delay { get; init; }
    public TaskCompletionSource Started { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }
        var body = request.RequestUri?.AbsolutePath.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) == true
            ? checksum
            : package;
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(body)
        };
    }
}

sealed class FakeUpdateHttpHandler : HttpMessageHandler
{
    private readonly Exception? _exception;
    private readonly TimeSpan _delay;
    private int _calls;

    public FakeUpdateHttpHandler(string responseJson) => ResponseJson = responseJson;
    public FakeUpdateHttpHandler(Exception exception) => _exception = exception;
    public FakeUpdateHttpHandler(TimeSpan delay)
    {
        _delay = delay;
        ResponseJson = "[]";
    }

    public string ResponseJson { get; set; } = "[]";
    public int Calls => Volatile.Read(ref _calls);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (_exception is not null)
        {
            throw _exception;
        }
        if (_delay > TimeSpan.Zero)
        {
            await Task.Delay(_delay, cancellationToken);
        }
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(ResponseJson)
        };
    }
}
