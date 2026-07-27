using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using SlashText.Models;

namespace SlashText.Services;

public sealed class TextExpansionService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VkBack = 0x08;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;

    private readonly TemplateEngine _templateEngine = new();

    public async Task ExpandAsync(Snippet snippet)
    {
        // Permite que a última tecla digitada chegue ao aplicativo de destino.
        await Task.Delay(35);

        var text = _templateEngine.Render(snippet.Content);
        IDataObject? previousClipboard = null;

        try
        {
            previousClipboard = TryGetClipboard();
            SetClipboardText(text);
            SendBackspaces(snippet.Trigger.Length);
            await Task.Delay(20);
            SendPaste();

            // A maioria dos aplicativos lê o clipboard durante o Ctrl+V.
            await Task.Delay(300);
        }
        finally
        {
            if (previousClipboard is not null)
            {
                TryRestoreClipboard(previousClipboard);
            }
        }
    }

    private static IDataObject? TryGetClipboard()
    {
        try
        {
            return Clipboard.GetDataObject();
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static void SetClipboardText(string text)
    {
        ExternalException? lastException = null;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (ExternalException exception)
            {
                lastException = exception;
                Thread.Sleep(20);
            }
        }

        throw new InvalidOperationException(
            "A área de transferência está ocupada. Tente novamente.",
            lastException);
    }

    private static void TryRestoreClipboard(IDataObject data)
    {
        try
        {
            Clipboard.SetDataObject(data, true);
        }
        catch (ExternalException)
        {
            // A expansão já foi concluída; não interrompe o usuário se outro
            // aplicativo assumir a área de transferência nesse intervalo.
        }
    }

    private static void SendBackspaces(int count)
    {
        var inputs = new Input[count * 2];
        for (var index = 0; index < count; index++)
        {
            inputs[index * 2] = KeyboardInput(VkBack, keyUp: false);
            inputs[(index * 2) + 1] = KeyboardInput(VkBack, keyUp: true);
        }

        Send(inputs);
    }

    private static void SendPaste()
    {
        Send(
        [
            KeyboardInput(VkControl, keyUp: false),
            KeyboardInput(VkV, keyUp: false),
            KeyboardInput(VkV, keyUp: true),
            KeyboardInput(VkControl, keyUp: true)
        ]);
    }

    private static Input KeyboardInput(ushort key, bool keyUp)
    {
        return new Input
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
    }

    private static void Send(Input[] inputs)
    {
        var sent = SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<Input>());

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
        [FieldOffset(0)]
        public MouseInputData Mouse;

        [FieldOffset(0)]
        public KeyboardInputData Keyboard;

        [FieldOffset(0)]
        public HardwareInputData Hardware;
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
    private static extern uint SendInput(
        uint inputCount,
        Input[] inputs,
        int inputSize);

    [DllImport("user32.dll")]
    private static extern UIntPtr GetMessageExtraInfo();
}
