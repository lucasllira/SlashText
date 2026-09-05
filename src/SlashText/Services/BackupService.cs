using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SlashText.Services;

public sealed class BackupService
{
    private const int RetentionDays = 7;
    private const int CurrentSchemaVersion = 2;
    private const string ManifestName = "backup-manifest.json";
    private static readonly HashSet<string> RestorableFiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "snippets.md",
            "settings.json",
            "usage.json",
            "capture-history.json"
        };
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _backupDirectory;
    private readonly IReadOnlyList<string> _sources;
    private readonly bool _includeAssets;
    private readonly string _assetsDirectory;

    public BackupService(
        string? backupDirectory = null,
        IReadOnlyList<string>? sources = null,
        string? assetsDirectory = null)
    {
        _backupDirectory = backupDirectory ?? AppPaths.BackupsDirectory;
        _includeAssets = sources is null || assetsDirectory is not null;
        _assetsDirectory = assetsDirectory ?? AppPaths.AssetsDirectory;
        _sources = sources ??
        [
            AppPaths.SnippetsFile,
            AppPaths.SettingsFile,
            AppPaths.UsageFile,
            AppPaths.CaptureHistoryFile
        ];
    }

    public void CreateDailySnapshot()
    {
        CreateSnapshot(
            $"SlashDesk-backup-{DateTime.Now:yyyyMMdd}.zip",
            skipWhenExisting: true);
        Prune();
    }

    public string CreateManualSnapshot()
    {
        var name = $"SlashDesk-backup-manual-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip";
        var path = CreateSnapshot(name, skipWhenExisting: false);
        Prune();
        return path;
    }

    public IReadOnlyList<FileInfo> ListSnapshots() =>
        Directory.Exists(_backupDirectory)
            ? new DirectoryInfo(_backupDirectory)
                .GetFiles("SlashDesk-backup-*.zip")
                .OrderByDescending(item => item.LastWriteTimeUtc)
                .ToList()
            : [];

    public void RestoreSnapshot(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("O backup selecionado não foi encontrado.", archivePath);
        }

        CreateSnapshot(
            $"SlashDesk-backup-before-restore-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip",
            skipWhenExisting: false);
        using (var archive = ZipFile.OpenRead(archivePath))
        {
            var entries = archive.Entries
                .Where(entry => RestorableFiles.Contains(entry.Name) ||
                                entry.FullName.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ||
                                entry.FullName.StartsWith("assets\\", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (entries.Count == 0)
            {
                throw new InvalidDataException("O ZIP não contém dados reconhecidos do SlashDesk.");
            }

            Directory.CreateDirectory(AppPaths.DataDirectory);
            foreach (var entry in entries)
            {
                var relative = RestorableFiles.Contains(entry.Name)
                    ? entry.Name
                    : entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                var destination = Path.GetFullPath(Path.Combine(AppPaths.DataDirectory, relative));
                var dataRoot = Path.GetFullPath(AppPaths.DataDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("O backup contém um caminho inseguro.");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }
        }
        Prune();
    }

    private string CreateSnapshot(string fileName, bool skipWhenExisting)
    {
        var sources = CollectSources();
        if (sources.Count == 0)
        {
            throw new InvalidOperationException("Ainda não há dados do SlashDesk para incluir no backup.");
        }

        var duplicate = sources
            .GroupBy(item => item.ArchivePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException(
                $"Mais de um arquivo produziria a entrada '{duplicate.Key}' no backup.");
        }

        Directory.CreateDirectory(_backupDirectory);
        var destination = Path.Combine(_backupDirectory, fileName);
        if (skipWhenExisting && File.Exists(destination))
        {
            ValidateSnapshot(destination);
            return destination;
        }

        var temporary = Path.Combine(
            _backupDirectory,
            $".{fileName}-{Guid.NewGuid():N}.tmp");
        try
        {
            CreateArchive(temporary, sources);
            ValidateSnapshot(temporary);
            File.Move(temporary, destination, overwrite: false);
            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private List<BackupSource> CollectSources()
    {
        var sources = _sources
            .Where(File.Exists)
            .Select(path => new BackupSource(path, Path.GetFileName(path)))
            .ToList();

        if (!_includeAssets || !Directory.Exists(_assetsDirectory))
        {
            return sources;
        }

        var assetsRoot = Path.GetFullPath(_assetsDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootPrefix = assetsRoot + Path.DirectorySeparatorChar;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };
        foreach (var asset in Directory.EnumerateFiles(
                     assetsRoot,
                     "*",
                     options))
        {
            var fullAsset = Path.GetFullPath(asset);
            if (!fullAsset.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Um asset está fora da pasta permitida.");
            }

            var relative = Path.GetRelativePath(assetsRoot, fullAsset)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            var archivePath = $"assets/{relative}";
            EnsureAllowedArchivePath(archivePath);
            sources.Add(new BackupSource(fullAsset, archivePath));
        }

        return sources;
    }

    private static void CreateArchive(
        string path,
        IReadOnlyList<BackupSource> sources)
    {
        var files = new List<BackupManifestFile>();
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var source in sources.OrderBy(
                         item => item.ArchivePath,
                         StringComparer.OrdinalIgnoreCase))
            {
                EnsureAllowedArchivePath(source.ArchivePath);
                var entry = archive.CreateEntry(
                    source.ArchivePath,
                    CompressionLevel.Optimal);
                using var input = new FileStream(
                    source.SourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    FileOptions.SequentialScan);
                using var output = entry.Open();
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                long size = 0;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    hash.AppendData(buffer, 0, read);
                    size += read;
                }

                files.Add(new BackupManifestFile(
                    source.ArchivePath,
                    size,
                    Convert.ToHexString(hash.GetHashAndReset())
                        .ToLowerInvariant()));
            }

            var manifest = archive.CreateEntry(
                ManifestName,
                CompressionLevel.Optimal);
            using var writer = new StreamWriter(manifest.Open());
            writer.Write(JsonSerializer.Serialize(
                new BackupManifest(
                    CurrentSchemaVersion,
                    DateTimeOffset.UtcNow,
                    Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
                        ?? "unknown",
                    AppPaths.Mode.ToString(),
                    files),
                ManifestOptions));
        }
    }

    internal static void ValidateSnapshot(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var manifestEntries = archive.Entries
            .Where(entry => NormalizeArchivePath(entry.FullName)
                .Equals(ManifestName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (manifestEntries.Count != 1)
        {
            throw new InvalidDataException(
                "O backup deve conter exatamente um manifesto.");
        }

        using var document = JsonDocument.Parse(manifestEntries[0].Open());
        if (!document.RootElement.TryGetProperty("schemaVersion", out var schemaElement) ||
            !schemaElement.TryGetInt32(out var schemaVersion))
        {
            throw new InvalidDataException("O manifesto não informa uma versão válida.");
        }

        switch (schemaVersion)
        {
            case 1:
                ValidateLegacySnapshot(archive);
                break;
            case CurrentSchemaVersion:
                ValidateCurrentSnapshot(archive, document.RootElement);
                break;
            default:
                throw new InvalidDataException("Versão de backup não suportada.");
        }
    }

    private static void ValidateLegacySnapshot(ZipArchive archive)
    {
        if (!archive.Entries.Any(entry =>
                RestorableFiles.Contains(entry.Name)))
        {
            throw new InvalidDataException(
                "O backup legado não contém dados restauráveis.");
        }
    }

    private static void ValidateCurrentSnapshot(
        ZipArchive archive,
        JsonElement manifest)
    {
        if (!manifest.TryGetProperty("files", out var filesElement) ||
            filesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("O manifesto não contém a lista de arquivos.");
        }

        var archiveFiles = new Dictionary<string, ZipArchiveEntry>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries.Where(entry =>
                     !string.IsNullOrEmpty(entry.Name) &&
                     !NormalizeArchivePath(entry.FullName)
                         .Equals(ManifestName, StringComparison.OrdinalIgnoreCase)))
        {
            var normalized = NormalizeArchivePath(entry.FullName);
            EnsureAllowedArchivePath(normalized);
            if (!archiveFiles.TryAdd(normalized, entry))
            {
                throw new InvalidDataException(
                    $"O backup contém a entrada duplicada '{normalized}'.");
            }
        }

        var manifestFiles = new Dictionary<string, BackupManifestFile>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var element in filesElement.EnumerateArray())
        {
            var file = element.Deserialize<BackupManifestFile>(ManifestOptions)
                ?? throw new InvalidDataException(
                    "O manifesto contém uma entrada inválida.");
            EnsureAllowedArchivePath(file.Path);
            if (file.Size < 0 ||
                file.Sha256.Length != 64 ||
                !file.Sha256.All(Uri.IsHexDigit) ||
                !manifestFiles.TryAdd(file.Path, file))
            {
                throw new InvalidDataException(
                    $"O manifesto contém dados inválidos para '{file.Path}'.");
            }
        }

        if (archiveFiles.Count == 0 ||
            archiveFiles.Count != manifestFiles.Count ||
            archiveFiles.Keys.Any(path => !manifestFiles.ContainsKey(path)))
        {
            throw new InvalidDataException(
                "O conteúdo do ZIP não corresponde ao manifesto.");
        }

        foreach (var item in archiveFiles)
        {
            var expected = manifestFiles[item.Key];
            if (item.Value.Length != expected.Size)
            {
                throw new InvalidDataException(
                    $"O tamanho de '{item.Key}' não corresponde ao manifesto.");
            }

            using var stream = item.Value.Open();
            var actualHash = Convert.ToHexString(SHA256.HashData(stream))
                .ToLowerInvariant();
            if (!actualHash.Equals(
                    expected.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"O hash de '{item.Key}' não corresponde ao manifesto.");
            }
        }
    }

    private static void EnsureAllowedArchivePath(string path)
    {
        var normalized = NormalizeArchivePath(path);
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.StartsWith('/') ||
            normalized.Contains(':') ||
            normalized.Split('/').Any(part =>
                string.IsNullOrWhiteSpace(part) || part is "." or ".."))
        {
            throw new InvalidDataException(
                $"O backup contém o caminho inseguro '{path}'.");
        }

        if (RestorableFiles.Contains(normalized))
        {
            return;
        }

        if (!normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Length <= "assets/".Length)
        {
            throw new InvalidDataException(
                $"O backup contém o arquivo não permitido '{path}'.");
        }
    }

    private static string NormalizeArchivePath(string path) =>
        path.Replace('\\', '/');

    private void Prune()
    {
        foreach (var backup in new DirectoryInfo(_backupDirectory)
                     .GetFiles("*-backup-*.zip")
                     .OrderByDescending(item => item.LastWriteTimeUtc)
                     .Skip(RetentionDays))
        {
            backup.Delete();
        }

        foreach (var legacy in Directory.EnumerateFiles(
                     _backupDirectory,
                     "snippets-*.md"))
        {
            File.Delete(legacy);
        }
    }

    private sealed record BackupSource(
        string SourcePath,
        string ArchivePath);

    private sealed record BackupManifest(
        int SchemaVersion,
        DateTimeOffset CreatedAtUtc,
        string SlashDeskVersion,
        string DistributionMode,
        IReadOnlyList<BackupManifestFile> Files);

    private sealed record BackupManifestFile(
        string Path,
        long Size,
        string Sha256);
}
