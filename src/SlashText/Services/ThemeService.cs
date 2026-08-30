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

        // Fluent 3.0: neutral light surfaces and a true-black theme without blue tint.
        Set("CanvasBrush", dark ? "#000000" : "#F3F3F3");
        Set("SurfaceBrush", dark ? "#0B0B0B" : "#FFFFFF");
        Set("ElevatedBrush", dark ? "#151515" : "#F8F8F8");
        Set("PanelBrush", dark ? "#101010" : "#F7F7F7");
        Set("ChromeBrush", dark ? "#050505" : "#FAFAFA");
        Set("InputBrush", dark ? "#171717" : "#FFFFFF");
        Set("InkBrush", dark ? "#F5F5F5" : "#1B1B1B");
        Set("MutedBrush", dark ? "#B3B3B3" : "#616161");
        Set("TertiaryBrush", dark ? "#858585" : "#8A8A8A");
        Set("DividerBrush", dark ? "#303030" : "#DADADA");
        Set("BorderStrongBrush", dark ? "#4A4A4A" : "#C4C4C4");
        Set("AccentBrush", dark ? "#2BC3D6" : "#089BB2");
        Set("AccentStrongBrush", dark ? "#68D8E6" : "#087F95");
        Set("AccentBorderBrush", dark ? "#207786" : "#9BDBE4");
        Set("CodeBrush", dark ? "#050505" : "#151821");
        Set("HoverBrush", dark ? "#1D1D1D" : "#ECECEC");
        Set("SelectedBrush", dark ? "#12383E" : "#E7F8FB");
        Set("ControlBrush", dark ? "#171717" : "#F4F4F4");
        Set("ControlHoverBrush", dark ? "#242424" : "#EAEAEA");
        Set("ControlPressedBrush", dark ? "#343434" : "#D7D7D7");
        Set("AccentSubtleBrush", dark ? "#102F34" : "#E7F8FB");
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
