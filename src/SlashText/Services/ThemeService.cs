using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace SlashText.Services;

public static class ThemeService
{
    public static void Apply(string theme)
    {
        var dark = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ||
                   theme.Equals("System", StringComparison.OrdinalIgnoreCase) && IsSystemDark();

        Set("CanvasBrush", dark ? "#090B10" : "#F4F6FA");
        Set("SurfaceBrush", dark ? "#11151D" : "#FFFFFFFF");
        Set("ElevatedBrush", dark ? "#181D27" : "#F7F7FC");
        Set("InputBrush", dark ? "#0D1118" : "#FFFFFFFF");
        Set("InkBrush", dark ? "#F4F6FB" : "#18202B");
        Set("MutedBrush", dark ? "#9AA6B6" : "#697586");
        Set("DividerBrush", dark ? "#28303D" : "#E2E6ED");
        Set("AccentBrush", dark ? "#8B83FF" : "#635BFF");
        Set("CodeBrush", dark ? "#06080C" : "#151821");
    }

    private static void Set(string key, string color) =>
        System.Windows.Application.Current.Resources[key] =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

    private static bool IsSystemDark()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1) is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
