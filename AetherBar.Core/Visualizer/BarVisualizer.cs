using System;
using System.Windows;
using System.Windows.Media;

namespace AetherBar.Core.Visualizer;

public class BarVisualizer : IVisualizerRenderer
{
    public string Name => "Bar";

    public void Render(DrawingContext context, float[] fftData, float peakLevel, Size size, RenderOptions options)
    {
        if (fftData.Length == 0) return;

        var animated = options.AnimatedGradientEnabled;
        var animTime = options.AnimationTime;
        var animDir = options.AnimatedGradientDirection;
        var animSpeed = options.AnimatedGradientSpeed;

        int offset = Math.Min(options.BarStartOffset, fftData.Length - 1);
        int effectiveLength = fftData.Length - offset;
        int barCount = Math.Min(options.BarCount, Math.Min(effectiveLength, (int)(size.Width / 3)));
        double barWidth = size.Width / barCount;
        double centerY = size.Height;

        for (int i = 0; i < barCount; i++)
        {
            int idx = offset + (int)((float)i / barCount * effectiveLength);
            if (idx >= fftData.Length || idx < 0) continue;
            float value = Math.Min(1, fftData[idx] * (float)options.Sensitivity);
            if (value < options.Threshold) continue;
            double barHeight = value * size.Height * 0.9;
            double x = i * barWidth;

            if (barHeight < 0.5) continue;

            float t = (float)i / barCount;
            float intensity = value;

            if (animated)
                t = GetAnimatedT(t, animTime, animDir, animSpeed);

            var color = GetThemeColor(options.ColorTheme, t, intensity, options.CustomColor, options.CustomGradientColors);
            byte alpha = (byte)(options.Opacity * 255);

            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), null,
                new Rect(x, centerY - barHeight, Math.Max(1, barWidth - 1), barHeight));
        }

        if (options.ShowPeak && peakLevel > 0.01f)
        {
            var peakColor = GetThemeColor(options.ColorTheme, 0.5f, 1, options.CustomColor, options.CustomGradientColors);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), null,
                new Rect(0, centerY - peakLevel * size.Height * 0.9, size.Width, 2));
        }
    }

    public static float GetAnimatedT(float t, double time, string direction, double speed)
    {
        if (direction == "Wave")
        {
            // Wave: smooth sin oscillation with reflection at boundaries (no hard wrap)
            double phase = time * speed * 2.0 * Math.PI;
            float wave = (float)(Math.Sin(phase) * 0.5);
            float result = t + wave;
            if (result < 0) result = -result;
            if (result > 1) result = 2 - result;
            return result;
        }

        // MoveLeft/MoveRight: triangle wave for smooth boomerang effect
        double raw = direction switch
        {
            "MoveLeft" => t - time * speed * 0.5,
            "MoveRight" => t + time * speed * 0.5,
            _ => t
        };
        float mod = (float)(raw - Math.Floor(raw));
        return mod < 0.5f ? mod * 2 : (1 - mod) * 2;
    }

    public void Reset() { }

    public static Color GetThemeColor(string theme, float t, float intensity, Color customColor, List<Color>? gradientColors = null)
    {
        float brightness = 0.5f + intensity * 0.5f;
        byte br(byte v) => (byte)(v * brightness);
        byte brI(byte v) => (byte)(v * (0.5f + intensity * 0.5f));

        if (theme == "Custom" && gradientColors != null && gradientColors.Count >= 2)
        {
            float pos = t * (gradientColors.Count - 1);
            int idx = (int)pos;
            float frac = pos - idx;
            if (idx >= gradientColors.Count - 1)
                return Color.FromRgb(br(gradientColors.Last().R), br(gradientColors.Last().G), br(gradientColors.Last().B));
            var c1 = gradientColors[idx];
            var c2 = gradientColors[idx + 1];
            byte r = (byte)(c1.R + (c2.R - c1.R) * frac);
            byte g = (byte)(c1.G + (c2.G - c1.G) * frac);
            byte b = (byte)(c1.B + (c2.B - c1.B) * frac);
            return Color.FromRgb(br(r), br(g), br(b));
        }

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
            case 0: r = 1; g = frac;       b = 0; break;
            case 1: r = 1 - frac; g = 1;   b = 0; break;
            case 2: r = 0; g = 1;          b = frac; break;
            case 3: r = 0; g = 1 - frac;   b = 1; break;
            default: r = frac; g = 0;      b = 1; break;
        }

        float brightness = 0.5f + intensity * 0.5f;
        return Color.FromRgb((byte)(r * brightness * 255), (byte)(g * brightness * 255), (byte)(b * brightness * 255));
    }
}
