namespace AetherBar.Plugins;

public interface IMediaController
{
    MediaPlaybackStatus? CurrentStatus { get; }
    event Action<MediaPlaybackStatus>? StatusChanged;
    Task PlayPauseAsync();
    Task SkipNextAsync();
    Task SkipPreviousAsync();
}
