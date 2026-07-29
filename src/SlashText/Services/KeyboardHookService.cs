using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using SlashText.Models;
using Point = System.Windows.Point;

namespace SlashText.Services;

public sealed class KeyboardHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkhfInjected = 0x00000010;
    // Windows 10 1607+: consulta a tradução sem alterar o estado interno das
    // teclas mortas. Sem esta flag, acentos de layouts ABNT podem ser aplicados
    // uma segunda vez quando o evento chega ao aplicativo de destino.
    private const uint ToUnicodeNoStateChange = 0x04;

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
    public event EventHandler<SnippetSuggestionsEventArgs>? SuggestionsChanged;

    public bool IsRunning => _hookHandle != IntPtr.Zero;

    public void UpdateSnippets(IEnumerable<Snippet> snippets)
    {
        lock (_sync)
        {
            _snippets = snippets
                .Where(item => item.Enabled)
                .Select(item => new MonitoredSnippet(
                    item,
                    item.Trigger.ToLowerInvariant(),
                    new HashSet<string>(item.ConfirmKeys, StringComparer.OrdinalIgnoreCase)))
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
        _hookHandle = SetWindowsHookEx(
            WhKeyboardLl,
            _hookCallback,
            GetModuleHandle(module?.ModuleName),
            0);

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
        IReadOnlyList<Snippet>? suggestions = null;
        Point suggestionPoint = default;
        var suppressKey = false;
        var targetWindow = GetForegroundWindow();

        lock (_sync)
        {
            if (_buffer.Length > 0 && targetWindow != _bufferWindow)
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
                suggestions = [];
            }
            else if (virtualKey == 0x1B)
            {
                ClearBuffer();
                suggestions = [];
            }
            else if (virtualKey == 0x08)
            {
                if (_buffer.Length > 0)
                {
                    _buffer = _buffer[..^1];
                    suggestions = GetSuggestions();
                    suggestionPoint = CaretLocator.GetScreenPosition();
                }
            }
            else if (IsModifierKey(virtualKey))
            {
                // Shift/AltGr podem fazer parte de "/" ou ":" em layouts diferentes.
            }
            else if (TryMapTriggerCharacter(virtualKey, out var character))
            {
                if (character is '/' or ':')
                {
                    _buffer = character.ToString();
                    _bufferWindow = targetWindow;
                }
                else if (HasSupportedPrefix(_buffer) && _buffer.Length < 64)
                {
                    _buffer += character;
                }

                if (HasSupportedPrefix(_buffer))
                {
                    var exact = FindExactMatch();
                    var hasLongerMatch = _snippets.Any(item =>
                        item.NormalizedTrigger.Length > _buffer.Length &&
                        item.NormalizedTrigger.StartsWith(_buffer, StringComparison.OrdinalIgnoreCase));

                    suggestions = GetSuggestions();
                    suggestionPoint = CaretLocator.GetScreenPosition();

                    if (exact is not null && !hasLongerMatch)
                    {
                        snippetToExpand = exact.Snippet;
                        ClearBuffer();
                        suggestions = [];
                    }
                }
            }
            else if (_buffer.Length > 0)
            {
                ClearBuffer();
                suggestions = [];
            }
        }

        if (suggestions is not null)
        {
            SuggestionsChanged?.Invoke(
                this,
                new SnippetSuggestionsEventArgs(suggestions, suggestionPoint));
        }

        if (snippetToExpand is not null)
        {
            ExpansionRequested?.Invoke(
                this,
                new SnippetExpansionRequestedEventArgs(snippetToExpand, targetWindow));
        }

        return suppressKey ? new IntPtr(1) : CallNextHookEx(_hookHandle, code, message, data);
    }

    private MonitoredSnippet? FindExactMatch() =>
        _snippets.FirstOrDefault(item =>
            item.NormalizedTrigger.Equals(_buffer, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<Snippet> GetSuggestions() =>
        _snippets
            .Where(item => item.NormalizedTrigger.StartsWith(_buffer, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.NormalizedTrigger.Length)
            .Take(6)
            .Select(item => item.Snippet)
            .ToList();

    private static string? ConfirmationName(int virtualKey) =>
        virtualKey switch
        {
            0x0D => "Enter",
            0x09 => "Tab",
            0x20 => "Space",
            _ => null
        };

    internal static bool TryMapTriggerCharacter(int virtualKey, out char character)
    {
        if (virtualKey == 0x6F)
        {
            character = '/';
            return true;
        }

        var keyboardState = new byte[256];
        if (!GetKeyboardState(keyboardState))
        {
            character = default;
            return false;
        }

        keyboardState[virtualKey] |= 0x80;
        var foreground = GetForegroundWindow();
        var threadId = foreground == IntPtr.Zero
            ? 0
            : GetWindowThreadProcessId(foreground, out _);
        var layout = GetKeyboardLayout(threadId);
        var buffer = new StringBuilder(8);
        var scanCode = MapVirtualKeyEx((uint)virtualKey, 0, layout);
        var count = ToUnicodeEx(
            (uint)virtualKey,
            scanCode,
            keyboardState,
            buffer,
            buffer.Capacity,
            ToUnicodeNoStateChange,
            layout);

        if (count <= 0 || buffer.Length == 0)
        {
            character = default;
            return false;
        }

        character = char.ToLowerInvariant(buffer[0]);
        return char.IsAsciiLetterOrDigit(character) ||
               character is '/' or ':' or '-' or '_';
    }

    private static bool HasSupportedPrefix(string value) =>
        value.StartsWith('/') || value.StartsWith(':');

    private static bool IsModifierKey(int virtualKey) =>
        virtualKey is 0x10 or 0x11 or 0x12 or
            0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;

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
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] keyState);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(
        uint code,
        uint mapType,
        IntPtr keyboardLayout);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ToUnicodeEx(
        uint virtualKey,
        uint scanCode,
        byte[] keyState,
        [Out] StringBuilder buffer,
        int bufferSize,
        uint flags,
        IntPtr keyboardLayout);
}

public sealed class SnippetExpansionRequestedEventArgs(Snippet snippet, IntPtr targetWindow) : EventArgs
{
    public Snippet Snippet { get; } = snippet;
    public IntPtr TargetWindow { get; } = targetWindow;
}

public sealed class SnippetSuggestionsEventArgs(
    IReadOnlyList<Snippet> snippets,
    Point screenPosition) : EventArgs
{
    public IReadOnlyList<Snippet> Snippets { get; } = snippets;
    public Point ScreenPosition { get; } = screenPosition;
}
