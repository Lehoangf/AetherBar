namespace AetherBar.Core.Models;

public class MediaInfo
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public byte[]? AlbumArt { get; set; }
    public TimeSpan Position { get; set; }
    public TimeSpan Duration { get; set; }
    public MediaPlaybackStatus PlaybackStatus { get; set; }
}

public enum MediaPlaybackStatus
{
    Closed,
    Changing,
    Stopped,
    Playing,
    Paused
}
