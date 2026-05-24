using System.Windows;
using System.Windows.Media;

namespace AetherBar.Core.Visualizer;

public class DotVisualizer : IVisualizerRenderer
{
    public string Name => "Dot";

    public void Render(DrawingContext context, float[] fftData, float peakLevel, Size size, RenderOptions options)
    {
        if (fftData.Length == 0) return;

        int offset = Math.Min(options.BarStartOffset, fftData.Length - 1);
        int effectiveLength = fftData.Length - offset;
        int totalCells = Math.Min(effectiveLength, options.BarCount);

        int cols = Math.Max(4, (int)(size.Width / (size.Height * 0.7)));
        if (cols > totalCells) cols = totalCells;
        int rows = Math.Max(1, (int)Math.Ceiling((double)totalCells / cols));
        if (rows > 4) { rows = 4; cols = (int)Math.Ceiling((double)totalCells / rows); }

        double cellW = size.Width / cols;
        double cellH = size.Height / rows;
        double maxR = Math.Min(cellW, cellH) * 0.45;
        byte alpha = (byte)(options.Opacity * 255);

        for (int i = 0; i < totalCells; i++)
        {
            int idx = offset + i;
            if (idx >= fftData.Length || idx < 0) break;

            float value = Math.Min(1, fftData[idx] * (float)options.Sensitivity);
            if (value < options.Threshold) continue;

            int x = i % cols;
            int y = i / cols;
            double cx = x * cellW + cellW / 2;
            double cy = y * cellH + cellH / 2;
            double dotR = value * maxR + 0.5;

            var color = BarVisualizer.GetThemeColor(options.ColorTheme, (float)x / cols, value, options.CustomColor);

            // Glow (larger, faint)
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.25), color.R, color.G, color.B)),
                null, new Point(cx, cy), dotR * 2.5, dotR * 2.5);

            // Core dot
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)),
                null, new Point(cx, cy), dotR, dotR);
        }
    }

    public void Reset() { }
}
