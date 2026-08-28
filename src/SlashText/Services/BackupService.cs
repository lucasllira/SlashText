using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SlashText.Services;

public sealed record BackupManifestFile(string Path, long Size, string Sha256);
public sealed record BackupManifest(
    int SchemaVersion,
    string SlashDeskVersion,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<BackupManifestFile> Files);
public sealed record BackupValidationResult(bool IsLegacy, int FileCount, long UncompressedBytes);
public sealed record BackupRestoreResult(bool LegacyWithoutAssets, string SafetyBackupPath);

public sealed partial class BackupService
{
    private const int RetainedBackupFiles = 7;
    private const int MaximumEntries = 10_000;
    private const long MaximumUncompressedBytes = 2L * 1024 * 1024 * 1024;
    private const string ManifestName = "backup-manifest.json";
    private static readonly HashSet<string> RestorableFiles = new(
        ["snippets.md", "settings.json", "usage.json", "capture-history.json"],
        StringComparer.OrdinalIgnoreCase);
    private readonly string _backupDirectory;
    private readonly string _dataDirectory;
    private readonly string _assetsDirectory;
    private readonly IReadOnlyList<string> _sources;
    private readonly bool _includeAssets;

    public BackupService(
        string? backupDirectory = null,
        IReadOnlyList<string>? sources = null,
        string? dataDirectory = null)
    {
        _dataDirectory = dataDirectory ?? AppPaths.DataDirectory;
        _backupDirectory = backupDirectory ?? Path.Combine(_dataDirectory, "Backups");
        _assetsDirectory = Path.Combine(_dataDirectory, "assets");
        _includeAssets = sources is null;
        _sources = sources ?? RestorableFiles.Select(name => Path.Combine(_dataDirectory, name)).ToArray();
    }

    public void CreateDailySnapshot()
    {
        CreateSnapshot($"SlashDesk-backup-{DateTime.Now:yyyyMMdd}.zip", skipWhenExisting: true);
        Prune();
    }

    public string CreateManualSnapshot()
    {
        var result = CreateSnapshot(
            $"SlashDesk-backup-manual-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip",
            skipWhenExisting: false);
        Prune();
        return result;
    }

    public IReadOnlyList<FileInfo> ListSnapshots() =>
        Directory.Exists(_backupDirectory)
            ? new DirectoryInfo(_backupDirectory).GetFiles("SlashDesk-backup-*.zip")
                .OrderByDescending(item => item.LastWriteTimeUtc).ToList()
            : [];

    public BackupRestoreResult RestoreSnapshot(string archivePath)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException("O backup selecionado não foi encontrado.", archivePath);
        }

        var parent = Path.GetDirectoryName(_dataDirectory)
            ?? throw new InvalidOperationException("A pasta de dados não possui diretório pai.");
        var staging = Path.Combine(parent, $".slashdesk-restore-{Guid.NewGuid():N}");
        var rollback = Path.Combine(parent, $".slashdesk-rollback-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            var validation = ValidateSnapshot(archivePath, allowLegacy: true);
            ExtractValidated(archivePath, staging);
            ValidateExtracted(staging, validation.IsLegacy);

            var safetyBackup = CreateSnapshot(
                $"SlashDesk-backup-before-restore-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip",
                skipWhenExisting: false);
            var lockPaths = _sources.Append(_assetsDirectory).ToArray();
            using var lease = FileOperationCoordinator.AcquireAsync(lockPaths)
                .GetAwaiter().GetResult();
            CopyActiveState(rollback);
            try
            {
                ApplyStaged(staging, replaceAssets: Directory.Exists(Path.Combine(staging, "assets")));
            }
            catch
            {
                RestoreRollback(rollback);
                AppDiagnosticLog.Write("backup.restore-rollback", ("result", "restored"));
                throw;
            }
            Prune();
            AppDiagnosticLog.Write(
                "backup.restore-complete",
                ("legacy", validation.IsLegacy),
                ("files", validation.FileCount));
            return new BackupRestoreResult(
                validation.IsLegacy && !Directory.Exists(Path.Combine(staging, "assets")),
                safetyBackup);
        }
        finally
        {
            TryDeleteDirectory(staging);
            TryDeleteDirectory(rollback);
        }
    }

    private string CreateSnapshot(string fileName, bool skipWhenExisting)
    {
        Directory.CreateDirectory(_backupDirectory);
        var destination = Path.Combine(_backupDirectory, fileName);
        if (skipWhenExisting && File.Exists(destination)) return destination;

        var lockPaths = _sources.Append(_assetsDirectory).ToArray();
        using var lease = FileOperationCoordinator.AcquireAsync(lockPaths).GetAwaiter().GetResult();
        var entries = SnapshotEntries();
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Ainda não há dados do SlashDesk para incluir no backup.");
        }

        var temporary = Path.Combine(_backupDirectory, $".{fileName}-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                foreach (var item in entries)
                {
                    var entry = archive.CreateEntry(item.RelativePath, CompressionLevel.Optimal);
                    using var output = entry.Open();
                    output.Write(item.Bytes);
                }
                var manifest = new BackupManifest(
                    2,
                    Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown",
                    DateTimeOffset.UtcNow,
                    entries.Select(item => new BackupManifestFile(
                        item.RelativePath,
                        item.Bytes.LongLength,
                        Sha256(item.Bytes))).ToArray());
                var manifestEntry = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
                using var writer = new StreamWriter(manifestEntry.Open());
                writer.Write(JsonSerializer.Serialize(manifest, JsonOptions));
            }
            ValidateSnapshot(temporary, allowLegacy: false);
            AtomicFile.Replace(temporary, destination);
            AppDiagnosticLog.Write("backup.created", ("fileName", fileName), ("entries", entries.Count));
            return destination;
        }
        finally
        {
            AtomicFile.TryDelete(temporary);
        }
    }

    private List<SnapshotEntry> SnapshotEntries()
    {
        var result = new List<SnapshotEntry>();
        foreach (var source in _sources.Where(File.Exists))
        {
            result.Add(ReadStable(source, Path.GetFileName(source)));
        }
        if (_includeAssets && Directory.Exists(_assetsDirectory))
        {
            foreach (var asset in Directory.EnumerateFiles(
                         _assetsDirectory, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(_assetsDirectory, asset).Replace('\\', '/');
                if (!IsSafeRelative(relative))
                {
                    throw new InvalidDataException("A pasta assets contém um caminho inseguro.");
                }
                result.Add(ReadStable(asset, $"assets/{relative}"));
            }
        }
        return result.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static SnapshotEntry ReadStable(string path, string relative)
    {
        var before = new FileInfo(path);
        var length = before.Length;
        var timestamp = before.LastWriteTimeUtc;
        var bytes = File.ReadAllBytes(path);
        var after = new FileInfo(path);
        if (length != after.Length || timestamp != after.LastWriteTimeUtc || bytes.LongLength != length)
        {
            throw new IOException($"{Path.GetFileName(path)} mudou durante a criação do backup.");
        }
        return new SnapshotEntry(relative.Replace('\\', '/'), bytes);
    }

    public static BackupValidationResult ValidateSnapshot(string path, bool allowLegacy = true)
    {
        using var archive = ZipFile.OpenRead(path);
        var entries = ValidateEntries(archive);
        var manifestEntry = archive.GetEntry(ManifestName);
        if (manifestEntry is null)
        {
            if (!allowLegacy) throw new InvalidDataException("O backup não contém manifesto.");
            return new BackupValidationResult(true, entries.Count, entries.Sum(item => item.Length));
        }
        using var manifestDocument = JsonDocument.Parse(manifestEntry.Open());
        var schemaVersion = manifestDocument.RootElement.GetProperty("schemaVersion").GetInt32();
        if (schemaVersion == 1)
        {
            return new BackupValidationResult(true, entries.Count, entries.Sum(item => item.Length));
        }
        if (schemaVersion != 2)
        {
            throw new InvalidDataException("Versão de backup não suportada.");
        }
        var manifest = JsonSerializer.Deserialize<BackupManifest>(
            manifestDocument.RootElement.GetRawText(), JsonOptions)
            ?? throw new InvalidDataException("Manifesto de backup inválido.");
        var indexed = entries.ToDictionary(item => NormalizeEntry(item.FullName), StringComparer.OrdinalIgnoreCase);
        foreach (var item in manifest.Files)
        {
            var normalized = NormalizeEntry(item.Path);
            if (!indexed.TryGetValue(normalized, out var entry) || entry.Length != item.Size)
            {
                throw new InvalidDataException($"Entrada ausente ou com tamanho inválido: {normalized}.");
            }
            using var stream = entry.Open();
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if (!hash.Equals(item.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Hash inválido no backup: {normalized}.");
            }
        }
        if (manifest.Files.Count != entries.Count)
        {
            throw new InvalidDataException("O manifesto não corresponde a todas as entradas do backup.");
        }
        return new BackupValidationResult(false, entries.Count, entries.Sum(item => item.Length));
    }

    private static List<ZipArchiveEntry> ValidateEntries(ZipArchive archive)
    {
        if (archive.Entries.Count > MaximumEntries)
            throw new InvalidDataException("O backup excede o limite de entradas.");
        var result = new List<ZipArchiveEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var normalized = NormalizeEntry(entry.FullName);
            if (normalized.Equals(ManifestName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!RestorableFiles.Contains(normalized) &&
                !normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Entrada desconhecida no backup: {normalized}.");
            if (!seen.Add(normalized))
                throw new InvalidDataException($"Entrada duplicada no backup: {normalized}.");
            total = checked(total + entry.Length);
            if (total > MaximumUncompressedBytes)
                throw new InvalidDataException("O backup excede o limite descompactado.");
            result.Add(entry);
        }
        if (!result.Any(item => RestorableFiles.Contains(NormalizeEntry(item.FullName))))
            throw new InvalidDataException("O backup não contém dados restauráveis.");
        return result;
    }

    private static void ExtractValidated(string archivePath, string staging)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in ValidateEntries(archive))
        {
            var relative = NormalizeEntry(entry.FullName).Replace('/', Path.DirectorySeparatorChar);
            var destination = SafeDestination(staging, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: false);
        }
    }

    private static void ValidateExtracted(string staging, bool legacy)
    {
        foreach (var name in RestorableFiles.Where(name => name.EndsWith(".json", StringComparison.Ordinal)))
        {
            var path = Path.Combine(staging, name);
            if (!File.Exists(path)) continue;
            using var _ = JsonDocument.Parse(File.ReadAllBytes(path));
        }
        var snippets = Path.Combine(staging, "snippets.md");
        if (File.Exists(snippets))
        {
            new SnippetMarkdownRepository(snippets, Path.Combine(staging, "Backups"))
                .ValidateFileAsync(snippets).GetAwaiter().GetResult();
            if (!legacy)
            {
                var markdown = File.ReadAllText(snippets);
                foreach (Match match in AssetReferencePattern().Matches(markdown))
                {
                    var relative = match.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar);
                    if (!File.Exists(SafeDestination(staging, relative)))
                        throw new InvalidDataException($"Asset referenciado ausente: {Path.GetFileName(relative)}.");
                }
            }
        }
    }

    private void ApplyStaged(string staging, bool replaceAssets)
    {
        Directory.CreateDirectory(_dataDirectory);
        foreach (var name in RestorableFiles)
        {
            var source = Path.Combine(staging, name);
            if (!File.Exists(source)) continue;
            var destination = Path.Combine(_dataDirectory, name);
            var temporary = destination + $".{Guid.NewGuid():N}.restore";
            File.Copy(source, temporary, overwrite: false);
            AtomicFile.Replace(temporary, destination);
        }
        if (replaceAssets)
        {
            var sourceAssets = Path.Combine(staging, "assets");
            var replacement = Path.Combine(_dataDirectory, $".assets-restore-{Guid.NewGuid():N}");
            CopyDirectory(sourceAssets, replacement);
            if (Directory.Exists(_assetsDirectory)) Directory.Delete(_assetsDirectory, recursive: true);
            Directory.Move(replacement, _assetsDirectory);
        }
    }

    private void CopyActiveState(string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var source in _sources.Where(File.Exists))
            File.Copy(source, Path.Combine(destination, Path.GetFileName(source)), overwrite: false);
        if (Directory.Exists(_assetsDirectory)) CopyDirectory(_assetsDirectory, Path.Combine(destination, "assets"));
    }

    private void RestoreRollback(string rollback)
    {
        foreach (var name in RestorableFiles)
        {
            var source = Path.Combine(rollback, name);
            var destination = Path.Combine(_dataDirectory, name);
            if (File.Exists(source))
                File.Copy(source, destination, overwrite: true);
            else if (File.Exists(destination))
                File.Delete(destination);
        }
        var assets = Path.Combine(rollback, "assets");
        if (Directory.Exists(assets))
        {
            if (Directory.Exists(_assetsDirectory)) Directory.Delete(_assetsDirectory, recursive: true);
            CopyDirectory(assets, _assetsDirectory);
        }
        else if (Directory.Exists(_assetsDirectory))
        {
            Directory.Delete(_assetsDirectory, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = SafeDestination(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static string NormalizeEntry(string value)
    {
        var normalized = value.Replace('\\', '/').Trim('/');
        if (!IsSafeRelative(normalized)) throw new InvalidDataException("O backup contém um caminho inseguro.");
        return normalized;
    }

    private static bool IsSafeRelative(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !Path.IsPathRooted(value) &&
        !value.Split('/', '\\').Any(segment => segment is ".." or "." or "");

    private static string SafeDestination(string root, string relative)
    {
        var destination = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("O backup contém um caminho inseguro.");
        return destination;
    }

    private void Prune()
    {
        if (!Directory.Exists(_backupDirectory)) return;
        foreach (var backup in new DirectoryInfo(_backupDirectory).GetFiles("SlashDesk-backup-*.zip")
                     .OrderByDescending(item => item.LastWriteTimeUtc).Skip(RetainedBackupFiles))
            backup.Delete();
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record SnapshotEntry(string RelativePath, byte[] Bytes);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    [GeneratedRegex(@"!\[[^\]]*\]\((?<path>assets/[^\s\)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex AssetReferencePattern();
}
