namespace SlashText.Models;

public sealed class AppSettings
{
    public bool CloseToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ShowSuggestions { get; set; } = true;
    public string Theme { get; set; } = "System";
    public bool QuickAccentEnabled { get; set; }
    public string QuickAccentActivationKey { get; set; } = "Space";
    public string QuickAccentToolbarPosition { get; set; } = "BottomCenter";
    public bool QuickAccentShowUnicode { get; set; }
    public bool QuickAccentSortByUsage { get; set; } = true;
    public int QuickAccentInputDelayMs { get; set; } = 200;
    public string QuickAccentExcludedApps { get; set; } = string.Empty;
    public List<string> QuickAccentCharacterSets { get; set; } = ["PortugueseBrazil"];
}
