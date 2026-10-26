using System.IO.Compression;
using Netimobiledevice.Plist;

namespace AppBridge.Core.Devices;

/// <summary>The parts of an IPA that installation needs: identity, requirements and App Store metadata.</summary>
public sealed record IpaPackage(
    string BundleId,
    string Version,
    string? MinimumOsVersion,
    byte[]? ITunesMetadata,
    byte[]? Sinf)
{
    private const long MaxMetadataBytes = 8 * 1024 * 1024;

    public static IpaPackage Read(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        ZipArchiveEntry? infoPlist = null;
        ZipArchiveEntry? sinf = null;
        foreach (var entry in zip.Entries)
        {
            var parts = entry.FullName.Split('/');
            if (parts.Length == 3 && parts[0] == "Payload" && parts[1].EndsWith(".app", StringComparison.OrdinalIgnoreCase) && parts[2] == "Info.plist")
                infoPlist = entry;
            else if (parts.Length == 4 && parts[0] == "Payload" && parts[1].EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                     && parts[2] == "SC_Info" && parts[3].EndsWith(".sinf", StringComparison.OrdinalIgnoreCase))
                sinf ??= entry;
        }

        if (infoPlist is null)
            throw new InvalidDataException("The IPA does not contain an app.");

        var info = PropertyList.LoadFromByteArray(ReadSmall(infoPlist)).AsDictionaryNode();
        var bundleId = GetString(info, "CFBundleIdentifier") ?? throw new InvalidDataException("The app has no bundle identifier.");
        var version = GetString(info, "CFBundleShortVersionString") ?? GetString(info, "CFBundleVersion") ?? "";

        var metadataEntry = zip.GetEntry("iTunesMetadata.plist");
        return new IpaPackage(
            bundleId,
            version,
            GetString(info, "MinimumOSVersion"),
            metadataEntry is null ? null : ReadSmall(metadataEntry),
            sinf is null ? null : ReadSmall(sinf));
    }

    private static byte[] ReadSmall(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxMetadataBytes)
            throw new InvalidDataException($"{entry.FullName} is unexpectedly large.");
        using var stream = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string? GetString(DictionaryNode dict, string key) =>
        dict.TryGetValue(key, out var node) && node is StringNode s ? s.Value : null;
}
