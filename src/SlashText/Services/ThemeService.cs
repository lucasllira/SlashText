using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SlashText.Services;

public static class ThemeService
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;

    public static bool IsDark { get; private set; }

    public static void Apply(string theme)
    {
        var dark = theme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ||
                   theme.Equals("System", StringComparison.OrdinalIgnoreCase) && IsSystemDark();
        IsDark = dark;

        Set("CanvasBrush", dark ? "#090B10" : "#F4F6FA");
        Set("SurfaceBrush", dark ? "#11151D" : "#FFFFFFFF");
        Set("ElevatedBrush", dark ? "#181D27" : "#F7F7FC");
        Set("InputBrush", dark ? "#0D1118" : "#FFFFFFFF");
        Set("InkBrush", dark ? "#F4F6FB" : "#18202B");
        Set("MutedBrush", dark ? "#9AA6B6" : "#697586");
        Set("DividerBrush", dark ? "#28303D" : "#E2E6ED");
        Set("AccentBrush", dark ? "#35C7D2" : "#087E8B");
        Set("CodeBrush", dark ? "#06080C" : "#151821");
        Set("HoverBrush", dark ? "#202833" : "#EEF2F4");
        Set("SelectedBrush", dark ? "#14363B" : "#DDF4F5");
        Set("ControlBrush", dark ? "#1A212B" : "#EDF1F5");
        Set("ControlHoverBrush", dark ? "#222C38" : "#E4EAEE");
        Set("ControlPressedBrush", dark ? "#2A3643" : "#D8E1E6");
        Set("AccentSubtleBrush", dark ? "#12343A" : "#DDF4F5");
        Set("DangerBrush", dark ? "#FF7D89" : "#C63C4A");
        Set("DangerSubtleBrush", dark ? "#3A1D23" : "#FCE8EA");
        Set("SuccessBrush", dark ? "#4FD7A5" : "#188A62");

        foreach (Window window in System.Windows.Application.Current.Windows)
        {
            ApplyToWindow(window);
        }
    }

    public static void ApplyToWindow(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var enabled = IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(
                handle,
                DwmwaUseImmersiveDarkMode,
                ref enabled,
                sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaUseImmersiveDarkModeBefore20H1,
                ref enabled,
                sizeof(int));
        }
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr window,
        int attribute,
        ref int value,
        int valueSize);
}
