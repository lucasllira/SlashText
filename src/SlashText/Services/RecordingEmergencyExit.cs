using System.Diagnostics;

namespace SlashText.Services;

internal static class RecordingEmergencyExit
{
    // Called only after an explicit confirmation. Do not run native Dispose or wait on
    // the recording queue: either can be the operation that is stuck. Target self only.
    internal static void TerminateCurrentProcess()
    {
        using var process = Process.GetCurrentProcess();
        process.Kill();
    }
}
