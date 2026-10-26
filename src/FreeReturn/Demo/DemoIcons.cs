using System.Buffers.Binary;
using System.IO.Compression;

namespace FreeReturn.Demo;

/// <summary>Draws simple gradient app icons as PNG bytes for demo mode (no image assets needed).</summary>
internal static class DemoIcons
{
    private const int Size = 96;
    private const int Samples = 3;

    private static readonly (uint Top, uint Bottom)[] Palettes =
    [
        (0xFF7AB8, 0xE8457C), (0x5AC8FA, 0x1E7BEA), (0xFFCC4D, 0xFF8A00), (0xB38CFF, 0x6A3DE8),
        (0xFF8A65, 0xE53935), (0x4A4A4A, 0x1C1C1E), (0x34D399, 0x0E9F6E), (0x60A5FA, 0x4F46E5),
        (0x7DD3FC, 0x14B8A6), (0x86EFAC, 0x16A34A), (0xFDBA74, 0xF97316), (0x312E81, 0x0F172A),
        (0xF472B6, 0x9333EA), (0xFCD34D, 0xF59E0B),
    ];

    public static byte[] Create(int index)
    {
        var (top, bottom) = Palettes[index % Palettes.Length];
        var shape = index % 5;
        var pixels = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            var t = y / (double)(Size - 1);
            var r = Lerp(top >> 16 & 0xFF, bottom >> 16 & 0xFF, t);
            var g = Lerp(top >> 8 & 0xFF, bottom >> 8 & 0xFF, t);
            var b = Lerp(top & 0xFF, bottom & 0xFF, t);
            for (var x = 0; x < Size; x++)
            {
                var coverage = Coverage(shape, x, y);
                var o = (y * Size + x) * 4;
                pixels[o] = (byte)(r + (255 - r) * coverage);
                pixels[o + 1] = (byte)(g + (255 - g) * coverage);
                pixels[o + 2] = (byte)(b + (255 - b) * coverage);
                pixels[o + 3] = 255;
            }
        }

        return EncodePng(pixels);
    }

    private static double Lerp(uint a, uint b, double t) => a + (b - (double)a) * t;

    /// <summary>Supersampled coverage (0..1) of the white symbol at a pixel.</summary>
    private static double Coverage(int shape, int px, int py)
    {
        var hits = 0;
        for (var sy = 0; sy < Samples; sy++)
        for (var sx = 0; sx < Samples; sx++)
        {
            var x = (px + (sx + 0.5) / Samples) / Size - 0.5;
            var y = (py + (sy + 0.5) / Samples) / Size - 0.5;
            var d = Math.Sqrt(x * x + y * y);
            var inside = shape switch
            {
                0 => d < 0.22,
                1 => d is < 0.25 and > 0.15,
                2 => Math.Abs(x) + Math.Abs(y) < 0.26,
                3 => Math.Abs(x) < 0.2 && Math.Abs(y) < 0.2 && !(Math.Abs(x) < 0.08 && Math.Abs(y) < 0.08),
                _ => y > -0.2 && y < 0.18 && Math.Abs(x) < (y + 0.2) * 0.62,
            };
            if (inside)
                hits++;
        }

        return hits / (double)(Samples * Samples) * 0.95;
    }

    private static byte[] EncodePng(byte[] rgba)
    {
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < Size; y++)
            {
                z.WriteByte(0);
                z.Write(rgba, y * Size * 4, Size * 4);
            }
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, Size);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), Size);
        header[8] = 8;
        header[9] = 6;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", raw.ToArray());
        WriteChunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        s.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = Crc32(typeBytes, data);
        BinaryPrimitives.WriteUInt32BigEndian(length, crc);
        s.Write(length);
    }

    private static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in type.Concat(data))
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return ~crc;
    }
}
