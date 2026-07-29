using System.IO;

namespace SlashText.Models;

public sealed class AppSettings
{
    public bool OnboardingCompleted { get; set; }
    public bool CheckUpdatesOnStartup { get; set; } = true;
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
    public CaptureSettings Capture { get; set; } = new();
}

public sealed class CaptureSettings
{
    public string ActiveMonitorShortcut { get; set; } = "Ctrl+Shift+PrintScreen";
    public string RegionShortcut { get; set; } = "Ctrl+Alt+PrintScreen";
    public string WindowShortcut { get; set; } = "Ctrl+Shift+WheelUp";
    public string OutputDirectoryTemplate { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "SlashDesk", "{year}", "{month}");
    public string FileNameTemplate { get; set; } =
        "{date}_{time}_{type}_{app}";
    public string ImageFormat { get; set; } = "PNG";
    public int JpegQuality { get; set; } = 90;
    public bool CopyToClipboard { get; set; } = true;
    public bool SaveAutomatically { get; set; } = true;
    public bool HideSlashDeskDuringCapture { get; set; } = true;
}
