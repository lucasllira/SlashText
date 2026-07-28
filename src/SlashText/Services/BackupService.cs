using System.IO;
using System.IO.Compression;

namespace SlashText.Services;

public sealed class BackupService
{
    private const int RetentionDays = 7;
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
            AppPaths.UsageFile
        ];
    }

    public void CreateDailySnapshot()
    {
        var sources = _sources.Where(File.Exists).ToList();

        if (sources.Count == 0)
        {
            return;
        }

        Directory.CreateDirectory(_backupDirectory);
        var destination = Path.Combine(
            _backupDirectory,
            $"SlashText-backup-{DateTime.Now:yyyyMMdd}.zip");
        if (!File.Exists(destination))
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

        foreach (var backup in new DirectoryInfo(_backupDirectory)
                     .GetFiles("SlashText-backup-*.zip")
                     .OrderByDescending(item => item.Name)
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
