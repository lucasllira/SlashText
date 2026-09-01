namespace SlashText.Services;

public enum BufferResetReason
{
    None,
    WindowChanged,
    FocusChanged,
    MouseClick,
    Escape,
    Navigation,
    ExpansionStarted,
    ExpansionFinished,
    SuggestionCancelled
}

public sealed class KeyboardBufferState
{
    public string Text { get; private set; } = string.Empty;
    public IntPtr TargetWindow { get; private set; }
    public IntPtr FocusWindow { get; private set; }
    public BufferResetReason LastResetReason { get; private set; }

    public bool HasValue => Text.Length > 0;

    public void Append(char character, IntPtr targetWindow, IntPtr focusWindow)
    {
        if (TriggerRule.IsSupportedPrefix(character))
        {
            Text = character.ToString();
            TargetWindow = targetWindow;
            FocusWindow = focusWindow;
            LastResetReason = BufferResetReason.None;
            return;
        }

        if (HasValue && Text.Length < TriggerRule.MaximumLength &&
            TriggerRule.IsSupportedCharacter(character))
        {
            Text += char.ToLowerInvariant(character);
        }
    }

    public void Backspace()
    {
        if (Text.Length > 0)
        {
            Text = Text[..^1];
        }
        if (Text.Length == 0)
        {
            TargetWindow = IntPtr.Zero;
            FocusWindow = IntPtr.Zero;
        }
    }

    public bool TargetChanged(IntPtr targetWindow, IntPtr focusWindow) =>
        HasValue && (targetWindow != TargetWindow ||
                     (FocusWindow != IntPtr.Zero && focusWindow != IntPtr.Zero && focusWindow != FocusWindow));

    public void Clear(BufferResetReason reason)
    {
        Text = string.Empty;
        TargetWindow = IntPtr.Zero;
        FocusWindow = IntPtr.Zero;
        LastResetReason = reason;
    }
}
