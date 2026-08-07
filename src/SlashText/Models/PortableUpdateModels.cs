namespace SlashText.Models;

public sealed record UpdateProgress(
    string Stage,
    long BytesReceived,
    long? TotalBytes,
    bool IsApplying = false);

internal sealed class PortableUpdateManifest
{
    public string OperationId { get; set; } = Guid.NewGuid().ToString("N");
    public int MainProcessId { get; set; }
    public string ExpectedVersion { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public string TargetExecutable { get; set; } = string.Empty;
    public string StagedExecutable { get; set; } = string.Empty;
    public string BackupExecutable { get; set; } = string.Empty;
    public string FailedExecutable { get; set; } = string.Empty;
    public string HelperExecutable { get; set; } = string.Empty;
    public string ConfirmationFile { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
}

internal sealed record PreparedPortableUpdate(
    PortableUpdateManifest Manifest,
    string ManifestPath);
