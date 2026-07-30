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

        Set("CanvasBrush", dark ? "#081019" : "#EEF2F5");
        Set("SurfaceBrush", dark ? "#0E1822" : "#FFFEFC");
        Set("ElevatedBrush", dark ? "#14222D" : "#F6F8FA");
        Set("PanelBrush", dark ? "#101C26" : "#FAFBFC");
        Set("ChromeBrush", dark ? "#15232E" : "#F3F6F8");
        Set("InputBrush", dark ? "#0A141D" : "#FFFFFF");
        Set("InkBrush", dark ? "#F4F7F9" : "#14202B");
        Set("MutedBrush", dark ? "#9DADB9" : "#64727E");
        Set("DividerBrush", dark ? "#263845" : "#D9E1E7");
        Set("AccentBrush", dark ? "#28C7D9" : "#079FB2");
        Set("CodeBrush", dark ? "#060A0E" : "#151821");
        Set("HoverBrush", dark ? "#182833" : "#EAF0F3");
        Set("SelectedBrush", dark ? "#113944" : "#DDF5F8");
        Set("ControlBrush", dark ? "#172631" : "#F0F4F6");
        Set("ControlHoverBrush", dark ? "#20333F" : "#E2E9ED");
        Set("ControlPressedBrush", dark ? "#29404D" : "#D5E0E5");
        Set("AccentSubtleBrush", dark ? "#10333D" : "#E0F6F8");
        Set("FocusBrush", dark ? "#6628C7D9" : "#50079FB2");
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
