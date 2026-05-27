using System;
using System.Windows;
using System.Windows.Media;

namespace AetherBar.Core.Visualizer;

public class MirrorVisualizer : IVisualizerRenderer
{
    public string Name => "Mirror";

    public void Render(DrawingContext context, float[] fftData, float peakLevel, Size size, RenderOptions options)
    {
        if (fftData.Length == 0) return;

        var animated = options.AnimatedGradientEnabled;
        var animTime = options.AnimationTime;
        var animDir = options.AnimatedGradientDirection;
        var animSpeed = options.AnimatedGradientSpeed;

        int offset = Math.Min(options.BarStartOffset, fftData.Length - 1);
        int effectiveLength = fftData.Length - offset;
        int barCount = Math.Min(options.BarCount, Math.Min(effectiveLength, (int)(size.Width / 6)));
        double barWidth = size.Width / (barCount * 2);
        double cx = size.Width / 2;
        double centerY = size.Height;
        byte alpha = (byte)(options.Opacity * 255);
        double maxHeight = size.Height * 0.9;

        for (int i = 0; i < barCount; i++)
        {
            int idx = offset + (int)((float)i / barCount * effectiveLength);
            if (idx >= fftData.Length || idx < 0) continue;
            float value = Math.Min(1, fftData[idx] * (float)options.Sensitivity);
            if (value < options.Threshold) continue;
            double barHeight = value * maxHeight;
            if (barHeight < 0.5) continue;

            float t = (float)i / barCount;
            float intensity = value;

            if (animated)
            {
                t = BarVisualizer.GetAnimatedT(t, animTime, animDir, animSpeed);
            }

            var color = BarVisualizer.GetThemeColor(options.ColorTheme, t, intensity, options.CustomColor, options.CustomGradientColors);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            double gap = Math.Max(0.5, barWidth * 0.15);
            double w = Math.Max(1, barWidth - gap);

            // Left side
            double xl = cx - (i + 1) * barWidth;
            context.DrawRectangle(brush, null, new Rect(xl, centerY - barHeight, w, barHeight));

            // Right side
            double xr = cx + i * barWidth;
            context.DrawRectangle(brush, null, new Rect(xr, centerY - barHeight, w, barHeight));
        }

        // Peak: horizontal line across center with glow
        if (options.ShowPeak && peakLevel > 0.01f)
        {
            double peakY = centerY - peakLevel * maxHeight;
            var peakBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255));
            context.DrawRectangle(peakBrush, null, new Rect(cx - 20, peakY, 40, 2));
            var glowBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            context.DrawRectangle(glowBrush, null, new Rect(cx - 30, peakY - 1, 60, 4));
        }
    }

    public void Reset() { }
}
