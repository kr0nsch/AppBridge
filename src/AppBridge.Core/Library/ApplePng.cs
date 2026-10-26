using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace AppBridge.Core.Library;

/// <summary>
/// Converts the "CgBI" PNG variant that Xcode puts in app bundles (raw deflate, BGRA pixels)
/// into a standard PNG that Windows can decode. Standard PNGs pass through unchanged.
/// </summary>
public static class ApplePng
{
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <returns>A standard PNG, or null when the image uses a layout this converter does not handle.</returns>
    public static byte[]? ToStandard(byte[] png)
    {
        if (png.Length < Signature.Length || !png.AsSpan(0, Signature.Length).SequenceEqual(Signature))
            return null;

        var apple = false;
        byte[]? header = null;
        using var compressed = new MemoryStream();
        var offset = Signature.Length;
        while (offset + 12 <= png.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            if (length < 0 || offset + 12 + length > png.Length)
                return null;
            var data = png.AsSpan(offset + 8, length);
            switch (type)
            {
                case "CgBI":
                    apple = true;
                    break;
                case "IHDR":
                    header = data.ToArray();
                    break;
                case "IDAT":
                    compressed.Write(data);
                    break;
            }

            offset += 12 + length;
            if (type == "IEND")
                break;
        }

        if (!apple)
            return png;

        // Only 8-bit RGBA without interlacing, which is what Xcode writes for app icons.
        if (header is not { Length: 13 } || header[8] != 8 || header[9] != 6 || header[12] != 0)
            return null;
        var width = (int)BinaryPrimitives.ReadUInt32BigEndian(header);
        var height = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
        if (width is <= 0 or > 4096 || height is <= 0 or > 4096)
            return null;

        var stride = 1 + width * 4;
        var pixels = new byte[stride * height];
        compressed.Position = 0;
        using (var inflate = new DeflateStream(compressed, CompressionMode.Decompress))
        {
            var read = 0;
            int n;
            while (read < pixels.Length && (n = inflate.Read(pixels, read, pixels.Length - read)) > 0)
                read += n;
            if (read != pixels.Length)
                return null;
        }

        // PNG filters work per byte position, so swapping B and R inside every filtered pixel
        // gives the filtered form of the RGBA image without decoding the filters.
        for (var row = 0; row < height; row++)
        {
            var start = row * stride + 1;
            for (var x = 0; x < width; x++)
            {
                var i = start + x * 4;
                (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            }
        }

        using var zlib = new MemoryStream();
        using (var deflate = new ZLibStream(zlib, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(pixels);

        using var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "IDAT", zlib.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
        output.Write(number);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc(typeBytes, data));
        output.Write(number);
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in type)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (var b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }

        return table;
    }
}
