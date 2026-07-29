using System.Windows.Controls;
using System.Windows.Input;
using SlashText.Services;

namespace SlashText.Views;

public sealed class ShortcutRecorderBox : TextBox
{
    private const string ListeningText = "Pressione uma combinação...";
    private string _committedValue = string.Empty;

    public ShortcutRecorderBox()
    {
        IsReadOnly = true;
        Cursor = Cursors.Hand;
        MinHeight = 42;
        VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
        ToolTip = "Clique e pressione a tecla, roda ou botão do mouse desejado";
        GotKeyboardFocus += (_, _) =>
        {
            _committedValue = Text;
            Text = ListeningText;
            SelectAll();
        };
        LostKeyboardFocus += (_, _) =>
        {
            if (Text == ListeningText)
            {
                Text = _committedValue;
            }
        };
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            Text = _committedValue;
            Keyboard.ClearFocus();
            return;
        }
        if (key is Key.Back or Key.Delete)
        {
            Commit(string.Empty);
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin)
        {
            return;
        }

        var shortcut = GlobalCaptureShortcutService.FormatKeyboardShortcut(
            key,
            Keyboard.Modifiers);
        if (!string.IsNullOrWhiteSpace(shortcut))
        {
            Commit(shortcut);
        }
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        var shortcut = GlobalCaptureShortcutService.FormatWheelShortcut(
            e.Delta,
            Keyboard.Modifiers);
        if (!string.IsNullOrWhiteSpace(shortcut))
        {
            Commit(shortcut);
        }
        else
        {
            Text = "A roda exige Ctrl, Alt, Shift ou Win";
        }
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is MouseButton.Left or MouseButton.Right)
        {
            return;
        }

        e.Handled = true;
        var shortcut = GlobalCaptureShortcutService.FormatMouseShortcut(
            e.ChangedButton,
            Keyboard.Modifiers);
        if (!string.IsNullOrWhiteSpace(shortcut))
        {
            Commit(shortcut);
        }
    }

    private void Commit(string value)
    {
        _committedValue = value;
        Text = value;
        CaretIndex = Text.Length;
        Keyboard.ClearFocus();
    }
}
