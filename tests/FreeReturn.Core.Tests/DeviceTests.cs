using System.Text;
using FreeReturn.Core.Devices;
using Netimobiledevice.Plist;

namespace FreeReturn.Core.Tests;

public class DeviceTests
{
    [Fact]
    public void ReadsMainAppFromIpa()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var ipa = TestIpa.Create(dir, "com.example.notes", version: "4.5", minimumOs: "17.2");

            var package = IpaPackage.Read(ipa);

            Assert.Equal("com.example.notes", package.BundleId);
            Assert.Equal("4.5", package.Version);
            Assert.Equal("17.2", package.MinimumOsVersion);
            Assert.NotNull(package.ITunesMetadata);
            Assert.Equal([1, 2, 3, 4], package.Sinf);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void IpaWithoutMetadataStillReads()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var package = IpaPackage.Read(TestIpa.Create(dir, "com.example.dev", withMetadata: false, withSinf: false));

            Assert.Null(package.ITunesMetadata);
            Assert.Null(package.Sinf);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RejectsZipWithoutApp()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var path = Path.Combine(dir, "empty.ipa");
            using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
                zip.CreateEntry("readme.txt");

            Assert.Throws<InvalidDataException>(() => IpaPackage.Read(path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void ParsesAppStoreAppFromInstallationProxy()
    {
        var metadata = new DictionaryNode
        {
            { "itemId", new IntegerNode(1234567890L) },
            { "itemName", new StringNode("Notes Plus") },
            { "artistName", new StringNode("Example Inc.") },
            { "softwareVersionExternalIdentifier", new IntegerNode(876543210L) },
            {
                "com.apple.iTunesStore.downloadInfo", new DictionaryNode
                {
                    { "accountInfo", new DictionaryNode { { "AppleID", new StringNode("anna@example.com") } } },
                }
            },
        };
        var app = new DictionaryNode
        {
            { "CFBundleIdentifier", new StringNode("com.example.notes") },
            { "CFBundleDisplayName", new StringNode("Notes+") },
            { "CFBundleShortVersionString", new StringNode("3.1") },
            { "MinimumOSVersion", new StringNode("16.0") },
            { "StaticDiskUsage", new IntegerNode(52_000_000L) },
            { "iTunesMetadata", new DataNode(PropertyList.SaveAsByteArray(metadata, PlistFormat.Binary)) },
        };

        var parsed = DeviceService.ParseApp(app)!;

        Assert.Equal("Notes+", parsed.Name);
        Assert.Equal("3.1", parsed.Version);
        Assert.Equal("Example Inc.", parsed.Developer);
        Assert.Equal(1234567890L, parsed.AppStoreId);
        Assert.Equal("876543210", parsed.ExternalVersionId);
        Assert.Equal("anna@example.com", parsed.PurchaserAppleId);
        Assert.Equal(52_000_000L, parsed.SizeBytes);
        Assert.True(parsed.IsFromAppStore);
    }

    [Fact]
    public void AppWithoutStoreMetadataIsNotFromAppStore()
    {
        var app = new DictionaryNode
        {
            { "CFBundleIdentifier", new StringNode("com.example.beta") },
            { "CFBundleName", new StringNode("Beta") },
        };

        var parsed = DeviceService.ParseApp(app)!;

        Assert.Equal("Beta", parsed.Name);
        Assert.False(parsed.IsFromAppStore);
        Assert.Null(parsed.AppStoreId);
    }

    [Fact]
    public void CorruptMetadataIsTolerated()
    {
        var app = new DictionaryNode
        {
            { "CFBundleIdentifier", new StringNode("com.example.odd") },
            { "iTunesMetadata", new DataNode(Encoding.UTF8.GetBytes("not a plist")) },
        };

        var parsed = DeviceService.ParseApp(app)!;

        Assert.Equal("com.example.odd", parsed.Name);
        Assert.False(parsed.IsFromAppStore);
    }

    [Fact]
    public void AppWithoutBundleIdIsSkipped() => Assert.Null(DeviceService.ParseApp(new DictionaryNode()));

    [Theory]
    [InlineData("17", "17.0")]
    [InlineData("16.4", "16.4")]
    [InlineData("18.0.1", "18.0.1")]
    public void ParsesIosVersions(string text, string expected)
    {
        Assert.True(DeviceService.TryParseVersion(text, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Fact]
    public void RedactsUdids() => Assert.Equal("0000…ABCD", DeviceWatcher.Redact("00008030-000A1B2C3D4EABCD"));
}
