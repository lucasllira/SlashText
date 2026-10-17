using System.Diagnostics;

internal static partial class SmokeSuite
{
    private static bool HasChangedPixel(System.Drawing.Bitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y += 2)
        {
            for (var x = 0; x < bitmap.Width; x += 2)
            {
                if (bitmap.GetPixel(x, y).ToArgb() !=
                    System.Drawing.Color.White.ToArgb())
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void Require(bool condition, string scenario)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Falha no cenário: {scenario}");
        }
    }

    private static void RequireThrows<TException>(Action action, string scenario)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Falha no smoke test: {scenario}");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string scenario)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            if (timeout.Elapsed > TimeSpan.FromSeconds(2))
            {
                throw new InvalidOperationException($"Timeout no cenário: {scenario}");
            }
            await Task.Delay(10);
        }
    }

    private static async Task RequireThrowsAsync<TException>(Func<Task> action, string scenario)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Falha no smoke test: {scenario}");
    }

    private static Task WriteValidMp4Async(string path) => File.WriteAllBytesAsync(
        path,
        [
            0, 0, 0, 12, (byte)'f', (byte)'t', (byte)'y', (byte)'p', 0, 0, 0, 0,
            0, 0, 0, 12, (byte)'m', (byte)'d', (byte)'a', (byte)'t', 1, 2, 3, 4,
            0, 0, 0, 8, (byte)'m', (byte)'o', (byte)'o', (byte)'v'
        ]);
}
