using SlashText.Models;
using System.IO.Compression;

internal static partial class SmokeSuite
{
    private static string ReleaseJson(
        string version,
        bool includeStable = true,
        bool includeDraft = false,
        bool includePrerelease = false)
    {
        var releases = new List<string>();
        if (includeDraft)
        {
            releases.Add(ReleaseEntry(version, draft: true, prerelease: false));
        }
        if (includePrerelease)
        {
            releases.Add(ReleaseEntry(version + "-rc.1", draft: false, prerelease: true));
        }
        if (includeStable)
        {
            releases.Add(ReleaseEntry(version, draft: false, prerelease: false));
        }
        return $"[{string.Join(',', releases)}]";
    }

    private static string ReleaseEntry(string version, bool draft, bool prerelease) => $$"""
        {
          "tag_name": "v{{version}}",
          "name": "SlashDesk {{version}}",
          "body": "Notas {{version}}",
          "html_url": "https://github.com/lucasllira/SlashText/releases/tag/v{{version}}",
          "published_at": "2026-08-06T12:00:00Z",
          "draft": {{draft.ToString().ToLowerInvariant()}},
          "prerelease": {{prerelease.ToString().ToLowerInvariant()}},
          "assets": [
            {
              "name": "SlashDesk-{{version}}-portable-win-x64.zip",
              "browser_download_url": "https://example.invalid/SlashDesk-{{version}}-portable-win-x64.zip",
              "size": 12345
            },
            {
              "name": "SlashDesk-{{version}}-portable-win-x64.zip.sha256",
              "browser_download_url": "https://example.invalid/SlashDesk-{{version}}-portable-win-x64.zip.sha256",
              "size": 128
            }
          ]
        }
        """;

    private static byte[] CreatePortableZip(string executable)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("SlashDesk.exe", CompressionLevel.NoCompression);
            using var destination = entry.Open();
            using var source = File.OpenRead(executable);
            source.CopyTo(destination);
        }
        return output.ToArray();
    }

    private static ReleaseInfo CreatePackageRelease(
        string version,
        string packageName,
        long packageSize,
        long checksumSize) => new(
            version,
            $"SlashDesk {version}",
            "Notas",
            $"https://github.com/lucasllira/SlashText/releases/tag/v{version}",
            DateTimeOffset.UtcNow,
            new ReleaseAssetInfo(
                packageName,
                $"https://github.com/lucasllira/SlashText/releases/download/v{version}/{packageName}",
                packageSize),
            new ReleaseAssetInfo(
                packageName + ".sha256",
                $"https://github.com/lucasllira/SlashText/releases/download/v{version}/{packageName}.sha256",
                checksumSize));
}
