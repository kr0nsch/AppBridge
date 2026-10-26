using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FreeReturn.Core.Tests;

public class AppTests
{
    [Fact]
    public void SettingsRoundTrip()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var file = Path.Combine(dir, "settings.json");
            var store = new SettingsStore(file);
            store.Current.Language = "ru-RU";
            store.Current.VersionPolicy = VersionPolicy.SameAsSource;
            store.Current.KeepDownloads = false;
            store.Save();

            var reloaded = new SettingsStore(file).Current;

            Assert.Equal("ru-RU", reloaded.Language);
            Assert.Equal(VersionPolicy.SameAsSource, reloaded.VersionPolicy);
            Assert.False(reloaded.KeepDownloads);
            Assert.Contains("\"SameAsSource\"", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void CorruptSettingsFallBackToDefaults()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var file = Path.Combine(dir, "settings.json");
            File.WriteAllText(file, "{ this is not json");

            var settings = new SettingsStore(file).Current;

            Assert.True(settings.AcquireFreeLicenses);
            Assert.Equal(VersionPolicy.Latest, settings.VersionPolicy);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void PortableMarkerKeepsDataNextToApp()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            Assert.False(AppPaths.Detect(dir).IsPortable);

            File.WriteAllText(Path.Combine(dir, AppPaths.PortableMarkerFileName), "");
            var paths = AppPaths.Detect(dir);

            Assert.True(paths.IsPortable);
            Assert.Equal(Path.Combine(dir, "Data"), paths.DataDirectory);
            Assert.StartsWith(paths.DataDirectory, paths.IpatoolHome);
            Assert.StartsWith(paths.DataDirectory, paths.DefaultDownloadDirectory);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void KeychainPassphraseIsStableAndProtected()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            var first = Ipatool.KeychainPassphrase.GetOrCreate(dir);
            var second = Ipatool.KeychainPassphrase.GetOrCreate(dir);

            Assert.Equal(first, second);
            Assert.True(first.Length >= 40);
            Assert.DoesNotContain(first, File.ReadAllText(Path.Combine(dir, "keychain.key")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void UnreadablePassphraseStartsFreshSession()
    {
        var dir = TestPaths.TempDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(dir, ".ipatool"));
            File.WriteAllText(Path.Combine(dir, ".ipatool", "account"), "old encrypted session");
            File.WriteAllBytes(Path.Combine(dir, "keychain.key"), [1, 2, 3]);

            var passphrase = Ipatool.KeychainPassphrase.GetOrCreate(dir);

            Assert.False(string.IsNullOrEmpty(passphrase));
            Assert.False(File.Exists(Path.Combine(dir, ".ipatool", "account")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>Every UI string must exist in every language with the same placeholders.</summary>
    [Fact]
    public void TranslationsMatchEnglish()
    {
        var strings = Path.Combine(TestPaths.RepositoryRoot, "src", "FreeReturn", "Strings");
        var english = Load(Path.Combine(strings, "en-US", "Resources.resw"));
        foreach (var dir in Directory.GetDirectories(strings).Where(d => !d.EndsWith("en-US", StringComparison.Ordinal)))
        {
            var other = Load(Path.Combine(dir, "Resources.resw"));
            Assert.Empty(english.Keys.Except(other.Keys));
            Assert.Empty(other.Keys.Except(english.Keys));
            foreach (var (key, value) in english)
                Assert.True(Placeholders(value).SetEquals(Placeholders(other[key])), $"{Path.GetFileName(dir)}: placeholders differ for {key}");
        }
    }

    private static Dictionary<string, string> Load(string path) =>
        XDocument.Load(path).Root!.Elements("data").ToDictionary(e => (string)e.Attribute("name")!, e => (string)e.Element("value")!);

    private static HashSet<string> Placeholders(string text) => [.. Regex.Matches(text, @"\{\d+\}").Select(m => m.Value)];
}
