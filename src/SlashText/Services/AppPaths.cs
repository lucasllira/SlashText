using System.IO;

namespace SlashText.Services;

public static class AppPaths
{
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

    public static string SnippetsFile => Path.Combine(BaseDirectory, "snippets.md");
    public static string BackupsDirectory => Path.Combine(BaseDirectory, "backups");
    public static string SettingsFile => Path.Combine(BaseDirectory, "settings.json");
    public static string UsageFile => Path.Combine(BaseDirectory, "usage.json");
    public static string AssetsDirectory => Path.Combine(BaseDirectory, "assets");
}
