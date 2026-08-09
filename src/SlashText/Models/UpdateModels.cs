namespace SlashText.Models;

public enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    Offline,
    NoRelease,
    InvalidRelease,
    Ignored,
    Deferred,
    Cached
}

public sealed record ReleaseAssetInfo(string Name, string DownloadUrl, long Size);

public sealed record ReleaseInfo(
    string Version,
    string Name,
    string Notes,
    string PageUrl,
    DateTimeOffset? PublishedAt,
    ReleaseAssetInfo PortableAsset,
    ReleaseAssetInfo ChecksumAsset);

public sealed class UpdateState
{
    public DateTimeOffset? LastCheckedUtc { get; set; }
    public string LastResult { get; set; } = "Ainda não verificado";
    public string? LastAvailableVersion { get; set; }
    public string? LastReleaseUrl { get; set; }
    public string? LastReleaseNotes { get; set; }
    public long? LastDownloadSize { get; set; }
    public string? LastPortableAssetName { get; set; }
    public string? LastPortableAssetUrl { get; set; }
    public string? LastChecksumAssetName { get; set; }
    public string? LastChecksumAssetUrl { get; set; }
    public string? IgnoredVersion { get; set; }
    public string? DeferredVersion { get; set; }
    public DateTimeOffset? DeferredUntilUtc { get; set; }
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    bool UpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string Message,
    string? Url,
    string? Notes,
    long? DownloadSize,
    ReleaseInfo? Release);
