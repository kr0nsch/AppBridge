using System.Globalization;

namespace AppBridge.Services;

public static class Format
{
    public static string Bytes(long bytes)
    {
        string[] units = [Loc.Get("Unit_KB"), Loc.Get("Unit_MB"), Loc.Get("Unit_GB"), Loc.Get("Unit_TB")];
        double value = Math.Max(0, bytes) / 1000.0;
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        var format = value >= 100 || unit == 0 ? "0" : "0.#";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {units[unit]}";
    }
}
