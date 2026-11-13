using System.Globalization;

namespace Hollowwork.Patches;

public static class ShaderColor
{
    public static bool TryParse(string? value, out byte r, out byte g, out byte b, out byte a)
    {
        r = g = b = 0;
        a = 255;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.Trim().TrimStart('#');
        if (hex.Length == 6)
        {
            hex += "FF";
        }

        if (hex.Length != 8)
        {
            return false;
        }

        return byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r)
            && byte.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g)
            && byte.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b)
            && byte.TryParse(hex[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a);
    }

    public static string ToFloat4(string? value)
    {
        if (!TryParse(value, out var r, out var g, out var b, out var a))
        {
            throw new PatchValidationException($"Invalid color '{value}'. Use #RRGGBB or #RRGGBBAA.");
        }

        static string Format(byte value) => (value / 255f).ToString("0.0##", CultureInfo.InvariantCulture);

        return $"{Format(r)}f, {Format(g)}f, {Format(b)}f, {Format(a)}f";
    }
}
