using System.IO;
using System.Text.Json;
using SlashText.Models;

namespace SlashText.Services;

public sealed class UsageService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly JsonFileStore<UsageSnapshot> _store;
    private readonly string _usageFile;
    private readonly List<UsageRecord> _records = [];
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private QuickAccentUsageRecord _quickAccent = new();

    public IReadOnlyList<UsageRecord> Records => _records;
    public QuickAccentUsageRecord QuickAccent => _quickAccent;
    public JsonLoadResult<UsageSnapshot>? LastLoadResult => _store.LastLoadResult;

    public UsageService(string? usageFile = null)
    {
        _usageFile = usageFile ?? AppPaths.UsageFile;
        _store = new JsonFileStore<UsageSnapshot>(_usageFile);
    }

    public async Task LoadAsync()
    {
        _records.Clear();
        _quickAccent = new QuickAccentUsageRecord();
        try
        {
            if (!File.Exists(_usageFile)) return;
            var json = await File.ReadAllTextAsync(_usageFile);
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                _records.AddRange(
                    JsonSerializer.Deserialize<List<UsageRecord>>(json, ReadOptions) ?? []);
                return;
            }

            var snapshot = await _store.LoadAsync();
            if (snapshot is null)
            {
                return;
            }

            _records.AddRange(snapshot.Snippets ?? []);
            _quickAccent = snapshot.QuickAccent ?? new QuickAccentUsageRecord();
            _quickAccent.Characters ??= new Dictionary<string, long>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            _ = await _store.LoadDetailedAsync();
        }
        catch (IOException exception)
        {
            AppDiagnosticLog.WriteException("usage.read-failed", exception);
        }
    }

    public async Task RecordAsync(Snippet snippet, int insertedCharacters)
    {
        await _writeLock.WaitAsync();
        try
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
            await SaveAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RecordQuickAccentAsync(char character)
    {
        await _writeLock.WaitAsync();
        try
        {
            _quickAccent.Count++;
            _quickAccent.LastUsedAt = DateTimeOffset.Now;
            var key = character.ToString();
            _quickAccent.Characters[key] =
                _quickAccent.Characters.GetValueOrDefault(key) + 1;
            await SaveAsync();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public UsageRecord? For(Guid snippetId) =>
        _records.FirstOrDefault(item => item.SnippetId == snippetId);

    public IReadOnlyDictionary<char, long> QuickAccentCharacterCounts() =>
        _quickAccent.Characters
            .Where(item => item.Key.Length == 1)
            .ToDictionary(item => item.Key[0], item => item.Value);

    private Task SaveAsync() =>
        _store.SaveAsync(new UsageSnapshot
        {
            Snippets = _records.ToList(),
            QuickAccent = new QuickAccentUsageRecord
            {
                Count = _quickAccent.Count,
                LastUsedAt = _quickAccent.LastUsedAt,
                Characters = new Dictionary<string, long>(_quickAccent.Characters)
            }
        });
}
