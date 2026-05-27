using System.Windows;
using System.Windows.Media;

namespace AetherBar.Core.Visualizer;

public class RenderOptions
{
    public string ColorTheme { get; set; } = "Rainbow";
    public double Opacity { get; set; } = 1.0;
    public int BarCount { get; set; } = 32;
    public double Sensitivity { get; set; } = 1.0;
    public double Threshold { get; set; } = 0.0;
    public bool ShowPeak { get; set; } = true;
    public int BarStartOffset { get; set; } = 0;
    public Color CustomColor { get; set; } = Color.FromRgb(255, 68, 68);
    public bool AnimatedGradientEnabled { get; set; } = false;
    public string AnimatedGradientDirection { get; set; } = "MoveRight";
    public double AnimatedGradientSpeed { get; set; } = 1.0;
    public double AnimationTime { get; set; } = 0.0;
    public List<Color> CustomGradientColors { get; set; } = new();
}

public interface IVisualizerRenderer
{
    string Name { get; }
    void Render(DrawingContext context, float[] fftData, float peakLevel, Size size, RenderOptions options);
    void Reset();
}
