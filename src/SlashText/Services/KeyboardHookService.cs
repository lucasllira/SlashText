using System.Diagnostics;
using System.Runtime.InteropServices;
using SlashText.Models;

namespace SlashText.Services;

public sealed class KeyboardHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkhfInjected = 0x00000010;

    private readonly object _sync = new();
    private readonly LowLevelKeyboardProc _hookCallback;
    private List<MonitoredSnippet> _snippets = [];
    private IntPtr _hookHandle;
    private string _buffer = string.Empty;
    private IntPtr _bufferWindow;
    private bool _disposed;

    public KeyboardHookService()
    {
        _hookCallback = HookCallback;
    }

    public event EventHandler<SnippetExpansionRequestedEventArgs>? ExpansionRequested;

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void UpdateSnippets(IEnumerable<Snippet> snippets)
    {
        lock (_sync)
        {
            _snippets = snippets
                .Where(snippet => snippet.Enabled)
                .Select(snippet => new MonitoredSnippet(
                    snippet,
                    snippet.Trigger.ToLowerInvariant(),
                    new HashSet<string>(
                        snippet.ConfirmKeys,
                        StringComparer.OrdinalIgnoreCase)))
                .ToList();
            ClearBuffer();
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = GetModuleHandle(module?.ModuleName);
        _hookHandle = SetWindowsHookEx(WhKeyboardLl, _hookCallback, moduleHandle, 0);

        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"O Windows não permitiu iniciar o monitoramento do teclado (erro {Marshal.GetLastWin32Error()}).");
        }
    }

    private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 ||
            (message.ToInt32() != WmKeyDown && message.ToInt32() != WmSysKeyDown))
        {
            return CallNextHookEx(_hookHandle, code, message, data);
        }

        var key = Marshal.PtrToStructure<KeyboardData>(data);
        if ((key.Flags & LlkhfInjected) != 0 || IsOwnWindowInForeground())
        {
            return CallNextHookEx(_hookHandle, code, message, data);
        }

        Snippet? snippetToExpand = null;
        var suppressKey = false;

        lock (_sync)
        {
            var foregroundWindow = GetForegroundWindow();
            if (_buffer.Length > 0 && foregroundWindow != _bufferWindow)
            {
                ClearBuffer();
            }

            var virtualKey = (int)key.VirtualKeyCode;
            var confirmation = ConfirmationName(virtualKey);

            if (confirmation is not null)
            {
                var exact = FindExactMatch();
                if (exact is not null && exact.ConfirmKeys.Contains(confirmation))
                {
                    snippetToExpand = exact.Snippet;
                    suppressKey = true;
                }

                ClearBuffer();
            }
            else if (virtualKey == 0x1B)
            {
                ClearBuffer();
            }
            else if (virtualKey == 0x08)
            {
                if (_buffer.Length > 0)
                {
                    _buffer = _buffer[..^1];
                }
            }
            else if (TryMapTriggerCharacter(virtualKey, out var character))
            {
                if (character == '/')
                {
                    _buffer = "/";
                    _bufferWindow = foregroundWindow;
                }
                else if (_buffer.StartsWith('/') && _buffer.Length < 64)
                {
                    _buffer += character;
                    var exact = FindExactMatch();
                    var hasLongerMatch = _snippets.Any(item =>
                        item.NormalizedTrigger.Length > _buffer.Length &&
                        item.NormalizedTrigger.StartsWith(
                            _buffer,
                            StringComparison.OrdinalIgnoreCase));

                    if (exact is not null && !hasLongerMatch)
                    {
                        snippetToExpand = exact.Snippet;
                        ClearBuffer();
                    }
                }
            }
            else if (_buffer.Length > 0)
            {
                ClearBuffer();
            }
        }

        if (snippetToExpand is not null)
        {
            ExpansionRequested?.Invoke(
                this,
                new SnippetExpansionRequestedEventArgs(snippetToExpand));
        }

        return suppressKey
            ? new IntPtr(1)
            : CallNextHookEx(_hookHandle, code, message, data);
    }

    private MonitoredSnippet? FindExactMatch()
    {
        return _snippets.FirstOrDefault(item =>
            item.NormalizedTrigger.Equals(_buffer, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ConfirmationName(int virtualKey)
    {
        return virtualKey switch
        {
            0x0D => "Enter",
            0x09 => "Tab",
            0x20 => "Space",
            _ => null
        };
    }

    private static bool TryMapTriggerCharacter(int virtualKey, out char character)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            character = char.ToLowerInvariant((char)virtualKey);
            return true;
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            character = (char)virtualKey;
            return true;
        }

        switch (virtualKey)
        {
            case 0xBF:
            case 0x6F:
                character = '/';
                return true;
            case 0xBD:
                character = IsKeyDown(0x10) ? '_' : '-';
                return true;
            default:
                character = default;
                return false;
        }
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetKeyState(virtualKey) & 0x8000) != 0;
    }

    private static bool IsOwnWindowInForeground()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(window, out var processId);
        return processId == Environment.ProcessId;
    }

    private void ClearBuffer()
    {
        _buffer = string.Empty;
        _bufferWindow = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_hookHandle != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private sealed record MonitoredSnippet(
        Snippet Snippet,
        string NormalizedTrigger,
        HashSet<string> ConfirmKeys);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        LowLevelKeyboardProc callback,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hook,
        int code,
        IntPtr message,
        IntPtr data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}

public sealed class SnippetExpansionRequestedEventArgs(Snippet snippet) : EventArgs
{
    public Snippet Snippet { get; } = snippet;
}
