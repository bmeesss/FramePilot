using System.Globalization;

namespace FramePilot.Core.Infrastructure;

/// <summary>Formats byte counts for the user interface.</summary>
public static class ByteFormatter
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    public static string Format(long bytes, string format = "0.0")
    {
        if (bytes < 0)
        {
            return "Unavailable";
        }

        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return value.ToString(format, CultureInfo.CurrentCulture) + " " + Units[unit];
    }

    public static string Format(double bytes, string format = "0.0")
    {
        if (double.IsNaN(bytes) || double.IsInfinity(bytes) || bytes < 0)
        {
            return "Unavailable";
        }

        return Format((long)bytes, format);
    }
}
