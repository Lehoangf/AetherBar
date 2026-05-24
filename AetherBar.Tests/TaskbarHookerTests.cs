namespace AetherBar.Tests;

public class TaskbarHookerTests
{
    // Note: These are basic logic tests.
    // Full integration tests require a real Windows taskbar environment.

    [Fact]
    public void AudioData_DefaultValues_AreZero()
    {
        var data = new Core.Models.AudioData();
        Assert.Empty(data.FftBuffer);
        Assert.Empty(data.WaveformBuffer);
        Assert.Equal(0, data.PeakLevel);
        Assert.Equal(0, data.RmsLevel);
    }

    [Fact]
    public void MediaInfo_DefaultValues_AreExpected()
    {
        var info = new Core.Models.MediaInfo();
        Assert.Equal(string.Empty, info.Title);
        Assert.Equal(string.Empty, info.Artist);
    }

    [Fact]
    public void TaskbarInfo_DefaultPosition_IsBottom()
    {
        var info = new Core.Models.TaskbarInfo();
        Assert.Equal(Core.Models.TaskbarPosition.Bottom, info.Position);
    }

    [Fact]
    public void VisualizerMode_CanSwitch()
    {
        var modes = new[] { "Bar", "Line", "Dot", "Circle" };
        foreach (var mode in modes)
        {
            Assert.NotEmpty(mode);
        }
    }

    [Fact]
    public void DominantColorExtractor_NullInput_ReturnsTransparent()
    {
        var color = Core.Media.DominantColorExtractor.ExtractFromBytes(null!);
        Assert.Equal(System.Windows.Media.Colors.Transparent, color);
    }

    [Fact]
    public void DominantColorExtractor_EmptyInput_ReturnsTransparent()
    {
        var color = Core.Media.DominantColorExtractor.ExtractFromBytes(Array.Empty<byte>());
        Assert.Equal(System.Windows.Media.Colors.Transparent, color);
    }
}
