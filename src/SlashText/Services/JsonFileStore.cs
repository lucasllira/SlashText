using System.IO;
using System.Text.Json;

namespace SlashText.Services;

public enum JsonLoadStatus
{
    Loaded,
    Missing,
    InvalidJson,
    ReadError,
    AccessDenied,
    Locked
}

public sealed record JsonLoadResult<T>(
    T Value,
    JsonLoadStatus Status,
    string? PreservedPath = null,
    Exception? Error = null);

public sealed class JsonFileStore<T> where T : new()
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;
    private volatile bool _writeBlockedByCorruption;

    public JsonFileStore(string path)
    {
        _path = path;
    }

    public JsonLoadResult<T>? LastLoadResult { get; private set; }

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default) =>
        (await LoadDetailedAsync(cancellationToken).ConfigureAwait(false)).Value;

    public async Task<JsonLoadResult<T>> LoadDetailedAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return SetResult(new JsonLoadResult<T>(new T(), JsonLoadStatus.Missing));
        }

        try
        {
            await using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var value = await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken)
                .ConfigureAwait(false) ?? new T();
            _writeBlockedByCorruption = false;
            return SetResult(new JsonLoadResult<T>(value, JsonLoadStatus.Loaded));
        }
        catch (JsonException exception)
        {
            var preserved = CorruptFilePreserver.Preserve(_path);
            _writeBlockedByCorruption = true;
            AppDiagnosticLog.Write(
                "store.corruption-preserved",
                ("fileName", Path.GetFileName(_path)),
                ("preservedName", Path.GetFileName(preserved)),
                ("exceptionType", exception.GetType().Name));
            return SetResult(new JsonLoadResult<T>(
                new T(), JsonLoadStatus.InvalidJson, preserved, exception));
        }
        catch (UnauthorizedAccessException exception)
        {
            return SetResult(new JsonLoadResult<T>(
                new T(), JsonLoadStatus.AccessDenied, Error: exception));
        }
        catch (IOException exception)
        {
            var code = exception.HResult & 0xFFFF;
            var status = code is 32 or 33 ? JsonLoadStatus.Locked : JsonLoadStatus.ReadError;
            return SetResult(new JsonLoadResult<T>(new T(), status, Error: exception));
        }
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        if (_writeBlockedByCorruption)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(_path)} está inválido e foi preservado. " +
                "Restaure um backup antes de gravar novos dados.");
        }
        using var lease = await FileOperationCoordinator.AcquireAsync(_path, cancellationToken)
            .ConfigureAwait(false);
        await AtomicFile.WriteAsync(
            _path,
            stream => JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken).AsTask(),
            cancellationToken).ConfigureAwait(false);
    }

    public void AllowRecoveryWrite() => _writeBlockedByCorruption = false;

    private JsonLoadResult<T> SetResult(JsonLoadResult<T> result)
    {
        LastLoadResult = result;
        return result;
    }
}
