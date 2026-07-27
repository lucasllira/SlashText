namespace SlashText.Models;

public sealed class UsageRecord
{
    public Guid SnippetId { get; set; }
    public long Count { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public long CharactersSaved { get; set; }
}
