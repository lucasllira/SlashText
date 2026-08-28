using System.Collections.Concurrent;
using System.IO;

namespace SlashText.Services;

public static class FileOperationCoordinator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates =
        new(StringComparer.OrdinalIgnoreCase);

    public static async Task<IDisposable> AcquireAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        var normalized = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var acquired = new List<SemaphoreSlim>(normalized.Length);
        try
        {
            foreach (var path in normalized)
            {
                var gate = Gates.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired.Add(gate);
            }
            return new Lease(acquired);
        }
        catch
        {
            Release(acquired);
            throw;
        }
    }

    public static Task<IDisposable> AcquireAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        AcquireAsync([path], cancellationToken);

    private static void Release(IReadOnlyList<SemaphoreSlim> gates)
    {
        for (var index = gates.Count - 1; index >= 0; index--)
        {
            gates[index].Release();
        }
    }

    private sealed class Lease(IReadOnlyList<SemaphoreSlim> gates) : IDisposable
    {
        private IReadOnlyList<SemaphoreSlim>? _gates = gates;

        public void Dispose()
        {
            var value = Interlocked.Exchange(ref _gates, null);
            if (value is not null)
            {
                Release(value);
            }
        }
    }
}

public static class AtomicFile
{
    public static async Task WriteAsync(
        string path,
        Func<Stream, Task> write,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("O arquivo não possui diretório pai.");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await write(stream).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            Replace(temporary, path);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public static void Replace(string temporary, string destination)
    {
        if (File.Exists(destination))
        {
            try
            {
                File.Replace(temporary, destination, null, ignoreMetadataErrors: true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
            }
            catch (IOException)
            {
            }
        }
        File.Move(temporary, destination, overwrite: true);
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
