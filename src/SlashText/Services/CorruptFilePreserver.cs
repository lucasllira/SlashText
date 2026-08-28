using System.IO;
using System.Security.Cryptography;

namespace SlashText.Services;

public static class CorruptFilePreserver
{
    public static string Preserve(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("O arquivo não possui diretório pai.");
        var stem = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var suffix = hash[..12];
        var existing = Directory.EnumerateFiles(directory, $"{stem}.corrupted-*-{suffix}{extension}")
            .FirstOrDefault();
        if (existing is not null) return existing;
        var preserved = Path.Combine(
            directory,
            $"{stem}.corrupted-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{suffix}{extension}");
        File.Copy(path, preserved, overwrite: false);
        return preserved;
    }
}
