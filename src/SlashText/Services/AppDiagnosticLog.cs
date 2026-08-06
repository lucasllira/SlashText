using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using SlashText.Models;

namespace SlashText.Services;

public static class AppDiagnosticLog
{
    private static readonly object Gate = new();
    private static readonly string ActiveRecordingMarker = Path.Combine(
        AppPaths.LogsDirectory,
        "recording-active.json");
    private static string _currentLogPath = string.Empty;

    public static string CurrentLogPath
    {
        get
        {
            EnsureInitialized();
            return _currentLogPath;
        }
    }

    public static void Initialize()
    {
        EnsureInitialized();
        if (File.Exists(ActiveRecordingMarker))
        {
            Write("recording.previous-process-ended-without-finalization");
            TryDelete(ActiveRecordingMarker);
        }

        Write(
            "application.start",
            ("slashDeskVersion", ProductVersion()),
            ("windowsVersion", Environment.OSVersion.VersionString),
            ("processArchitecture", Environment.Is64BitProcess ? "x64" : "x86"),
            ("framework", Environment.Version.ToString()));
    }

    public static void Write(string stage, params (string Key, object? Value)[] fields)
    {
        try
        {
            EnsureInitialized();
            var entry = new Dictionary<string, object?>
            {
                ["timestampUtc"] = DateTimeOffset.UtcNow,
                ["stage"] = stage,
                ["processId"] = Environment.ProcessId,
                ["threadId"] = Environment.CurrentManagedThreadId
            };
            foreach (var (key, value) in fields)
            {
                entry[key] = value is string text ? Sanitize(text) : value;
            }

            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            lock (Gate)
            {
                File.AppendAllText(_currentLogPath, line, new UTF8Encoding(false));
            }
        }
        catch
        {
            // Diagnostics must never bring down the application.
        }
    }

    public static void WriteException(string stage, Exception exception)
    {
        Write(
            stage,
            ("exceptionType", exception.GetType().FullName),
            ("hresult", $"0x{exception.HResult:X8}"),
            ("message", Sanitize(exception.Message)));
    }

    public static string CreateLibraryLogPath(Guid? recordingId = null)
    {
        Directory.CreateDirectory(AppPaths.LogsDirectory);
        var suffix = recordingId is null ? string.Empty : $"-{recordingId.Value:N}";
        return Path.Combine(
            AppPaths.LogsDirectory,
            $"screenrecorderlib-{DateTimeOffset.Now:yyyyMMdd-HHmmss-fff}{suffix}.log");
    }

    public static void MarkRecordingActive(
        Guid recordingId,
        RecordingTarget target,
        RecordingSettings settings,
        string encoderPolicy)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            var marker = JsonSerializer.Serialize(new
            {
                recordingId = recordingId.ToString("N"),
                timestampUtc = DateTimeOffset.UtcNow,
                target = target.Kind.ToString(),
                width = target.Bounds.Width,
                height = target.Bounds.Height,
                fps = settings.VideoFps,
                encoderPolicy
            });
            File.WriteAllText(ActiveRecordingMarker, marker, new UTF8Encoding(false));
        }
        catch
        {
            // The regular log still records the last managed stage.
        }
    }

    public static void MarkRecordingEnded() => TryDelete(ActiveRecordingMarker);

    private static void EnsureInitialized()
    {
        if (!string.IsNullOrWhiteSpace(_currentLogPath))
        {
            return;
        }

        lock (Gate)
        {
            if (!string.IsNullOrWhiteSpace(_currentLogPath))
            {
                return;
            }
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            _currentLogPath = Path.Combine(
                AppPaths.LogsDirectory,
                $"slashdesk-{DateTimeOffset.Now:yyyyMMdd}.jsonl");
        }
    }

    private static string ProductVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";

    private static string Sanitize(string value)
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(profile)
            ? value
            : value.Replace(profile, "%UserProfile%", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
