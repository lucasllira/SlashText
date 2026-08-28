using System.IO;
using System.Text.RegularExpressions;

namespace SlashText.Services;

public sealed record OrphanAsset(string RelativePath, long Size);
public sealed record AssetAnalysisResult(
    IReadOnlyList<OrphanAsset> Orphans,
    long TotalBytes,
    int ReferencedCount);

public sealed partial class AssetMaintenanceService
{
    private readonly string _snippetsPath;
    private readonly string _assetsDirectory;

    public AssetMaintenanceService(string? snippetsPath = null, string? assetsDirectory = null)
    {
        _snippetsPath = snippetsPath ?? AppPaths.SnippetsFile;
        _assetsDirectory = assetsDirectory ?? AppPaths.AssetsDirectory;
    }

    public async Task<AssetAnalysisResult> AnalyzeAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_snippetsPath) || !Directory.Exists(_assetsDirectory))
            return new AssetAnalysisResult([], 0, 0);

        await new SnippetMarkdownRepository(_snippetsPath)
            .ValidateFileAsync(_snippetsPath, cancellationToken).ConfigureAwait(false);
        var markdown = await File.ReadAllTextAsync(_snippetsPath, cancellationToken)
            .ConfigureAwait(false);
        var referenced = AssetPattern().Matches(markdown).Cast<Match>()
            .Select(match => NormalizeReference(match.Groups["path"].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphans = new List<OrphanAsset>();
        foreach (var file in Directory.EnumerateFiles(_assetsDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(file);
            if (!string.IsNullOrEmpty(info.LinkTarget)) continue;
            var relative = Path.GetRelativePath(_assetsDirectory, file).Replace('\\', '/');
            if (!referenced.Contains(relative))
                orphans.Add(new OrphanAsset(relative, info.Length));
        }
        return new AssetAnalysisResult(
            orphans.OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            orphans.Sum(item => item.Size),
            referenced.Count);
    }

    public int DeleteOrphans(AssetAnalysisResult analysis)
    {
        var deleted = 0;
        foreach (var orphan in analysis.Orphans)
        {
            var path = SafeAssetPath(orphan.RelativePath);
            var info = new FileInfo(path);
            if (!info.Exists || !string.IsNullOrEmpty(info.LinkTarget)) continue;
            File.Delete(path);
            deleted++;
        }
        return deleted;
    }

    private string NormalizeReference(string value)
    {
        var relative = value.Replace('\\', '/');
        if (!relative.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Referência de asset fora da pasta permitida.");
        relative = relative["assets/".Length..];
        _ = SafeAssetPath(relative);
        return relative;
    }

    private string SafeAssetPath(string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(item => item is ".." or "." or ""))
            throw new InvalidDataException("Caminho de asset inseguro.");
        var root = Path.GetFullPath(_assetsDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(_assetsDirectory, relative));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Caminho de asset fora da pasta permitida.");
        return path;
    }

    [GeneratedRegex(@"!\[[^\]]*\]\((?<path>assets/[^\s\)]+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex AssetPattern();
}
