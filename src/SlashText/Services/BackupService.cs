using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

namespace SlashText.Services;

public sealed class BackupService
{
    private const int RetentionDays = 7;
    private const string ManifestName = "backup-manifest.json";
    private static readonly HashSet<string> RestorableFiles =
    [
        "snippets.md",
        "settings.json",
        "usage.json",
        "capture-history.json"
    ];
    private readonly string _backupDirectory;
    private readonly IReadOnlyList<string> _sources;
    private readonly bool _includeAssets;

    public BackupService(
        string? backupDirectory = null,
        IReadOnlyList<string>? sources = null)
    {
        _backupDirectory = backupDirectory ?? AppPaths.BackupsDirectory;
        _includeAssets = sources is null;
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
        var sources = _sources.Where(File.Exists).ToList();

        if (sources.Count == 0)
        {
            throw new InvalidOperationException("Ainda não há dados do SlashDesk para incluir no backup.");
        }

        Directory.CreateDirectory(_backupDirectory);
        var destination = Path.Combine(_backupDirectory, fileName);
        if (!skipWhenExisting || !File.Exists(destination))
        {
            using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
            foreach (var source in sources)
            {
                archive.CreateEntryFromFile(
                    source,
                    Path.GetFileName(source),
                    CompressionLevel.Optimal);
            }
            if (_includeAssets && Directory.Exists(AppPaths.AssetsDirectory))
            {
                foreach (var asset in Directory.EnumerateFiles(
                             AppPaths.AssetsDirectory,
                             "*",
                             SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(AppPaths.AssetsDirectory, asset);
                    archive.CreateEntryFromFile(
                        asset,
                        Path.Combine("assets", relative),
                        CompressionLevel.Optimal);
                }
            }
            var manifest = archive.CreateEntry(ManifestName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(manifest.Open());
            writer.Write(JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                createdAtUtc = DateTimeOffset.UtcNow,
                slashDeskVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown",
                distributionMode = AppPaths.Mode.ToString(),
                files = sources.Select(Path.GetFileName).Order().ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        ValidateSnapshot(destination);
        return destination;
    }

    internal static void ValidateSnapshot(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var manifest = archive.GetEntry(ManifestName)
            ?? throw new InvalidDataException("O backup não contém manifesto.");
        using (var document = JsonDocument.Parse(manifest.Open()))
        {
            if (document.RootElement.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException("Versão de backup não suportada.");
            }
        }
        if (!archive.Entries.Any(entry => RestorableFiles.Contains(entry.Name)))
        {
            throw new InvalidDataException("O backup não contém dados restauráveis.");
        }
    }

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
}
