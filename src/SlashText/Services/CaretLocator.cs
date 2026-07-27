using System.Runtime.InteropServices;
using System.Windows;
using Point = System.Windows.Point;

namespace SlashText.Services;

public static class CaretLocator
{
    public static Point GetScreenPosition()
    {
        var foreground = GetForegroundWindow();
        var thread = GetWindowThreadProcessId(foreground, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };

        if (thread != 0 && GetGUIThreadInfo(thread, ref info) && info.CaretWindow != IntPtr.Zero)
        {
            var point = new NativePoint
            {
                X = info.CaretRectangle.Left,
                Y = info.CaretRectangle.Bottom + 6
            };

            if (ClientToScreen(info.CaretWindow, ref point))
            {
                return new Point(point.X, point.Y);
            }
        }

        return GetCursorPos(out var cursor)
            ? new Point(cursor.X + 12, cursor.Y + 18)
            : new Point(100, 100);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public NativeRect CaretRectangle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGUIThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
