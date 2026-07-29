using System.IO;
using System.IO.Compression;

namespace SlashText.Services;

public sealed class BackupService
{
    private const int RetentionDays = 7;
    private static readonly HashSet<string> RestorableFiles =
    [
        "snippets.md",
        "settings.json",
        "usage.json",
        "capture-history.json"
    ];
    private readonly string _backupDirectory;
    private readonly IReadOnlyList<string> _sources;

    public BackupService(
        string? backupDirectory = null,
        IReadOnlyList<string>? sources = null)
    {
        _backupDirectory = backupDirectory ?? AppPaths.BackupsDirectory;
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
                .Where(entry => RestorableFiles.Contains(entry.Name))
                .ToList();
            if (entries.Count == 0)
            {
                throw new InvalidDataException("O ZIP não contém dados reconhecidos do SlashDesk.");
            }

            Directory.CreateDirectory(AppPaths.DataDirectory);
            foreach (var entry in entries)
            {
                var destination = Path.Combine(AppPaths.DataDirectory, entry.Name);
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
        }
        return destination;
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
