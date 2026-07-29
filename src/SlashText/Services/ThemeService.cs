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

        Set("CanvasBrush", dark ? "#090D12" : "#F5F7F9");
        Set("SurfaceBrush", dark ? "#0E151C" : "#FFFFFFFF");
        Set("ElevatedBrush", dark ? "#151E27" : "#F8FAFB");
        Set("PanelBrush", dark ? "#111922" : "#FBFCFD");
        Set("ChromeBrush", dark ? "#141E27" : "#F0F4F6");
        Set("InputBrush", dark ? "#0A1118" : "#FFFFFFFF");
        Set("InkBrush", dark ? "#F2F6F8" : "#15202A");
        Set("MutedBrush", dark ? "#99A8B5" : "#62717E");
        Set("DividerBrush", dark ? "#26333E" : "#DCE4E9");
        Set("AccentBrush", dark ? "#2BC9DA" : "#0AA9BB");
        Set("CodeBrush", dark ? "#060A0E" : "#151821");
        Set("HoverBrush", dark ? "#1A2731" : "#EDF3F5");
        Set("SelectedBrush", dark ? "#12363E" : "#DDF6F8");
        Set("ControlBrush", dark ? "#18232C" : "#F2F5F7");
        Set("ControlHoverBrush", dark ? "#20303B" : "#E4EAEE");
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
