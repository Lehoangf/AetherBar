using System.Windows;
using System.Windows.Media;

namespace AetherBar.Core.Visualizer;

public class BarVisualizer : IVisualizerRenderer
{
    public string Name => "Bar";

    public void Render(DrawingContext context, float[] fftData, float peakLevel, Size size, RenderOptions options)
    {
        if (fftData.Length == 0) return;

        int offset = Math.Min(options.BarStartOffset, fftData.Length - 1);
        int effectiveLength = fftData.Length - offset;
        int barCount = Math.Min(options.BarCount, Math.Min(effectiveLength, (int)(size.Width / 3)));
        double barWidth = size.Width / barCount;
        double centerY = size.Height;

        for (int i = 0; i < barCount; i++)
        {
            int idx = offset + (int)((float)i / barCount * effectiveLength);
            if (idx >= fftData.Length) continue;
            float value = Math.Min(1, fftData[idx] * (float)options.Sensitivity);
            if (value < options.Threshold) continue;
            double barHeight = value * size.Height * 0.9;
            double x = i * barWidth;

            if (barHeight < 0.5) continue;

            var color = GetThemeColor(options.ColorTheme, (float)i / barCount, value, options.CustomColor);
            byte alpha = (byte)(options.Opacity * 255);

            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), null,
                new Rect(x, centerY - barHeight, Math.Max(1, barWidth - 1), barHeight));
        }

        if (options.ShowPeak && peakLevel > 0.01f)
        {
            var peakColor = GetThemeColor(options.ColorTheme, 0.5f, 1, options.CustomColor);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), null,
                new Rect(0, centerY - peakLevel * size.Height * 0.9, size.Width, 2));
        }
    }

    public static Color GetThemeColor(string theme, float t, float intensity, Color customColor)
    {
        float brightness = 0.5f + intensity * 0.5f;
        byte br(byte v) => (byte)(v * brightness);
        byte brI(byte v) => (byte)(v * (0.5f + intensity * 0.5f));

        return theme switch
        {
            "Neon Blue" => Color.FromRgb(0, br((byte)(t * 200)), brI((byte)(100 + t * 155))),
            "Matrix Green" => Color.FromRgb(0, br((byte)(80 + t * 175)), br((byte)(t * 100))),
            "Fire" => Color.FromRgb(brI((byte)(150 + t * 105)), br((byte)(t * 200)), 0),
            "Monochrome" => Color.FromRgb(brI((byte)(80 + t * 175)), brI((byte)(80 + t * 175)), brI((byte)(80 + t * 175))),
            "Sunset" => Color.FromRgb(brI((byte)(200 + t * 55)), br((byte)(100 - t * 80)), br((byte)(t * 150))),
            "Ocean" => Color.FromRgb(0, br((byte)(100 - t * 60)), brI((byte)(100 + t * 155))),
            "Cyberpunk" => Color.FromRgb(br((byte)(200 - t * 150)), br((byte)(t * 150)), brI((byte)(100 + t * 155))),
            "Custom" => Color.FromRgb(br(customColor.R), br(customColor.G), br(customColor.B)),
            _ => RainbowColor(t, intensity)
        };
    }

    private static Color RainbowColor(float t, float intensity)
    {
        float segment = t * 5;
        int seg = (int)segment;
        float frac = segment - seg;
        float r, g, b;

        switch (seg)
        {
            case 0: r = 1; g = frac;       b = 0; break;          // red → yellow
            case 1: r = 1 - frac; g = 1;   b = 0; break;          // yellow → green
            case 2: r = 0; g = 1;          b = frac; break;       // green → cyan
            case 3: r = 0; g = 1 - frac;   b = 1; break;          // cyan → blue
            default: r = frac; g = 0;      b = 1; break;          // blue → violet
        }

        float brightness = 0.5f + intensity * 0.5f;
        return Color.FromRgb((byte)(r * brightness * 255), (byte)(g * brightness * 255), (byte)(b * brightness * 255));
    }

    public void Reset() { }
}
