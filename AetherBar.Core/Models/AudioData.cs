namespace AetherBar.Core.Models;

public class AudioData
{
    public float[] FftBuffer { get; set; } = Array.Empty<float>();
    public float[] WaveformBuffer { get; set; } = Array.Empty<float>();
    public float PeakLevel { get; set; }
    public float RmsLevel { get; set; }
    public int SampleRate { get; set; }
    public int ChannelCount { get; set; }
}
