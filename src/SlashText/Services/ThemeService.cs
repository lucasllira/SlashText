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

        // SlashDesk / Atalhos / V2: semantic tokens measured from the approved handoff.
        Set("CanvasBrush", dark ? "#10191E" : "#F8FAFB");
        Set("SurfaceBrush", dark ? "#152127" : "#FFFFFF");
        Set("ElevatedBrush", dark ? "#111C21" : "#F4F7F8");
        Set("PanelBrush", dark ? "#111C21" : "#F4F7F8");
        Set("ChromeBrush", dark ? "#10191E" : "#F8FAFB");
        Set("InputBrush", dark ? "#111C21" : "#FFFFFF");
        Set("InkBrush", dark ? "#EDF6F8" : "#14232B");
        Set("MutedBrush", dark ? "#9BABB2" : "#64747C");
        Set("TertiaryBrush", dark ? "#708087" : "#8A989F");
        Set("DividerBrush", dark ? "#293940" : "#D7E0E4");
        Set("BorderStrongBrush", dark ? "#3A4B52" : "#C4D1D6");
        Set("AccentBrush", dark ? "#30C8DF" : "#089BB2");
        Set("AccentStrongBrush", dark ? "#72D9E8" : "#087F95");
        Set("AccentBorderBrush", dark ? "#1D6070" : "#9BDBE4");
        Set("CodeBrush", dark ? "#060A0E" : "#151821");
        Set("HoverBrush", dark ? "#1B2B32" : "#EDF4F6");
        Set("SelectedBrush", dark ? "#0C343D" : "#E7F8FB");
        Set("ControlBrush", dark ? "#111C21" : "#F4F7F8");
        Set("ControlHoverBrush", dark ? "#1B2B32" : "#EDF4F6");
        Set("ControlPressedBrush", dark ? "#293940" : "#D7E0E4");
        Set("AccentSubtleBrush", dark ? "#0C343D" : "#E7F8FB");
        Set("FocusBrush", dark ? "#8030C8DF" : "#70089BB2");
        Set("DangerBrush", dark ? "#FF7F8D" : "#C84E5B");
        Set("DangerSubtleBrush", dark ? "#3A1D23" : "#FCE8EA");
        Set("SuccessBrush", dark ? "#42D894" : "#19A66A");
        Set("WarningBrush", dark ? "#F0AD4F" : "#D89024");

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
