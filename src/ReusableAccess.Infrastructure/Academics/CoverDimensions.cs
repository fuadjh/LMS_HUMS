using System.Buffers.Binary;
using ReusableAccess.Contracts;

namespace ReusableAccess.Infrastructure.Academics;

internal static class CoverDimensions
{
    // Inspect only the dimensions, without decoding untrusted compressed data in the API.
    // This is not a substitute for AV/content scanning. The normal signature and byte limits still apply.
    internal static bool Valid(string path, string extension)
    {
        using var file = File.OpenRead(path);
        return extension switch
        {
            ".png" => Png(file),
            ".jpg" or ".jpeg" => Jpeg(file),
            _ => false
        };
    }

    private static bool Png(Stream file)
    {
        Span<byte> header = stackalloc byte[24];
        if (file.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) != header.Length ||
            BinaryPrimitives.ReadUInt32BigEndian(header[8..]) != 13 || !header[12..16].SequenceEqual("IHDR"u8)) return false;
        return Allowed(BinaryPrimitives.ReadUInt32BigEndian(header[16..]), BinaryPrimitives.ReadUInt32BigEndian(header[20..]));
    }

    private static bool Jpeg(Stream file)
    {
        file.Position = 2; // the shared signature check already verified the SOI marker
        Span<byte> len = stackalloc byte[2];
        Span<byte> dims = stackalloc byte[5];
        for (var segments = 0; segments < 256; segments++)
        {
            if (file.ReadByte() != 0xff) return false;
            int mark;
            do { mark = file.ReadByte(); } while (mark == 0xff);
            if (mark is <= 0 or 0xda or 0xd9) return false;
            if (mark == 0x01 || mark is >= 0xd0 and <= 0xd7) continue;
            if (file.ReadAtLeast(len, 2, throwOnEndOfStream: false) != 2) return false;
            var size = BinaryPrimitives.ReadUInt16BigEndian(len);
            if (size < 2 || file.Position + size - 2 > file.Length) return false;
            if (mark is 0xc0 or 0xc1 or 0xc2)
            {
                return size >= 8 && file.ReadAtLeast(dims, 5, throwOnEndOfStream: false) == 5 &&
                    Allowed(BinaryPrimitives.ReadUInt16BigEndian(dims[1..]), BinaryPrimitives.ReadUInt16BigEndian(dims[3..]));
            }
            file.Seek(size - 2, SeekOrigin.Current);
        }
        return false;
    }

    private static bool Allowed(uint width, uint height) => width is > 0 and <= UploadLimits.CoverMaxAxis &&
                                                            height is > 0 and <= UploadLimits.CoverMaxAxis;
}
