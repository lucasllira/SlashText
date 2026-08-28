using System.IO;
using System.Text.Json;
using SlashText.Models;

namespace SlashText.Services;

internal sealed class CaptureHistoryStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    private readonly string _path;
    private volatile bool _writeBlockedByCorruption;

    public CaptureHistoryStore(string path)
    {
        _path = path;
    }

    public string? PreservedCorruptPath { get; private set; }

    public async Task<List<CaptureRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }
        try
        {
            await using var stream = File.OpenRead(_path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                PreservedCorruptPath = CorruptFilePreserver.Preserve(_path);
                _writeBlockedByCorruption = true;
                return [];
            }
            _writeBlockedByCorruption = false;
            PreservedCorruptPath = null;
            var records = new List<CaptureRecord>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                try
                {
                    var record = element.Deserialize<CaptureRecord>(Options);
                    if (record is not null)
                    {
                        records.Add(record);
                    }
                }
                catch (JsonException exception)
                {
                    AppDiagnosticLog.WriteException("history.item-corrupt", exception);
                }
            }
            return records;
        }
        catch (JsonException exception)
        {
            AppDiagnosticLog.WriteException("history.file-corrupt", exception);
            PreservedCorruptPath = CorruptFilePreserver.Preserve(_path);
            _writeBlockedByCorruption = true;
            return [];
        }
        catch (IOException exception)
        {
            AppDiagnosticLog.WriteException("history.file-read-failed", exception);
            return [];
        }
    }

    public async Task SaveAsync(
        IReadOnlyList<CaptureRecord> records,
        CancellationToken cancellationToken = default)
    {
        if (_writeBlockedByCorruption)
        {
            throw new InvalidOperationException(
                "capture-history.json está inválido e foi preservado; " +
                "o histórico em memória não substituirá o original.");
        }
        var snapshot = records.Select(Clone).ToArray();
        using var lease = await FileOperationCoordinator.AcquireAsync(_path, cancellationToken)
            .ConfigureAwait(false);
        await AtomicFile.WriteAsync(
            _path,
            stream => JsonSerializer.SerializeAsync(stream, snapshot, Options, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
    }

    public void AllowRecoveryWrite() => _writeBlockedByCorruption = false;

    private static CaptureRecord Clone(CaptureRecord item) => new()
    {
        Id = item.Id,
        CreatedAt = item.CreatedAt,
        Type = item.Type,
        MediaKind = item.MediaKind,
        FilePath = item.FilePath,
        PortableRelativePath = item.PortableRelativePath,
        Width = item.Width,
        Height = item.Height,
        DurationSeconds = item.DurationSeconds
    };
}
