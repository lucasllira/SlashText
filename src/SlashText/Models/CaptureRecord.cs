namespace SlashText.Models;

public sealed class CaptureRecord
{
    public DateTimeOffset CreatedAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}
