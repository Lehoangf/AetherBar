using System;
using System.Windows;
using System.Windows.Media;

namespace AetherBar.Core.Visualizer;

public class BlocksVisualizer : IVisualizerRenderer
{
    public string Name => "Blocks";

    public void Render(DrawingContext context, float[] fftData, float peakLevel, Size size, RenderOptions options)
    {
        if (fftData.Length == 0) return;

        var animated = options.AnimatedGradientEnabled;
        var animTime = options.AnimationTime;
        var animDir = options.AnimatedGradientDirection;
        var animSpeed = options.AnimatedGradientSpeed;

        int offset = Math.Min(options.BarStartOffset, fftData.Length - 1);
        int effectiveLength = fftData.Length - offset;
        int barCount = Math.Min(options.BarCount, Math.Min(effectiveLength, (int)(size.Width / 4)));
        double barWidth = size.Width / barCount;
        double maxHeight = size.Height * 0.9;
        int maxBlocks = Math.Max(2, (int)(maxHeight / 4));
        double blockHeight = maxHeight / maxBlocks;
        byte alpha = (byte)(options.Opacity * 255);

        for (int i = 0; i < barCount; i++)
        {
            int idx = offset + (int)((float)i / barCount * effectiveLength);
            if (idx >= fftData.Length || idx < 0) continue;
            float value = Math.Min(1, fftData[idx] * (float)options.Sensitivity);
            if (value < options.Threshold) continue;

            int activeBlocks = (int)Math.Ceiling(value * maxBlocks);
            double x = i * barWidth;
            double w = Math.Max(1, barWidth - 1);

            for (int b = 0; b < activeBlocks; b++)
            {
                double y = size.Height - (b + 1) * blockHeight;
                float bt = (float)b / Math.Max(1, activeBlocks - 1);

                float barPos = (float)i / barCount;
                float intensity = 0.5f + bt * 0.5f;

                if (animated)
                    barPos = BarVisualizer.GetAnimatedT(barPos, animTime, animDir, animSpeed);

                var color = BarVisualizer.GetThemeColor(options.ColorTheme, barPos, intensity, options.CustomColor, options.CustomGradientColors);
                byte a = (byte)(alpha * (0.6f + bt * 0.4f));
                context.DrawRectangle(
                    new SolidColorBrush(Color.FromArgb(a, color.R, color.G, color.B)),
                    null,
                    new Rect(x, y, w, Math.Max(1, blockHeight - 1)));
            }
        }

        // Peak: horizontal line
        if (options.ShowPeak && peakLevel > 0.01f)
        {
            double peakY = size.Height - peakLevel * maxHeight;
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), null,
                new Rect(0, peakY - 1, size.Width, 2));
        }
    }

    public void Reset() { }
}
