namespace SlashText.Models;

public sealed class AppSettings
{
    public bool CloseToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool ShowSuggestions { get; set; } = true;
}
