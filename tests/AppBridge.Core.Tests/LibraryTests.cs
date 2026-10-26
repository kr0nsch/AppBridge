using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using AppBridge.Core.Library;

namespace AppBridge.Core.Tests;

public sealed class LibraryTests : IDisposable
{
    private readonly string _folder = TestPaths.TempDirectory();

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void ReadsAppStoreMetadata()
    {
        var path = Ipa("weather.ipa", "com.example.weather", "2.4.1", metadata: new()
        {
            ["itemName"] = "Weather — Forecast and Radar",
            ["artistName"] = "Example LLC",
            ["itemId"] = "123456",
            ["softwareVersionExternalIdentifier"] = "987",
            ["apple-id"] = "owner@example.com",
        });

        var app = SavedAppLibrary.Read(path);

        Assert.Equal("com.example.weather", app.BundleId);
        Assert.Equal("Weather", app.Name);
        Assert.Equal("2.4.1", app.Version);
        Assert.Equal("Example LLC", app.Developer);
        Assert.Equal(123456, app.AppStoreId);
        Assert.Equal("987", app.ExternalVersionId);
        Assert.Equal("owner@example.com", app.PurchaserAppleId);
        Assert.Equal("16.0", app.MinimumOsVersion);
        Assert.True(app.RunsOnIPhone);
        Assert.Equal(new FileInfo(path).Length, app.SizeBytes);
    }

    [Fact]
    public void PrefersTheLocalizedHomeScreenName()
    {
        var path = Ipa("mail.ipa", "com.example.mail", localized: new() { ["ru"] = "Почта" });

        Assert.Equal("Почта", SavedAppLibrary.Read(path, "ru-RU").Name);
        Assert.Equal("Weather", SavedAppLibrary.Read(path, "en-US").Name);
        Assert.Equal("Weather", SavedAppLibrary.Read(path).Name);
    }

    [Theory]
    [InlineData("Caf\\U00e9", "Café")]
    [InlineData("Say \\\"hi\\\"\\n", "Say \"hi\"\n")]
    [InlineData("odd \\q", "odd \\q")]
    public void UnescapesOldStyleStrings(string raw, string expected) => Assert.Equal(expected, SavedAppLibrary.UnescapeStrings(raw));

    [Fact]
    public void OneOddFileDoesNotBreakTheScan()
    {
        Ipa("good.ipa", "com.example.good");
        var odd = Path.Combine(_folder, "odd.ipa");
        using (var zip = ZipFile.Open(odd, ZipArchiveMode.Create))
            Write(zip, "Payload/Odd.app/Info.plist", """<?xml version="1.0" encoding="UTF-8"?><plist version="1.0"><array><string>x</string></array></plist>""");

        var scan = SavedAppLibrary.Scan(_folder, "ru-RU");

        Assert.Equal("com.example.good", Assert.Single(scan.Apps).BundleId);
        Assert.Single(scan.UnreadableFiles);
    }

    [Fact]
    public void FlagsIPadOnlyApps()
    {
        Assert.False(SavedAppLibrary.Read(Ipa("pad.ipa", "com.example.pad", families: [2])).RunsOnIPhone);
        Assert.True(SavedAppLibrary.Read(Ipa("both.ipa", "com.example.both", families: [1, 2])).RunsOnIPhone);
    }

    [Fact]
    public void ScanKeepsTheNewestCopyOfEachAppAndReportsBrokenFiles()
    {
        Ipa("old.ipa", "com.example.a", "1.9.0");
        Ipa(Path.Combine("Backup", "new.ipa"), "com.example.a", "1.10.0");
        Ipa("b.ipa", "com.example.b", "3.0");
        File.WriteAllText(Path.Combine(_folder, "broken.ipa"), "not a zip");
        File.WriteAllText(Path.Combine(_folder, "notes.txt"), "ignored");

        var scan = SavedAppLibrary.Scan(_folder);

        Assert.Equal(["com.example.a", "com.example.b"], scan.Apps.Select(a => a.BundleId).Order());
        Assert.Equal("1.10.0", scan.Apps.Single(a => a.BundleId == "com.example.a").Version);
        Assert.Equal(["1.10.0", "1.9.0"], scan.VersionsOf("com.example.a").Select(a => a.Version));
        Assert.Equal(1, scan.OlderCopies);
        Assert.Equal("broken.ipa", Path.GetFileName(Assert.Single(scan.UnreadableFiles)));
    }

    [Fact]
    public void FilesWithoutAnAppStoreLicenseAreUnreadable()
    {
        Ipa("dev.ipa", "com.example.dev", licensed: false);

        var scan = SavedAppLibrary.Scan(_folder);

        Assert.Empty(scan.Apps);
        Assert.Single(scan.UnreadableFiles);
    }

    [Fact]
    public void ScanOfMissingFolderIsEmpty() => Assert.Empty(SavedAppLibrary.Scan(Path.Combine(_folder, "missing")).Apps);

    [Theory]
    [InlineData("1.10.0", "1.9.9", 1)]
    [InlineData("2.0", "2.0.0", 0)]
    [InlineData("1.0", "1.0.1", -1)]
    [InlineData("1.0b", "1.0a", 1)]
    public void ComparesVersionsNumerically(string a, string b, int sign) =>
        Assert.Equal(sign, Math.Sign(SavedAppLibrary.CompareVersions(a, b)));

    [Fact]
    public void ReadsTheAppIconFromAppleCrushedPng()
    {
        byte[] rgba = [10, 20, 30, 255, 40, 50, 60, 255, 70, 80, 90, 255, 100, 110, 120, 255];
        var path = Ipa("icon.ipa", "com.example.icon", icon: CrushedPng(2, 2, rgba));

        var png = SavedAppLibrary.ReadIcon(path);

        Assert.NotNull(png);
        Assert.Equal(rgba, DecodeUnfiltered(png!, 2, 2));
    }

    [Fact]
    public void StandardPngPassesThroughAndJunkIsRejected()
    {
        var standard = StandardPng(1, 1, [1, 2, 3, 255]);
        Assert.Same(standard, ApplePng.ToStandard(standard));
        Assert.Null(ApplePng.ToStandard([1, 2, 3]));
    }

    private string Ipa(string relative, string bundleId, string version = "1.0", Dictionary<string, string>? metadata = null,
        Dictionary<string, string>? localized = null, int[]? families = null, byte[]? icon = null, bool licensed = true)
    {
        var path = Path.Combine(_folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var info = new StringBuilder($"<key>CFBundleIdentifier</key><string>{bundleId}</string>"
            + $"<key>CFBundleShortVersionString</key><string>{version}</string>"
            + "<key>CFBundleDisplayName</key><string>Weather</string><key>MinimumOSVersion</key><string>16.0</string>");
        if (families is not null)
            info.Append("<key>UIDeviceFamily</key><array>").Append(string.Concat(families.Select(f => $"<integer>{f}</integer>"))).Append("</array>");
        if (icon is not null)
            info.Append("<key>CFBundleIcons</key><dict><key>CFBundlePrimaryIcon</key><dict><key>CFBundleIconFiles</key><array><string>AppIcon60x60</string></array></dict></dict>");
        Write(zip, "Payload/Weather.app/Info.plist", $"""<?xml version="1.0" encoding="UTF-8"?><plist version="1.0"><dict>{info}</dict></plist>""");
        if (metadata is not null)
            Write(zip, "iTunesMetadata.plist", TestIpa.Plist(metadata!));
        if (licensed)
            Write(zip, "Payload/Weather.app/SC_Info/Weather.sinf", "license");
        foreach (var (language, name) in localized ?? [])
            Write(zip, $"Payload/Weather.app/{language}.lproj/InfoPlist.strings", $"\"CFBundleDisplayName\" = \"{name}\";\n");
        if (icon is not null)
        {
            using (var large = zip.CreateEntry("Payload/Weather.app/AppIcon60x60@2x.png").Open())
                large.Write(icon);
            using (var small = zip.CreateEntry("Payload/Weather.app/AppIcon20x20@2x.png").Open())
                small.Write(icon.AsSpan(0, icon.Length - 1));
        }

        return path;
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    /// <summary>A PNG the way Xcode stores it: CgBI chunk, raw deflate, BGRA pixels.</summary>
    private static byte[] CrushedPng(int width, int height, byte[] rgba)
    {
        var raw = Filtered(width, height, rgba, bgra: true);
        using var deflated = new MemoryStream();
        using (var deflate = new DeflateStream(deflated, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(raw);
        return Png([("CgBI", [0x50, 0x00, 0x20, 0x02]), ("IHDR", Header(width, height)), ("IDAT", deflated.ToArray()), ("IEND", [])]);
    }

    private static byte[] StandardPng(int width, int height, byte[] rgba)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(Filtered(width, height, rgba, bgra: false));
        return Png([("IHDR", Header(width, height)), ("IDAT", compressed.ToArray()), ("IEND", [])]);
    }

    /// <summary>Rows with the "Sub" filter, so the test also covers filtered data.</summary>
    private static byte[] Filtered(int width, int height, byte[] rgba, bool bgra)
    {
        var stride = width * 4;
        var output = new byte[(stride + 1) * height];
        for (var y = 0; y < height; y++)
        {
            output[y * (stride + 1)] = 1;
            for (var x = 0; x < stride; x++)
            {
                var source = y * stride + x;
                var channel = x % 4;
                if (bgra && channel is 0 or 2)
                    source += channel == 0 ? 2 : -2;
                var left = x >= 4 ? rgba[source - 4] : 0;
                output[y * (stride + 1) + 1 + x] = (byte)(rgba[source] - left);
            }
        }

        return output;
    }

    private static byte[] DecodeUnfiltered(byte[] png, int width, int height)
    {
        var offset = 8;
        using var idat = new MemoryStream();
        while (offset < png.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));
            if (Encoding.ASCII.GetString(png, offset + 4, 4) == "IDAT")
                idat.Write(png, offset + 8, length);
            offset += 12 + length;
        }

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var data = raw.ToArray();
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            Assert.Equal(1, data[y * (stride + 1)]);
            for (var x = 0; x < stride; x++)
                pixels[y * stride + x] = (byte)(data[y * (stride + 1) + 1 + x] + (x >= 4 ? pixels[y * stride + x - 4] : 0));
        }

        return pixels;
    }

    private static byte[] Header(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = 8;
        header[9] = 6;
        return header;
    }

    private static byte[] Png(IEnumerable<(string Type, byte[] Data)> chunks)
    {
        using var output = new MemoryStream();
        output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        foreach (var (type, data) in chunks)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
            output.Write(length);
            output.Write(Encoding.ASCII.GetBytes(type));
            output.Write(data);
            output.Write(new byte[4]);
        }

        return output.ToArray();
    }
}
