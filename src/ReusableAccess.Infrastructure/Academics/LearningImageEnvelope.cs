using System.Buffers.Binary;
using System.IO.Compression;

namespace ReusableAccess.Infrastructure.Academics;

// Bounded container validation. Only PNG is decompressed here; the remaining formats are not decoded on the server.
internal static class LearningImageEnvelope
{
    internal static bool Valid(string path, string extension)
    {
        try
        {
            using var file = File.OpenRead(path);
            if (file.Length < 20) return false;
            if (extension is ".jpg" or ".jpeg")
            {
                file.Position = file.Length - 2;
                return file.ReadByte() == 255 && file.ReadByte() == 217 && CoverDimensions.Valid(path, extension);
            }
            if (extension == ".png") return Png(file);
            Span<byte> bytes = stackalloc byte[30];
            var n = file.ReadAtLeast(bytes, 30, false);
            if (extension == ".gif")
            {
                file.Position = file.Length - 1;
                return file.ReadByte() == 0x3b && Size(BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]), BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]));
            }
            if (extension != ".webp" || n < 30 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) + 8L != file.Length) return false;
            if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]) + 20L > file.Length) return false;
            if (bytes[12..16].SequenceEqual("VP8X"u8)) return Size(1 + U24(bytes[24..]), 1 + U24(bytes[27..]));
            if (bytes[12..16].SequenceEqual("VP8L"u8) && bytes[20] == 0x2f)
            {
                var packed = BinaryPrimitives.ReadUInt32LittleEndian(bytes[21..]);
                return Size(1 + (packed & 0x3fff), 1 + (packed >> 14 & 0x3fff));
            }
            return bytes[12..16].SequenceEqual("VP8 "u8) && bytes[23] == 0x9d && bytes[24] == 1 && bytes[25] == 0x2a &&
                Size((uint)(BinaryPrimitives.ReadUInt16LittleEndian(bytes[26..]) & 0x3fff), (uint)(BinaryPrimitives.ReadUInt16LittleEndian(bytes[28..]) & 0x3fff));
        }
        catch (Exception e) when (e is IOException or InvalidDataException or OverflowException) { return false; }
    }
    private static uint U24(ReadOnlySpan<byte> b) => (uint)(b[0] | b[1] << 8 | b[2] << 16);
    private static bool Size(uint w, uint h) => w is > 0 and <= 4096 && h is > 0 and <= 4096 && (long)w * h <= 12_000_000;

    private static bool Png(Stream file)
    {
        file.Position = 8;
        Span<byte> chunk = stackalloc byte[8];
        Span<byte> ihdr = stackalloc byte[13];
        file.ReadExactly(chunk);
        if (BinaryPrimitives.ReadUInt32BigEndian(chunk) != 13 || !chunk[4..].SequenceEqual("IHDR"u8)) return false;
        file.ReadExactly(ihdr); file.Seek(4, SeekOrigin.Current);
        var w = BinaryPrimitives.ReadUInt32BigEndian(ihdr); var h = BinaryPrimitives.ReadUInt32BigEndian(ihdr[4..]);
        if (!Size(w, h) || ihdr[10] != 0 || ihdr[11] != 0 || ihdr[12] > 1) return false;
        var samples = ihdr[9] switch { 0 or 3 => 1, 2 => 3, 4 => 2, 6 => 4, _ => 0 };
        if (samples == 0 || ihdr[8] is not (1 or 2 or 4 or 8 or 16) ||
            (ihdr[9] is 2 or 4 or 6) && ihdr[8] < 8 || ihdr[9] == 3 && ihdr[8] == 16) return false;
        using var compressed = new MemoryStream();
        var buffer = new byte[8192];
        var ended = false; var palette = false;
        for (var k = 0; k < 10000 && file.Position + 12 <= file.Length; k++)
        {
            file.ReadExactly(chunk);
            var count = (long)BinaryPrimitives.ReadUInt32BigEndian(chunk);
            if (count + 4 > file.Length - file.Position) return false;
            if (chunk[4..].SequenceEqual("IEND"u8)) { ended = count == 0 && file.Position + 4 == file.Length; break; }
            if (chunk[4..].SequenceEqual("PLTE"u8)) palette = count is >= 3 and <= 768 && count % 3 == 0;
            if (chunk[4..].SequenceEqual("IDAT"u8))
            {
                while (count > 0)
                {
                    var size = (int)Math.Min(count, buffer.Length);
                    file.ReadExactly(buffer.AsSpan(0, size)); compressed.Write(buffer, 0, size); count -= size;
                }
                file.Seek(4, SeekOrigin.Current);
            }
            else file.Seek(count + 4, SeekOrigin.Current);
        }
        if (!ended || compressed.Length == 0 || ihdr[9] == 3 && !palette) return false;
        compressed.Position = 0;
        using var pixels = new ZLibStream(compressed, CompressionMode.Decompress);
        int[] startX = [0, 4, 0, 2, 0, 1, 0], startY = [0, 0, 4, 0, 2, 0, 1], stepX = [8, 8, 4, 4, 2, 2, 1], stepY = [8, 8, 8, 4, 4, 2, 2];
        var interlaced = ihdr[12] == 1;
        for (var pass = 0; pass < (interlaced ? 7 : 1); pass++)
        {
            int pw = interlaced ? ((int)w - startX[pass] + stepX[pass] - 1) / stepX[pass] : (int)w;
            int ph = interlaced ? ((int)h - startY[pass] + stepY[pass] - 1) / stepY[pass] : (int)h;
            if (pw <= 0 || ph <= 0) continue;
            var length = (pw * samples * ihdr[8] + 7) / 8;
            for (var row = 0; row < ph; row++)
            {
                if (pixels.ReadByte() is < 0 or > 4) return false;
                int remaining = length;
                while (remaining > 0)
                {
                    var take = Math.Min(remaining, buffer.Length);
                    pixels.ReadExactly(buffer.AsSpan(0, take)); remaining -= take;
                }
            }
        }
        return pixels.ReadByte() == -1;
    }
}
