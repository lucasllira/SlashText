using System.Text.Json;

namespace SlashText.Services;

public static class SafeDiagnosticLog
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("SLASHDESK_DIAGNOSTICS"), "1", StringComparison.Ordinal) ||
        File.Exists(Path.Combine(AppPaths.LogsDirectory, "diagnostics.enabled"));

    public static void Write(string eventType, IReadOnlyDictionary<string, object?>? fields = null)
    {
        if (!Enabled)
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(AppPaths.LogsDirectory);
            var record = new Dictionary<string, object?>
            {
                ["timestampUtc"] = DateTimeOffset.UtcNow,
                ["eventType"] = eventType,
                ["threadId"] = Environment.CurrentManagedThreadId
            };
            if (fields is not null)
            {
                foreach (var field in fields)
                {
                    record[field.Key] = field.Value;
                }
            }
            var line = JsonSerializer.Serialize(record, JsonOptions) + Environment.NewLine;
            lock (Gate)
            {
                File.AppendAllText(
                    Path.Combine(AppPaths.LogsDirectory, $"functional-{DateTime.UtcNow:yyyyMMdd}.jsonl"),
                    line);
            }
        }
        catch
        {
            // Diagnóstico opcional nunca pode interromper expansão ou captura.
        }
    }
}
