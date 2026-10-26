using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Netimobiledevice.Plist;

namespace AppBridge.Core.Library;

/// <summary>
/// Reads the apps saved in a folder (and its subfolders): the files AppBridge keeps after a
/// transfer, saved for later, or copied there from elsewhere.
/// </summary>
public static partial class SavedAppLibrary
{
    private const long MaxPlistBytes = 8 * 1024 * 1024;
    private const long MaxIconBytes = 4 * 1024 * 1024;

    private static readonly EnumerationOptions Search = new()
    {
        RecurseSubdirectories = true,
        MaxRecursionDepth = 4,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    /// <param name="language">BCP-47 tag (such as "ru-RU") used to show app names the way the iPhone would.</param>
    public static LibraryScan Scan(string folder, string? language = null, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(folder))
            return LibraryScan.Empty;

        List<SavedApp> found = [];
        List<string> unreadable = [];
        foreach (var file in Directory.EnumerateFiles(folder, "*.ipa", Search))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                found.Add(Read(file, language));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppLog.Warn($"Skipping unreadable saved app {Path.GetFileName(file)}: {ex.Message}");
                unreadable.Add(file);
            }
        }

        var versions = found
            .GroupBy(a => a.BundleId, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(a => a, NewestFirst.Instance).ToList())
            .OrderBy(g => g[0].Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        return new LibraryScan(versions.Select(g => g[0]).ToList(), versions.SelectMany(g => g).ToList(), unreadable);
    }

    public static SavedApp Read(string path, string? language = null)
    {
        var file = new FileInfo(path);
        using var zip = ZipFile.OpenRead(path);
        var infoEntry = FindInfoPlist(zip) ?? throw new InvalidDataException("The file does not contain an iPhone app.");
        var info = PropertyList.LoadFromByteArray(ReadEntry(infoEntry, MaxPlistBytes)).AsDictionaryNode();
        var metadataEntry = zip.GetEntry("iTunesMetadata.plist");
        var metadata = metadataEntry is null ? null : PropertyList.LoadFromByteArray(ReadEntry(metadataEntry, MaxPlistBytes)) as DictionaryNode;

        var bundleId = GetString(info, "CFBundleIdentifier") ?? throw new InvalidDataException("The app has no bundle identifier.");
        var appFolder = infoEntry.FullName[..^"Info.plist".Length];
        if (!zip.Entries.Any(e => e.FullName.StartsWith(appFolder + "SC_Info/", StringComparison.Ordinal) && e.Name.EndsWith(".sinf", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The app carries no App Store license.");
        var accountInfo = GetDictionary(GetDictionary(metadata, "com.apple.iTunesStore.downloadInfo"), "accountInfo");
        var externalVersion = GetLong(metadata, "softwareVersionExternalIdentifier");

        return new SavedApp(
            path,
            bundleId,
            FirstNonEmpty(LocalizedName(zip, infoEntry, language), GetString(info, "CFBundleDisplayName"), GetString(metadata, "bundleDisplayName"),
                GetString(info, "CFBundleName"), GetString(metadata, "itemName")) ?? bundleId,
            GetString(info, "CFBundleShortVersionString") ?? GetString(metadata, "bundleShortVersionString") ?? GetString(info, "CFBundleVersion") ?? "",
            GetString(metadata, "artistName"),
            GetLong(metadata, "itemId"),
            externalVersion is > 0 ? externalVersion.Value.ToString(CultureInfo.InvariantCulture) : null,
            GetString(accountInfo, "AppleID") ?? GetString(metadata, "apple-id"),
            GetString(info, "MinimumOSVersion"),
            RunsOnIPhone(info),
            file.Length,
            file.LastWriteTimeUtc);
    }

    /// <summary>The app's home-screen icon as a standard PNG, or null when it can't be found.</summary>
    public static byte[]? ReadIcon(string path)
    {
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var infoEntry = FindInfoPlist(zip);
            if (infoEntry is null)
                return null;

            var appFolder = infoEntry.FullName[..^"Info.plist".Length];
            var info = PropertyList.LoadFromByteArray(ReadEntry(infoEntry, MaxPlistBytes)).AsDictionaryNode();
            var names = IconBaseNames(info);

            // The biggest iPhone icon wins; iPad-only variants are a last resort.
            var icon = zip.Entries
                .Where(e => e.FullName.StartsWith(appFolder, StringComparison.Ordinal)
                            && e.FullName.IndexOf('/', appFolder.Length) < 0
                            && e.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                            && e.Length is > 0 and <= MaxIconBytes
                            && names.Any(n => e.Name.StartsWith(n, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(e => e.Name.Contains("~ipad", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(e => e.Length)
                .FirstOrDefault();
            return icon is null ? null : ApplePng.ToStandard(ReadEntry(icon, MaxIconBytes));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppLog.Debug($"No icon in {Path.GetFileName(path)}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The home-screen name from &lt;language&gt;.lproj/InfoPlist.strings, as the iPhone shows it.</summary>
    private static string? LocalizedName(ZipArchive zip, ZipArchiveEntry infoEntry, string? language)
    {
        if (string.IsNullOrEmpty(language))
            return null;

        var appFolder = infoEntry.FullName[..^"Info.plist".Length];
        var primary = language.Split('-', '_')[0];
        foreach (var candidate in new[] { language, language.Replace('-', '_'), primary }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var entry = zip.GetEntry($"{appFolder}{candidate}.lproj/InfoPlist.strings");
            if (entry is null || entry.Length > MaxPlistBytes)
                continue;
            try
            {
                var bytes = ReadEntry(entry, MaxPlistBytes);
                var strings = TryParseStrings(bytes);
                if (FirstNonEmpty(GetString(strings, "CFBundleDisplayName"), GetString(strings, "CFBundleName")) is { } name)
                    return name;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppLog.Debug($"Unreadable {entry.FullName}: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>.strings files are binary plists in shipped apps; older ones are "key" = "value"; text.</summary>
    private static DictionaryNode? TryParseStrings(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith("bplist"u8) || bytes.AsSpan().StartsWith("<?xml"u8))
            return PropertyList.LoadFromByteArray(bytes) as DictionaryNode;

        var text = bytes.Length >= 2 && (bytes[0], bytes[1]) is (0xFF, 0xFE) or (0xFE, 0xFF)
            ? new UnicodeEncoding(bytes[0] == 0xFE, true).GetString(bytes, 2, bytes.Length - 2)
            : Encoding.UTF8.GetString(bytes);
        var result = new DictionaryNode();
        foreach (Match match in StringsPair().Matches(text))
            result[match.Groups[1].Value] = new StringNode(UnescapeStrings(match.Groups[2].Value));
        return result;
    }

    /// <summary>Escapes of the old-style .strings format (\n, \t, \", \\, \Uxxxx); anything else stays as written.</summary>
    internal static string UnescapeStrings(string value)
    {
        var text = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 >= value.Length)
            {
                text.Append(value[i]);
                continue;
            }

            var next = value[++i];
            if (next is 'U' or 'u' && i + 4 < value.Length
                && int.TryParse(value.AsSpan(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            {
                text.Append((char)code);
                i += 4;
                continue;
            }

            text.Append(next switch
            {
                'n' => "\n",
                't' => "\t",
                'r' => "\r",
                '"' or '\\' or '\'' => next.ToString(),
                _ => "\\" + next,
            });
        }

        return text.ToString();
    }

    [GeneratedRegex("\"?([A-Za-z]+)\"?\\s*=\\s*\"((?:[^\"\\\\]|\\\\.)*)\"\\s*;")]
    private static partial Regex StringsPair();

    private static ZipArchiveEntry? FindInfoPlist(ZipArchive zip) => zip.Entries.FirstOrDefault(e =>
    {
        var parts = e.FullName.Split('/');
        return parts.Length == 3 && parts[0] == "Payload" && parts[1].EndsWith(".app", StringComparison.OrdinalIgnoreCase) && parts[2] == "Info.plist";
    });

    private static List<string> IconBaseNames(DictionaryNode info)
    {
        List<string> names = [];
        foreach (var key in new[] { "CFBundleIcons", "CFBundleIcons~ipad" })
        {
            var primary = GetDictionary(GetDictionary(info, key), "CFBundlePrimaryIcon");
            if (primary is not null && primary.TryGetValue("CFBundleIconFiles", out var files) && files is ArrayNode list)
                names.AddRange(list.OfType<StringNode>().Select(s => s.Value).Where(s => s.Length > 0));
            if (GetString(primary, "CFBundleIconName") is { Length: > 0 } iconName)
                names.Add(iconName);
        }

        if (info.TryGetValue("CFBundleIconFiles", out var legacy) && legacy is ArrayNode legacyList)
            names.AddRange(legacyList.OfType<StringNode>().Select(s => Path.GetFileNameWithoutExtension(s.Value)));
        names.Add("AppIcon");
        names.Add("Icon");
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool RunsOnIPhone(DictionaryNode info)
    {
        if (!info.TryGetValue("UIDeviceFamily", out var node) || node is not ArrayNode families || families.Count == 0)
            return true;
        return families.Any(f => f switch
        {
            IntegerNode i => i.SignedValue == 1,
            StringNode s => s.Value == "1",
            _ => false,
        });
    }

    private static byte[] ReadEntry(ZipArchiveEntry entry, long limit)
    {
        if (entry.Length > limit)
            throw new InvalidDataException($"{entry.FullName} is unexpectedly large.");
        using var stream = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static DictionaryNode? GetDictionary(DictionaryNode? dict, string key) =>
        dict is not null && dict.TryGetValue(key, out var node) ? node as DictionaryNode : null;

    private static string? GetString(DictionaryNode? dict, string key) =>
        dict is not null && dict.TryGetValue(key, out var node) && node is StringNode s ? s.Value : null;

    private static long? GetLong(DictionaryNode? dict, string key) =>
        dict is not null && dict.TryGetValue(key, out var node) ? node switch
        {
            IntegerNode i => i.SignedValue,
            StringNode s when long.TryParse(s.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) => v,
            _ => null,
        } : null;

    /// <summary>Higher version first; for the same version, the most recently saved file.</summary>
    private sealed class NewestFirst : IComparer<SavedApp>
    {
        public static NewestFirst Instance { get; } = new();

        public int Compare(SavedApp? x, SavedApp? y)
        {
            if (x is null || y is null)
                return x is null ? (y is null ? 0 : -1) : 1;
            var byVersion = CompareVersions(x.Version, y.Version);
            return byVersion != 0 ? byVersion : x.SavedAtUtc.CompareTo(y.SavedAtUtc);
        }
    }

    /// <summary>Compares dotted versions number by number ("1.10" is newer than "1.9").</summary>
    public static int CompareVersions(string a, string b)
    {
        var left = a.Split('.');
        var right = b.Split('.');
        for (var i = 0; i < Math.Max(left.Length, right.Length); i++)
        {
            var l = i < left.Length ? left[i] : "0";
            var r = i < right.Length ? right[i] : "0";
            var compared = long.TryParse(l, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ln)
                           && long.TryParse(r, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rn)
                ? ln.CompareTo(rn)
                : string.CompareOrdinal(l, r);
            if (compared != 0)
                return compared;
        }

        return 0;
    }
}
