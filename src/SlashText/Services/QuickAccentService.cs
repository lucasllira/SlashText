using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SlashText.Services;

public sealed class QuickAccentService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const uint LlkhfInjected = 0x00000010;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;

    private static readonly Dictionary<int, string> Characters = new()
    {
        [0x41] = "áàâãäåæ",
        [0x43] = "çćč",
        [0x45] = "éèêë",
        [0x49] = "íìîï",
        [0x4E] = "ñń",
        [0x4F] = "óòôõöøœ",
        [0x53] = "šß",
        [0x55] = "úùûü",
        [0x59] = "ýÿ",
        [0x5A] = "žźż"
    };

    private readonly LowLevelKeyboardProc _callback;
    private readonly Dictionary<char, int> _usage = [];
    private IntPtr _hook;
    private int? _baseKey;
    private long _basePressedAt;
    private string _choices = string.Empty;
    private int _selected;
    private bool _active;
    private bool _disposed;

    public QuickAccentService() => _callback = HookCallback;

    public bool Enabled { get; set; }
    public string ActivationKey { get; set; } = "Space";
    public bool SortByUsage { get; set; } = true;
    public int InputDelayMs { get; set; } = 200;
    public string ExcludedApps { get; set; } = string.Empty;
    public bool IsRunning => _hook != IntPtr.Zero;

    public event EventHandler<QuickAccentChangedEventArgs>? Changed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(module?.ModuleName), 0);
        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"O Windows não permitiu iniciar o Acento Rápido (erro {Marshal.GetLastWin32Error()}).");
        }
    }

    private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code < 0)
        {
            return CallNextHookEx(_hook, code, message, data);
        }

        var keyboard = Marshal.PtrToStructure<KeyboardData>(data);
        if ((keyboard.Flags & LlkhfInjected) != 0 ||
            !Enabled ||
            IsOwnProcessInForeground() ||
            IsExcludedApp())
        {
            return CallNextHookEx(_hook, code, message, data);
        }

        var key = (int)keyboard.VirtualKeyCode;
        var isDown = message.ToInt32() is WmKeyDown or WmSysKeyDown;
        var isUp = message.ToInt32() is WmKeyUp or WmSysKeyUp;

        if (isDown && Characters.ContainsKey(key) && _baseKey is null)
        {
            _baseKey = key;
            _basePressedAt = Environment.TickCount64;
        }

        if (isDown && _baseKey is not null && key == ActivationVirtualKey())
        {
            if (Environment.TickCount64 - _basePressedAt < Math.Clamp(InputDelayMs, 0, 2000))
            {
                return CallNextHookEx(_hook, code, message, data);
            }

            if (!_active)
            {
                _choices = MatchCase(Characters[_baseKey.Value], IsKeyDown(0x10));
                if (SortByUsage)
                {
                    _choices = new string(_choices
                        .OrderByDescending(character => _usage.GetValueOrDefault(character))
                        .ToArray());
                }
                _selected = 0;
                _active = true;
            }
            else
            {
                _selected = (_selected + 1) % _choices.Length;
            }

            Changed?.Invoke(this, new QuickAccentChangedEventArgs(_choices, _selected, true));
            return new IntPtr(1);
        }

        if (_active && isDown && key is 0x25 or 0x27)
        {
            _selected = key == 0x25
                ? (_selected - 1 + _choices.Length) % _choices.Length
                : (_selected + 1) % _choices.Length;
            Changed?.Invoke(this, new QuickAccentChangedEventArgs(_choices, _selected, true));
            return new IntPtr(1);
        }

        if (_active && isDown && key == 0x1B)
        {
            Reset();
            return new IntPtr(1);
        }

        if (isUp && _baseKey == key)
        {
            if (_active && _choices.Length > 0)
            {
                _usage[_choices[_selected]] = _usage.GetValueOrDefault(_choices[_selected]) + 1;
                ReplaceBaseCharacter(_choices[_selected]);
            }
            Reset();
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    private void Reset()
    {
        _baseKey = null;
        _active = false;
        _choices = string.Empty;
        Changed?.Invoke(this, new QuickAccentChangedEventArgs(string.Empty, 0, false));
    }

    private int ActivationVirtualKey() => ActivationKey switch
    {
        "Left" => 0x25,
        "Right" => 0x27,
        _ => 0x20
    };

    private bool IsExcludedApp()
    {
        if (string.IsNullOrWhiteSpace(ExcludedApps))
        {
            return false;
        }

        try
        {
            var window = GetForegroundWindow();
            _ = GetWindowThreadProcessId(window, out var processId);
            var processName = Process.GetProcessById((int)processId).ProcessName;
            return ExcludedApps
                .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(item => item.Equals(processName, StringComparison.OrdinalIgnoreCase) ||
                             item.Equals($"{processName}.exe", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOwnProcessInForeground()
    {
        var window = GetForegroundWindow();
        _ = GetWindowThreadProcessId(window, out var processId);
        return processId == Environment.ProcessId;
    }

    private static string MatchCase(string value, bool upper) =>
        upper ? value.ToUpperInvariant() : value;

    private static void ReplaceBaseCharacter(char character)
    {
        var inputs = new[]
        {
            KeyInput(0x08, 0, false),
            KeyInput(0x08, 0, true),
            KeyInput(0, character, false, true),
            KeyInput(0, character, true, true)
        };
        _ = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    private static Input KeyInput(ushort key, char scan, bool keyUp, bool unicode = false) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInputData
                {
                    VirtualKey = key,
                    ScanCode = scan,
                    Flags = (keyUp ? KeyEventKeyUp : 0) | (unicode ? KeyEventUnicode : 0)
                }
            }
        };

    private static bool IsKeyDown(int virtualKey) =>
        (GetKeyState(virtualKey) & 0x8000) != 0;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        if (_hook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _disposed = true;
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr message, IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardData
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInputData Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public char ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}

public sealed class QuickAccentChangedEventArgs(
    string choices, int selectedIndex, bool visible) : EventArgs
{
    public string Choices { get; } = choices;
    public int SelectedIndex { get; } = selectedIndex;
    public bool Visible { get; } = visible;
}
