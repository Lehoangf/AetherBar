using System;
using System.Windows;
using System.Windows.Media;

namespace AetherBar.Core.Visualizer;

public class LineVisualizer : IVisualizerRenderer
{
    public string Name => "Line";

    public void Render(DrawingContext context, float[] fftData, float peakLevel, Size size, RenderOptions options)
    {
        if (fftData.Length < 2) return;

        var animated = options.AnimatedGradientEnabled;
        var animTime = options.AnimationTime;
        var animDir = options.AnimatedGradientDirection;
        var animSpeed = options.AnimatedGradientSpeed;

        int offset = Math.Min(options.BarStartOffset, fftData.Length - 1);
        int effectiveLength = fftData.Length - offset;
        int pointCount = Math.Min(effectiveLength, options.BarCount);
        if (pointCount < 2) return;
        double stepX = size.Width / (pointCount - 1);
        double baseline = size.Height;

        var values = new float[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            int idx = offset + (int)((float)i / pointCount * effectiveLength);
            float value = 0;
            if (idx >= 0 && idx < fftData.Length)
            {
                value = Math.Min(1, fftData[idx] * (float)options.Sensitivity);
                if (value < (float)options.Threshold) value = 0;
            }
            values[i] = value;
        }

        byte alpha = (byte)(options.Opacity * 255);

        // Filled area under the curve
        var fillPts = new Point[pointCount + 2];
        fillPts[0] = new Point(0, baseline);
        for (int i = 0; i < pointCount; i++)
            fillPts[i + 1] = new Point(i * stepX, baseline - values[i] * baseline * 0.9);
        fillPts[pointCount + 1] = new Point((pointCount - 1) * stepX, baseline);

        var fillGeo = new StreamGeometry();
        using (var fillCtx = fillGeo.Open())
        {
            fillCtx.BeginFigure(fillPts[0], true, true);
            for (int i = 1; i < fillPts.Length; i++)
                fillCtx.LineTo(fillPts[i], true, false);
        }
        fillGeo.Freeze();

        float midT = animated ? BarVisualizer.GetAnimatedT(0.5f, animTime, animDir, animSpeed) : 0.5f;
        var midColor = BarVisualizer.GetThemeColor(options.ColorTheme, midT, 0.6f, options.CustomColor, options.CustomGradientColors);
        var fillBrush = new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.4), midColor.R, midColor.G, midColor.B));
        context.DrawGeometry(fillBrush, null, fillGeo);

        // Line on top
        var linePts = new Point[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            double y = baseline - values[i] * baseline * 0.9;
            linePts[i] = new Point(i * stepX, y);
        }

        var lineGeo = new StreamGeometry();
        using (var lineCtx = lineGeo.Open())
        {
            lineCtx.BeginFigure(linePts[0], false, false);
            for (int i = 1; i < pointCount; i++)
                lineCtx.LineTo(linePts[i], true, false);
        }
        lineGeo.Freeze();

        float lineT = animated ? BarVisualizer.GetAnimatedT(0.5f, animTime, animDir, animSpeed) : 0.5f;
        var lineColor = BarVisualizer.GetThemeColor(options.ColorTheme, lineT, 1, options.CustomColor, options.CustomGradientColors);
        var linePen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, lineColor.R, lineColor.G, lineColor.B)), 2);
        context.DrawGeometry(null, linePen, lineGeo);

        // Glow dots at each point
        for (int i = 0; i < pointCount; i += 3)
        {
            if (values[i] < options.Threshold) continue;
            float t = (float)i / pointCount;
            float intensity = values[i];

            if (animated)
            {
                t = BarVisualizer.GetAnimatedT(t, animTime, animDir, animSpeed);
            }

            var dotColor = BarVisualizer.GetThemeColor(options.ColorTheme, t, intensity, options.CustomColor, options.CustomGradientColors);
            double dotR = 1.5 + values[i] * 1.5;
            context.DrawEllipse(
                new SolidColorBrush(Color.FromArgb((byte)(alpha * 0.8), dotColor.R, dotColor.G, dotColor.B)),
                null, linePts[i], dotR, dotR);
        }

        if (options.ShowPeak && peakLevel > 0.01f)
        {
            var peakY = baseline - peakLevel * baseline * 0.9;
            var peakColor = Color.FromArgb(200, 255, 255, 255);
            context.DrawLine(new Pen(new SolidColorBrush(peakColor), 1),
                new Point(0, peakY), new Point(size.Width, peakY));
        }
    }

    public void Reset() { }
}
