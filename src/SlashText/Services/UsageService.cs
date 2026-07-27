using SlashText.Models;

namespace SlashText.Services;

public sealed class UsageService
{
    private readonly JsonFileStore<List<UsageRecord>> _store = new(AppPaths.UsageFile);
    private readonly List<UsageRecord> _records = [];

    public IReadOnlyList<UsageRecord> Records => _records;

    public async Task LoadAsync()
    {
        _records.Clear();
        _records.AddRange(await _store.LoadAsync());
    }

    public async Task RecordAsync(Snippet snippet, int insertedCharacters)
    {
        var record = _records.FirstOrDefault(item => item.SnippetId == snippet.Id);
        if (record is null)
        {
            record = new UsageRecord { SnippetId = snippet.Id };
            _records.Add(record);
        }

        record.Count++;
        record.LastUsedAt = DateTimeOffset.Now;
        record.CharactersSaved += Math.Max(0, insertedCharacters - snippet.Trigger.Length);
        await _store.SaveAsync(_records);
    }

    public UsageRecord? For(Guid snippetId) =>
        _records.FirstOrDefault(item => item.SnippetId == snippetId);
}
