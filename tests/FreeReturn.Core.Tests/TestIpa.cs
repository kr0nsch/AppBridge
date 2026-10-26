using System.IO.Compression;
using System.Text;

namespace FreeReturn.Core.Tests;

/// <summary>Builds small IPA-shaped zip files for tests.</summary>
internal static class TestIpa
{
    public static string Create(string directory, string bundleId, string version = "1.2.3", string? minimumOs = "16.0",
        bool withMetadata = true, bool withSinf = true, string appName = "Demo")
    {
        var path = Path.Combine(directory, $"{bundleId}.ipa");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(zip, $"Payload/{appName}.app/Info.plist", Plist(new Dictionary<string, string?>
        {
            ["CFBundleIdentifier"] = bundleId,
            ["CFBundleShortVersionString"] = version,
            ["MinimumOSVersion"] = minimumOs,
        }));
        Write(zip, $"Payload/{appName}.app/Frameworks/Inner.framework/Info.plist", Plist(new Dictionary<string, string?> { ["CFBundleIdentifier"] = "inner.framework" }));
        if (withMetadata)
            Write(zip, "iTunesMetadata.plist", Plist(new Dictionary<string, string?> { ["itemName"] = appName }));
        if (withSinf)
            zip.CreateEntry($"Payload/{appName}.app/SC_Info/{appName}.sinf").Open().Write([1, 2, 3, 4]);
        return path;
    }

    public static string Plist(IDictionary<string, string?> values)
    {
        var body = new StringBuilder();
        foreach (var (key, value) in values)
        {
            if (value is not null)
                body.Append($"<key>{key}</key><string>{value}</string>");
        }

        return $"""<?xml version="1.0" encoding="UTF-8"?><plist version="1.0"><dict>{body}</dict></plist>""";
    }

    private static void Write(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(text);
    }
}
