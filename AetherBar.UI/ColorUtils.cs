using System.Globalization;
using System.Windows.Media;

namespace AetherBar.UI;

internal static class ColorUtils
{
    public static Color? ParseHexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return null;
        hex = hex.TrimStart('#');
        if (hex.Length != 6) return null;
        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var val))
            return null;
        return Color.FromRgb((byte)(val >> 16), (byte)((val >> 8) & 0xFF), (byte)(val & 0xFF));
    }

    public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static Color ClampLightness(Color color, double minLightness, double maxLightness)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double lightness = (max + min) / 2.0;

        if (lightness >= minLightness && lightness <= maxLightness)
            return color;

        double target = lightness < minLightness ? minLightness : maxLightness;
        double delta = max - min;
        if (delta < 0.001)
            return Color.FromRgb((byte)(target * 255), (byte)(target * 255), (byte)(target * 255));

        double saturation = delta / (1.0 - Math.Abs(2.0 * lightness - 1.0));
        double hue;
        if (max == r) hue = ((g - b) / delta + (g < b ? 6 : 0)) / 6.0;
        else if (max == g) hue = ((b - r) / delta + 2) / 6.0;
        else hue = ((r - g) / delta + 4) / 6.0;

        return FromHsl(hue, saturation, target);
    }

    private static Color FromHsl(double h, double s, double l)
    {
        if (s == 0) { byte v = (byte)(l * 255); return Color.FromRgb(v, v, v); }

        static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        double q2 = l < 0.5 ? l * (1 + s) : l + s - l * s;
        double p2 = 2 * l - q2;
        return Color.FromRgb(
            (byte)(HueToRgb(p2, q2, h + 1.0 / 3) * 255),
            (byte)(HueToRgb(p2, q2, h) * 255),
            (byte)(HueToRgb(p2, q2, h - 1.0 / 3) * 255));
    }
}
