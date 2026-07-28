namespace SlashText.Models;

public sealed class UsageRecord
{
    public Guid SnippetId { get; set; }
    public long Count { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public long CharactersSaved { get; set; }
}

public sealed class UsageSnapshot
{
    public List<UsageRecord> Snippets { get; set; } = [];
    public QuickAccentUsageRecord QuickAccent { get; set; } = new();
}

public sealed class QuickAccentUsageRecord
{
    public long Count { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public Dictionary<string, long> Characters { get; set; } =
        new(StringComparer.Ordinal);
}
