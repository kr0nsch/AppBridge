using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace FreeReturn.Services;

/// <summary>Localized strings from Strings/&lt;language&gt;/Resources.resw.</summary>
public static class Loc
{
    private static readonly Lazy<ResourceLoader> Loader = new(() => new ResourceLoader());

    public static string Get(string key)
    {
        try
        {
            var value = Loader.Value.GetString(key);
            return string.IsNullOrEmpty(value) ? key : value;
        }
        catch (Exception)
        {
            return key;
        }
    }

    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    /// <summary>Picks "One"/"Few"/"Many" forms; Russian needs all three, English uses One/Many.</summary>
    public static string Plural(string key, long count)
    {
        var form = PluralForm(count);
        var text = Get($"{key}_{form}");
        if (text == $"{key}_{form}")
            text = Get($"{key}_Many");
        return string.Format(CultureInfo.CurrentCulture, text, count);
    }

    private static string PluralForm(long count)
    {
        var language = Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride;
        if (string.IsNullOrEmpty(language))
            language = CultureInfo.CurrentUICulture.Name;

        if (language.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            var n = Math.Abs(count);
            if (n % 10 == 1 && n % 100 != 11)
                return "One";
            if (n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14)
                return "Few";
            return "Many";
        }

        return count == 1 ? "One" : "Many";
    }
}
