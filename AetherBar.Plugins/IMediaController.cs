namespace AetherBar.Plugins;

public interface IMediaController
{
    MediaPlaybackStatus? CurrentStatus { get; }
    event Action<MediaPlaybackStatus>? StatusChanged;

    string Title { get; }
    string Artist { get; }
    string Album { get; }
    TimeSpan Duration { get; }
    TimeSpan Position { get; }

    event EventHandler? MediaInfoChanged;

    Task PlayPauseAsync();
    Task SkipNextAsync();
    Task SkipPreviousAsync();
}
