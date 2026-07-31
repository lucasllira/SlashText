namespace SlashText.Models;

public sealed class CaptureRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset CreatedAt { get; set; }
    public string Type { get; set; } = string.Empty;
    public string MediaKind { get; set; } = "image";
    public string FilePath { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public double DurationSeconds { get; set; }
}
