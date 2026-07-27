using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using SlashText.Models;
using WpfClipboard = System.Windows.Clipboard;
using WpfDataFormats = System.Windows.DataFormats;
using WpfDataObject = System.Windows.DataObject;
using WpfIDataObject = System.Windows.IDataObject;

namespace SlashText.Services;

public sealed class TextExpansionService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VkBack = 0x08;
    private const ushort VkTab = 0x09;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;

    private readonly TemplateEngine _templateEngine = new();

    public IReadOnlyList<TemplateField> GetFillableFields(Snippet snippet) =>
        _templateEngine.GetFillableFields(snippet.Content);

    public async Task<int> ExpandAsync(
        Snippet snippet,
        IReadOnlyDictionary<string, string> values,
        IntPtr targetWindow)
    {
        var rendered = _templateEngine.Render(snippet.Content, values);
        var segments = rendered.Split(TemplateEngine.TabMarker, StringSplitOptions.None);
        var insertedCharacters = segments.Sum(segment =>
            snippet.Format == SnippetFormat.Markdown
                ? RichTextMarkdownConverter.ToPlainText(segment).Length
                : segment.Length);

        WpfIDataObject? previousClipboard = null;
        try
        {
            previousClipboard = TryGetClipboard();
            if (targetWindow != IntPtr.Zero)
            {
                _ = SetForegroundWindow(targetWindow);
                await Task.Delay(80);
            }

            SendBackspaces(snippet.Trigger.Length);
            await Task.Delay(25);

            for (var index = 0; index < segments.Length; index++)
            {
                SetClipboardSegment(segments[index], snippet.Format);
                SendPaste();
                await Task.Delay(150);

                if (index < segments.Length - 1)
                {
                    SendKey(VkTab);
                    await Task.Delay(100);
                }
            }

            await Task.Delay(250);
            return insertedCharacters;
        }
        finally
        {
            if (previousClipboard is not null)
            {
                TryRestoreClipboard(previousClipboard);
            }
        }
    }

    private static void SetClipboardSegment(string value, SnippetFormat format)
    {
        var plain = format == SnippetFormat.Markdown
            ? RichTextMarkdownConverter.ToPlainText(value)
            : value;
        var data = new WpfDataObject();
        data.SetData(WpfDataFormats.UnicodeText, plain);
        data.SetData(WpfDataFormats.Text, plain);

        if (format == SnippetFormat.Markdown)
        {
            var html = RichTextMarkdownConverter.ToHtml(value);
            data.SetData(WpfDataFormats.Html, HtmlClipboardFormatter.Create(html));
        }

        SetClipboard(data);
    }

    private static WpfIDataObject? TryGetClipboard()
    {
        try
        {
            return WpfClipboard.GetDataObject();
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static void SetClipboard(WpfIDataObject data)
    {
        ExternalException? lastException = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                WpfClipboard.SetDataObject(data, true);
                return;
            }
            catch (ExternalException exception)
            {
                lastException = exception;
                Thread.Sleep(25);
            }
        }

        throw new InvalidOperationException(
            "A área de transferência está ocupada. Tente novamente.",
            lastException);
    }

    private static void TryRestoreClipboard(WpfIDataObject data)
    {
        try
        {
            WpfClipboard.SetDataObject(data, true);
        }
        catch (ExternalException)
        {
            // A expansão terminou; outro aplicativo pode ter assumido o clipboard.
        }
    }

    private static void SendBackspaces(int count)
    {
        var inputs = new Input[count * 2];
        for (var index = 0; index < count; index++)
        {
            inputs[index * 2] = KeyboardInput(VkBack, false);
            inputs[(index * 2) + 1] = KeyboardInput(VkBack, true);
        }

        Send(inputs);
    }

    private static void SendPaste() =>
        Send(
        [
            KeyboardInput(VkControl, false),
            KeyboardInput(VkV, false),
            KeyboardInput(VkV, true),
            KeyboardInput(VkControl, true)
        ]);

    private static void SendKey(ushort key) =>
        Send([KeyboardInput(key, false), KeyboardInput(key, true)]);

    private static Input KeyboardInput(ushort key, bool keyUp) =>
        new()
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInputData
                {
                    VirtualKey = key,
                    Flags = keyUp ? KeyEventKeyUp : 0,
                    ExtraInfo = GetMessageExtraInfo()
                }
            }
        };

    private static void Send(Input[] inputs)
    {
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "O Windows não permitiu inserir o texto no aplicativo atual.");
        }
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
        [FieldOffset(0)] public MouseInputData Mouse;
        [FieldOffset(0)] public KeyboardInputData Keyboard;
        [FieldOffset(0)] public HardwareInputData Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInputData
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInputData
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInputData
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern UIntPtr GetMessageExtraInfo();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);
}
