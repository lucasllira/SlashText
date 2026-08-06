using System.IO;

namespace SlashText.Services;

public static class AppPaths
{
    private const string DataFolderName = "SlashDeskData";

    public static string BaseDirectory
    {
        get
        {
            var executablePath = Environment.ProcessPath;
            var executableDirectory = string.IsNullOrWhiteSpace(executablePath)
                ? null
                : Path.GetDirectoryName(executablePath);

            return string.IsNullOrWhiteSpace(executableDirectory)
                ? AppContext.BaseDirectory
                : executableDirectory;
        }
    }

    public static string DataDirectory => Path.Combine(BaseDirectory, DataFolderName);
    public static string SnippetsFile => Path.Combine(DataDirectory, "snippets.md");
    public static string BackupsDirectory => Path.Combine(DataDirectory, "backups");
    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    public static string UsageFile => Path.Combine(DataDirectory, "usage.json");
    public static string AssetsDirectory => Path.Combine(DataDirectory, "assets");
    public static string CaptureHistoryFile => Path.Combine(DataDirectory, "capture-history.json");
    public static string LogsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SlashDesk",
        "Logs");

    public static void EnsureDataLayout()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        MigrateProductDataDirectory();
        MigrateFile("snippets.md", SnippetsFile);
        MigrateFile("settings.json", SettingsFile);
        MigrateFile("usage.json", UsageFile);
        MigrateDirectory("assets", AssetsDirectory);
        MigrateDirectory("backups", BackupsDirectory);
    }

    private static void MigrateProductDataDirectory()
    {
        var legacy = Path.Combine(BaseDirectory, "SlashTextData");
        if (!Directory.Exists(legacy) ||
            legacy.Equals(DataDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MigrateDirectory("SlashTextData", DataDirectory, overwriteExisting: true);
    }

    private static void MigrateFile(string legacyName, string destination)
    {
        var legacy = Path.Combine(BaseDirectory, legacyName);
        if (!File.Exists(legacy) ||
            legacy.Equals(destination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Na atualização, o pacote pode trazer um snippets.md inicial dentro da
        // nova pasta. O arquivo legado contém os dados reais do usuário e tem
        // prioridade sobre esse modelo.
        File.Move(legacy, destination, overwrite: true);
    }

    private static void MigrateDirectory(
        string legacyName,
        string destination,
        bool overwriteExisting = false)
    {
        var legacy = Path.Combine(BaseDirectory, legacyName);
        if (!Directory.Exists(legacy) ||
            legacy.Equals(destination, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.Exists(destination))
        {
            Directory.Move(legacy, destination);
            return;
        }

        foreach (var file in Directory.EnumerateFiles(legacy, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(legacy, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (overwriteExisting || !File.Exists(target))
            {
                File.Move(file, target, overwriteExisting);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(
                     legacy,
                     "*",
                     SearchOption.AllDirectories).OrderByDescending(item => item.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
            {
                Directory.Delete(directory);
            }
        }

        if (!Directory.EnumerateFileSystemEntries(legacy).Any())
        {
            Directory.Delete(legacy);
        }
    }
}
