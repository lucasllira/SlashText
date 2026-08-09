using System.IO;
using SlashText.Models;

namespace SlashText.Services;

internal static class CapturePathResolver
{
    public static string? CreatePortableRelativePath(
        string? filePath,
        AppDataEnvironment environment)
    {
        if (!environment.IsPortable || string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }
        var full = Path.GetFullPath(filePath);
        var root = environment.ExecutableDirectory.TrimEnd(Path.DirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return Path.GetRelativePath(environment.ExecutableDirectory, full);
    }

    public static string Resolve(CaptureRecord record, AppDataEnvironment environment)
    {
        if (!environment.IsPortable || string.IsNullOrWhiteSpace(record.PortableRelativePath))
        {
            return record.FilePath;
        }
        var candidate = Path.GetFullPath(Path.Combine(
            environment.ExecutableDirectory,
            record.PortableRelativePath));
        var root = environment.ExecutableDirectory.TrimEnd(Path.DirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : record.FilePath;
    }
}
