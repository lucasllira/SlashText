namespace SlashText.Services;

public static class AppPaths
{
    public static string BaseDirectory => AppContext.BaseDirectory;
    public static string SnippetsFile => Path.Combine(BaseDirectory, "snippets.md");
    public static string BackupsDirectory => Path.Combine(BaseDirectory, "backups");
    public static string SettingsFile => Path.Combine(BaseDirectory, "settings.json");
    public static string UsageFile => Path.Combine(BaseDirectory, "usage.json");
}

