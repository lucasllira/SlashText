using System.Buffers.Binary;
using System.IO;

namespace SlashText.Services;

/// <summary>Apply timing to WPF's generated GIF without loading it all into memory.</summary>
internal static class GifTimingMetadata
{
    internal static void Write(string path, IReadOnlyList<int> delays)
    {
        var staging = path + "." + Guid.NewGuid().ToString("N") + ".timing";
        var created = false;
        try
        {
            using (var source = File.OpenRead(path))
            using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                Rewrite(source, output, delays);
                output.Flush(flushToDisk: true);
            }
            File.Move(staging, path, overwrite: true);
        }
        finally { if (created && File.Exists(staging)) File.Delete(staging); }
    }

    private static void Rewrite(Stream source, Stream output, IReadOnlyList<int> delays)
    {
        var buffer = new byte[81920]; // Bounded even for large files/long holds.
        Span<byte> header = stackalloc byte[13];
        if (source.Length < header.Length) throw Invalid();
        source.ReadExactly(header);
        if (!header[..6].SequenceEqual("GIF87a"u8) && !header[..6].SequenceEqual("GIF89a"u8)) throw Invalid();
        var prefixLength = 13 + ColorTableBytes(header[10]);
        Ensure(prefixLength - source.Position);
        output.Write("GIF89a"u8); // Animation extensions require this version.
        CopyRange(6, prefixLength - 6);
        output.Write(new byte[] { 0x21, 0xFF, 11, (byte)'N', (byte)'E', (byte)'T', (byte)'S',
            (byte)'C', (byte)'A', (byte)'P', (byte)'E', (byte)'2', (byte)'.', (byte)'0', 3, 1, 0, 0, 0 });
        var frame = 0;
        Span<byte> control = stackalloc byte[] { 0x21, 0xF9, 4, 8, 0, 0, 0, 0 };
        Span<byte> descriptor = stackalloc byte[9];
        while (source.Position < source.Length)
        {
            var start = source.Position;
            var tag = source.ReadByte();
            if (tag == 0x3B)
            {
                if (frame != delays.Count || source.Position != source.Length) throw Invalid();
                output.WriteByte((byte)tag);
                return;
            }
            if (tag == 0x21)
            {
                Ensure(1);
                var label = source.ReadByte();
                var isLoop = false;
                if (label == 0xFF)
                {
                    Ensure(1);
                    var length = source.ReadByte();
                    Ensure(length);
                    source.ReadExactly(buffer.AsSpan(0, length));
                    isLoop = length == 11 && buffer.AsSpan(0, length).SequenceEqual("NETSCAPE2.0"u8);
                    if (length != 0) SkipSubBlocks();
                }
                else SkipSubBlocks();
                var end = source.Position;
                // Replace GCE/loop if a future encoder starts emitting them;
                // preserve all other extensions unchanged.
                if (label != 0xF9 && !isLoop) CopyRange(start, end - start);
                continue;
            }
            if (tag != 0x2C || frame >= delays.Count) throw Invalid();
            Ensure(descriptor.Length);
            source.ReadExactly(descriptor);
            var colors = ColorTableBytes(descriptor[8]);
            Ensure(colors + 1);
            source.Position += colors + 1; // Local palette and LZW minimum code size.
            SkipSubBlocks();
            var imageEnd = source.Position;
            var remaining = Math.Max(2, delays[frame++]);
            while (remaining > 0)
            {
                var delay = Math.Min(ushort.MaxValue, remaining);
                // Split long holds without silently clamping their duration or
                // leaving a final 10 ms image below the existing minimum.
                if (remaining - delay == 1) delay--;
                BinaryPrimitives.WriteUInt16LittleEndian(control.Slice(4, 2), (ushort)delay);
                output.Write(control);
                CopyRange(start, imageEnd - start);
                remaining -= delay;
            }
        }
        throw Invalid();

        void Ensure(long count)
        { if (count < 0 || count > source.Length - source.Position) throw Invalid(); }
        void SkipSubBlocks()
        {
            while (true)
            {
                Ensure(1);
                var length = source.ReadByte();
                Ensure(length);
                source.Position += length;
                if (length == 0) return;
            }
        }
        void CopyRange(long start, long count)
        {
            source.Position = start;
            Ensure(count);
            while (count > 0)
            {
                var size = (int)Math.Min(buffer.Length, count);
                source.ReadExactly(buffer.AsSpan(0, size));
                output.Write(buffer.AsSpan(0, size));
                count -= size;
            }
        }
    }

    private static int ColorTableBytes(byte packed) => (packed & 0x80) == 0 ? 0 : 3 * (1 << ((packed & 7) + 1));
    private static InvalidDataException Invalid() => new("Invalid generated GIF timing structure.");
}
