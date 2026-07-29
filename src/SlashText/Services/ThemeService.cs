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

        Set("CanvasBrush", dark ? "#0A0F14" : "#F3F5F7");
        Set("SurfaceBrush", dark ? "#10171E" : "#FFFFFFFF");
        Set("ElevatedBrush", dark ? "#172129" : "#F7F9FA");
        Set("PanelBrush", dark ? "#131C24" : "#F8FAFB");
        Set("ChromeBrush", dark ? "#172129" : "#EEF2F4");
        Set("InputBrush", dark ? "#0C1218" : "#FFFFFFFF");
        Set("InkBrush", dark ? "#F3F7F9" : "#17212B");
        Set("MutedBrush", dark ? "#9BAAB5" : "#65727D");
        Set("DividerBrush", dark ? "#2A3741" : "#D9E0E5");
        Set("AccentBrush", dark ? "#2BC9DA" : "#099CAD");
        Set("CodeBrush", dark ? "#060A0E" : "#151821");
        Set("HoverBrush", dark ? "#19252E" : "#EDF1F3");
        Set("SelectedBrush", dark ? "#123A42" : "#DDF4F6");
        Set("ControlBrush", dark ? "#18232C" : "#F1F4F6");
        Set("ControlHoverBrush", dark ? "#21313C" : "#E3E9ED");
        Set("ControlPressedBrush", dark ? "#293B47" : "#D8E1E6");
        Set("AccentSubtleBrush", dark ? "#10363D" : "#E2F7F9");
        Set("FocusBrush", dark ? "#662BC9DA" : "#500AA9BB");
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
