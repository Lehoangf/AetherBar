using System.Windows;
using System.Windows.Media;

namespace AetherBar.Core.Visualizer;

public class CircleVisualizer : IVisualizerRenderer
{
    public string Name => "Circle";

    public void Render(DrawingContext context, float[] fftData, float peakLevel, Size size, RenderOptions options)
    {
        if (fftData.Length == 0) return;

        double cx = size.Width / 2;
        double cy = size.Height;
        double radius = Math.Min(cx, size.Height) - 2;
        if (radius < 4) radius = 4;

        int offset = Math.Min(options.BarStartOffset, fftData.Length - 1);
        int effectiveLength = fftData.Length - offset;
        int segments = Math.Min(effectiveLength, options.BarCount);
        if (segments < 3) segments = 3;
        double angleStep = Math.PI / (segments - 1);
        byte alpha = (byte)(options.Opacity * 255);

        // Fan of wedges radiating from bottom center
        for (int i = 0; i < segments; i++)
        {
            int idx = offset + (int)((float)i / segments * effectiveLength);
            if (idx >= fftData.Length || idx < 0) continue;
            float value = Math.Min(1, fftData[idx] * (float)options.Sensitivity);
            if (value < options.Threshold) continue;

            double angle = i * angleStep;
            double barLen = value * radius;

            double x1 = cx + Math.Cos(Math.PI - angle) * 1;
            double y1 = cy - 1;
            double x2 = cx + Math.Cos(Math.PI - angle) * barLen;
            double y2 = cy - Math.Sin(angle) * barLen;

            var color = BarVisualizer.GetThemeColor(options.ColorTheme, (float)i / segments, value, options.CustomColor);

            // Thick wedge line
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B)), 2.5);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            context.DrawLine(pen, new Point(x1, y1), new Point(x2, y2));

            // Glow
            var glowPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.3), color.R, color.G, color.B)), 6);
            glowPen.StartLineCap = PenLineCap.Round;
            glowPen.EndLineCap = PenLineCap.Round;
            context.DrawLine(glowPen, new Point(x1, y1), new Point(x2, y2));
        }

        // Center glow
        if (peakLevel > 0.01f)
        {
            double pulseR = 2 + peakLevel * 4;
            var pulseColor = BarVisualizer.GetThemeColor(options.ColorTheme, 0.5f, peakLevel, options.CustomColor);
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.5), pulseColor.R, pulseColor.G, pulseColor.B)),
                null, new Point(cx, cy), pulseR, pulseR);
        }

        // Subtle arc connecting the tips
        var arcColor = BarVisualizer.GetThemeColor(options.ColorTheme, 0.5f, 0.3f, options.CustomColor);
        var arcPen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.2), arcColor.R, arcColor.G, arcColor.B)), 1);
        var arcGeo = new StreamGeometry();
        using (var arcCtx = arcGeo.Open())
        {
            bool first = true;
            for (int i = 0; i < segments; i++)
            {
                int idx = offset + (int)((float)i / segments * effectiveLength);
                if (idx >= fftData.Length || idx < 0) continue;
                float value = Math.Min(1, fftData[idx] * (float)options.Sensitivity);
                if (value < options.Threshold) { first = true; continue; }
                double angle = i * angleStep;
                double barLen = value * radius;
                double x = cx + Math.Cos(Math.PI - angle) * barLen;
                double y = cy - Math.Sin(angle) * barLen;
                if (first) { arcCtx.BeginFigure(new Point(x, y), false, false); first = false; }
                else arcCtx.LineTo(new Point(x, y), true, false);
            }
        }
        arcGeo.Freeze();
        context.DrawGeometry(null, arcPen, arcGeo);
    }

    public void Reset() { }
}
