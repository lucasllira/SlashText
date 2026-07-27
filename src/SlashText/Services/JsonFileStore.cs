using System.IO;
using System.Text.Json;

namespace SlashText.Services;

public sealed class JsonFileStore<T> where T : new()
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;

    public JsonFileStore(string path)
    {
        _path = path;
    }

    public async Task<T> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new T();
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken)
                ?? new T();
        }
        catch (JsonException)
        {
            return new T();
        }
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path) ?? AppPaths.BaseDirectory;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(_path)}-{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, value, Options, cancellationToken);
            }

            File.Move(temporary, _path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
