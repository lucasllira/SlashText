using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace SlashText.Services;

public enum CaptureShortcutAction
{
    ActiveMonitor,
    Region,
    Window
}

public sealed class CaptureShortcutEventArgs(CaptureShortcutAction action) : EventArgs
{
    public CaptureShortcutAction Action { get; } = action;
}

public sealed class GlobalCaptureShortcutService : IDisposable
{
    private const int HotKeyMessage = 0x0312;
    private const int MouseLowLevel = 14;
    private const int MouseWheel = 0x020A;
    private readonly Dictionary<int, CaptureShortcutAction> _actions = [];
    private readonly HookProcedure _mouseProcedure;
    private HwndSource? _source;
    private IntPtr _window;
    private IntPtr _mouseHook;
    private CaptureSettingsSnapshot[] _wheelShortcuts = [];

    public event EventHandler<CaptureShortcutEventArgs>? Triggered;

    public GlobalCaptureShortcutService()
    {
        _mouseProcedure = MouseHook;
    }

    public IReadOnlyList<string> Configure(
        Window owner,
        string activeMonitor,
        string region,
        string window)
    {
        DisposeRegistrations();
        _window = new WindowInteropHelper(owner).EnsureHandle();
        _source = HwndSource.FromHwnd(_window);
        _source?.AddHook(WindowHook);

        var errors = new List<string>();
        var values = new[]
        {
            (CaptureShortcutAction.ActiveMonitor, activeMonitor),
            (CaptureShortcutAction.Region, region),
            (CaptureShortcutAction.Window, window)
        };
        var wheels = new List<CaptureSettingsSnapshot>();
        var id = 4100;
        foreach (var (action, text) in values)
        {
            if (!TryParse(text, out var parsed))
            {
                errors.Add($"Atalho inválido: {text}");
                continue;
            }

            if (parsed.WheelDirection != 0)
            {
                wheels.Add(new CaptureSettingsSnapshot(action, parsed));
                continue;
            }
            if (!RegisterHotKey(_window, id, parsed.Modifiers, parsed.Key))
            {
                errors.Add($"Atalho em uso: {text}");
                continue;
            }
            _actions[id++] = action;
        }

        _wheelShortcuts = wheels.ToArray();
        if (_wheelShortcuts.Length > 0)
        {
            _mouseHook = SetWindowsHookEx(MouseLowLevel, _mouseProcedure, IntPtr.Zero, 0);
            if (_mouseHook == IntPtr.Zero)
            {
                errors.Add("Não foi possível ativar atalhos com a roda do mouse.");
            }
        }
        return errors;
    }

    public static bool IsValid(string value) => TryParse(value, out _);

    public void Dispose()
    {
        DisposeRegistrations();
        GC.SuppressFinalize(this);
    }

    private IntPtr WindowHook(
        IntPtr handle,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == HotKeyMessage && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            handled = true;
            Triggered?.Invoke(this, new CaptureShortcutEventArgs(action));
        }
        return IntPtr.Zero;
    }

    private IntPtr MouseHook(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToInt32() == MouseWheel && _wheelShortcuts.Length > 0)
        {
            var data = Marshal.PtrToStructure<MouseHookData>(lParam);
            var delta = (short)((data.MouseData >> 16) & 0xffff);
            foreach (var item in _wheelShortcuts)
            {
                if (Math.Sign(delta) == item.Shortcut.WheelDirection &&
                    ModifiersDown(item.Shortcut.Modifiers))
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        new Action(() => Triggered?.Invoke(
                            this,
                            new CaptureShortcutEventArgs(item.Action))));
                    return (IntPtr)1;
                }
            }
        }
        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private void DisposeRegistrations()
    {
        foreach (var id in _actions.Keys)
        {
            if (_window != IntPtr.Zero)
            {
                UnregisterHotKey(_window, id);
            }
        }
        _actions.Clear();
        if (_source is not null)
        {
            _source.RemoveHook(WindowHook);
            _source = null;
        }
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
        _wheelShortcuts = [];
    }

    private static bool ModifiersDown(uint modifiers) =>
        ((modifiers & 1) == 0 || Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) &&
        ((modifiers & 2) == 0 || Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
        ((modifiers & 4) == 0 || Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) &&
        ((modifiers & 8) == 0 || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin));

    private static bool TryParse(string text, out ParsedShortcut result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        uint modifiers = 0;
        uint key = 0;
        var wheel = 0;
        foreach (var rawPart in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var part = rawPart.ToLowerInvariant();
            switch (part)
            {
                case "alt": modifiers |= 1; break;
                case "ctrl":
                case "control": modifiers |= 2; break;
                case "shift": modifiers |= 4; break;
                case "win":
                case "windows": modifiers |= 8; break;
                case "wheelup":
                case "scrollup": wheel = 1; break;
                case "wheeldown":
                case "scrolldown": wheel = -1; break;
                default:
                    var keyName = part switch
                    {
                        "printscreen" or "prtsc" => nameof(Key.Snapshot),
                        "escape" => nameof(Key.Escape),
                        "pageup" => nameof(Key.PageUp),
                        "pagedown" => nameof(Key.PageDown),
                        _ => rawPart
                    };
                    if (!Enum.TryParse<Key>(keyName, true, out var parsedKey))
                    {
                        return false;
                    }
                    key = (uint)KeyInterop.VirtualKeyFromKey(parsedKey);
                    break;
            }
        }

        if (wheel != 0 && modifiers == 0)
        {
            return false;
        }
        if (wheel == 0 && key == 0)
        {
            return false;
        }
        result = new ParsedShortcut(modifiers, key, wheel);
        return true;
    }

    private readonly record struct ParsedShortcut(uint Modifiers, uint Key, int WheelDirection);
    private readonly record struct CaptureSettingsSnapshot(
        CaptureShortcutAction Action,
        ParsedShortcut Shortcut);

    private delegate IntPtr HookProcedure(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(
        int idHook,
        HookProcedure procedure,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hook,
        int code,
        IntPtr wParam,
        IntPtr lParam);
}
