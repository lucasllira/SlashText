namespace SlashText.Services;

internal sealed class StartupModuleCoordinator
{
    private readonly List<StartupModuleFailure> _failures = [];

    public IReadOnlyList<StartupModuleFailure> Failures => _failures;

    public async Task<bool> RunAsync(
        string module,
        Func<Task> initialize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentNullException.ThrowIfNull(initialize);

        try
        {
            await initialize();
            AppDiagnosticLog.Write(
                "startup.module.ready",
                ("module", module));
            return true;
        }
        catch (Exception exception)
        {
            RecordFailure(module, exception);
            return false;
        }
    }

    public bool Run(
        string module,
        Action initialize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentNullException.ThrowIfNull(initialize);

        try
        {
            initialize();
            AppDiagnosticLog.Write(
                "startup.module.ready",
                ("module", module));
            return true;
        }
        catch (Exception exception)
        {
            RecordFailure(module, exception);
            return false;
        }
    }

    private void RecordFailure(string module, Exception exception)
    {
        _failures.Add(new StartupModuleFailure(
            module,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.HResult));
        AppDiagnosticLog.Write(
            "startup.module.failed",
            ("module", module),
            ("exceptionType", exception.GetType().FullName),
            ("hresult", $"0x{exception.HResult:X8}"));
    }
}

internal sealed record StartupModuleFailure(
    string Module,
    string ExceptionType,
    int HResult);
