using System.IO;
using System.Reflection;

namespace SlashText.Services;

public static class AppPaths
{
    private const string DefaultSnippetsResource = "SlashText.Defaults.snippets.md";
    private static readonly object Gate = new();
    private static AppDataEnvironment? _current;

    public static AppDataEnvironment Current => _current ??= AppDataEnvironment.Detect();
    public static DistributionMode Mode => Current.Mode;
    public static bool IsPortable => Current.IsPortable;
    public static string BaseDirectory => Current.ExecutableDirectory;
    public static string DataDirectory => Current.DataDirectory;
    public static string SnippetsFile => Path.Combine(DataDirectory, "snippets.md");
    public static string BackupsDirectory => Path.Combine(DataDirectory, "Backups");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string UsageFile => Path.Combine(DataDirectory, "usage.json");
    public static string AssetsDirectory => Path.Combine(DataDirectory, "assets");
    public static string CaptureHistoryFile => Path.Combine(DataDirectory, "capture-history.json");
    public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");
    public static string UpdatesDirectory => Path.Combine(DataDirectory, "Updates");
    public static string UpdateStateFile => Path.Combine(DataDirectory, "update-state.json");

    public static void Initialize(AppDataEnvironment environment)
    {
        lock (Gate)
        {
            _current = environment;
        }
    }

    internal static void ResetForTests() => _current = null;

    public static DataMigrationResult EnsureDataLayout()
    {
        var result = new DataMigrationService().EnsureLayout(Current);
        EnsureDefaultSnippets();
        return result;
    }

    private static void EnsureDefaultSnippets()
    {
        if (File.Exists(SnippetsFile))
        {
            return;
        }
        using var source = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(DefaultSnippetsResource);
        if (source is null)
        {
            return;
        }
        Directory.CreateDirectory(DataDirectory);
        var temporary = SnippetsFile + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (var destination = File.Create(temporary))
            {
                source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
            }
            File.Move(temporary, SnippetsFile);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
