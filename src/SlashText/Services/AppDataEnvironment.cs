using System.IO;
using System.Reflection;

namespace SlashText.Services;

public enum DistributionMode
{
    Portable,
    Installed
}

public sealed class AppDataEnvironment
{
    internal const string DistributionMetadataKey = "SlashDeskDistribution";

    public AppDataEnvironment(
        DistributionMode mode,
        string executableDirectory,
        string localAppDataDirectory)
    {
        Mode = mode;
        ExecutableDirectory = Path.GetFullPath(executableDirectory);
        LocalAppDataDirectory = Path.GetFullPath(localAppDataDirectory);
        DataDirectory = mode == DistributionMode.Portable
            ? Path.Combine(ExecutableDirectory, "SlashDeskData")
            : Path.Combine(LocalAppDataDirectory, "SlashDesk");
    }

    public DistributionMode Mode { get; }
    public string ExecutableDirectory { get; }
    public string LocalAppDataDirectory { get; }
    public string DataDirectory { get; }
    public string LegacyInstalledDataDirectory => Path.Combine(LocalAppDataDirectory, "SlashDesk");
    public bool IsPortable => Mode == DistributionMode.Portable;

    public static AppDataEnvironment Detect()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var declared = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(item => item.Key.Equals(
                DistributionMetadataKey,
                StringComparison.OrdinalIgnoreCase))?.Value;
        var mode = Enum.TryParse<DistributionMode>(declared, ignoreCase: true, out var parsed)
            ? parsed
            : DistributionMode.Installed;
        var processPath = Environment.ProcessPath;
        var executableDirectory = string.IsNullOrWhiteSpace(processPath)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        return new AppDataEnvironment(
            mode,
            executableDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    }

    public bool TryProbePortableWrite(out string? error)
    {
        error = null;
        if (!IsPortable)
        {
            return true;
        }

        var probe = Path.Combine(ExecutableDirectory, $".slashdesk-write-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                       probe,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       1,
                       FileOptions.WriteThrough))
            {
                stream.WriteByte(0x53);
                stream.Flush(flushToDisk: true);
            }
            File.Delete(probe);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = exception.Message;
            TryDelete(probe);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
