using System.IO;
using System.Runtime.InteropServices;
using Windows.Media.Control;
using AetherBar.Core.Models;
using AetherBar.Plugins;

namespace AetherBar.Core.Media;

public class MediaManager : IDisposable, IMediaController
{
    private bool _disposed;
    private Timer? _pollTimer;
    private GlobalSystemMediaTransportControlsSessionManager? _smtcManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

    private TimeSpan _lastKnownPosition;
    private DateTimeOffset _lastPositionTimestamp;
    private bool _positionTracking;

    public event EventHandler<MediaInfo>? MediaInfoChanged;

    MediaPlaybackStatus? IMediaController.CurrentStatus => CurrentMedia.PlaybackStatus;
    event Action<MediaPlaybackStatus>? IMediaController.StatusChanged
    {
        add => _statusChanged += value;
        remove => _statusChanged -= value;
    }
    private Action<MediaPlaybackStatus>? _statusChanged;

    string IMediaController.Title => CurrentMedia.Title;
    string IMediaController.Artist => CurrentMedia.Artist;
    string IMediaController.Album => CurrentMedia.Album;
    TimeSpan IMediaController.Duration => CurrentMedia.Duration;
    TimeSpan IMediaController.Position
    {
        get
        {
            if (_positionTracking && CurrentMedia.PlaybackStatus == MediaPlaybackStatus.Playing)
            {
                var elapsed = DateTimeOffset.UtcNow - _lastPositionTimestamp;
                return _lastKnownPosition + elapsed;
            }
            return CurrentMedia.Position;
        }
    }

    event EventHandler? IMediaController.MediaInfoChanged
    {
        add => _mediaInfoChangedForPlugins += value;
        remove => _mediaInfoChangedForPlugins -= value;
    }
    private EventHandler? _mediaInfoChangedForPlugins;

    public MediaInfo CurrentMedia { get; private set; } = new()
    {
        Title = "No media playing",
        PlaybackStatus = MediaPlaybackStatus.Closed
    };

    public bool StartMonitoring()
    {
        try
        {
            _smtcManager = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
            _smtcManager.CurrentSessionChanged += OnCurrentSessionChanged;
            _currentSession = _smtcManager.GetCurrentSession();
            _pollTimer = new Timer(PollMediaInfo, null, 0, 500);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _currentSession = sender.GetCurrentSession();
    }

    private void PollMediaInfo(object? state)
    {
        try
        {
            var mediaInfo = GetMediaInfoFromSystem();
            if (mediaInfo != null)
            {
                if (!AreSameState(CurrentMedia, mediaInfo))
                {
                    var oldStatus = CurrentMedia.PlaybackStatus;
                    CurrentMedia = mediaInfo;
                    _lastKnownPosition = mediaInfo.Position;
                    _lastPositionTimestamp = DateTimeOffset.UtcNow;
                    _positionTracking = true;
                    if (oldStatus != mediaInfo.PlaybackStatus)
                        _statusChanged?.Invoke(mediaInfo.PlaybackStatus);
                    MediaInfoChanged?.Invoke(this, mediaInfo);
                    _mediaInfoChangedForPlugins?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    CurrentMedia = mediaInfo;
                    _lastKnownPosition = mediaInfo.Position;
                    _lastPositionTimestamp = DateTimeOffset.UtcNow;
                    _positionTracking = true;
                }
            }
            else if (CurrentMedia.PlaybackStatus != MediaPlaybackStatus.Closed)
            {
                var oldStatus = CurrentMedia.PlaybackStatus;
                CurrentMedia = CreateClosedMediaInfo();
                _positionTracking = false;
                _currentSession = null;
                if (oldStatus != MediaPlaybackStatus.Closed)
                    _statusChanged?.Invoke(MediaPlaybackStatus.Closed);
                MediaInfoChanged?.Invoke(this, CurrentMedia);
                _mediaInfoChangedForPlugins?.Invoke(this, EventArgs.Empty);
            }
        }
        catch
        {
        }
    }

    private static bool AreSameState(MediaInfo left, MediaInfo right)
    {
        return left.PlaybackStatus == right.PlaybackStatus &&
               string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
               string.Equals(left.Artist, right.Artist, StringComparison.Ordinal) &&
               string.Equals(left.Album, right.Album, StringComparison.Ordinal);
    }

    private static MediaInfo CreateClosedMediaInfo()
    {
        return new MediaInfo
        {
            Title = "No media playing",
            PlaybackStatus = MediaPlaybackStatus.Closed
        };
    }

    private MediaInfo? GetMediaInfoFromSystem()
    {
        try
        {
            var session = _currentSession;
            if (session == null)
                return null;

            var mediaProps = session.TryGetMediaPropertiesAsync().GetAwaiter().GetResult();
            if (mediaProps == null)
                return null;

            var playbackInfo = session.GetPlaybackInfo();
            var status = playbackInfo.PlaybackStatus switch
            {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaPlaybackStatus.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaPlaybackStatus.Paused,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => MediaPlaybackStatus.Stopped,
                _ => MediaPlaybackStatus.Closed
            };

            byte[]? albumArtBytes = null;
            if (mediaProps.Thumbnail != null)
            {
                using var stream = mediaProps.Thumbnail.OpenReadAsync().GetAwaiter().GetResult();
                if (stream != null)
                {
                    using var memStream = new MemoryStream();
                    stream.AsStreamForRead().CopyTo(memStream);
                    albumArtBytes = memStream.ToArray();
                }
            }

            var timeline = session.GetTimelineProperties();

            return new MediaInfo
            {
                Title = mediaProps.Title ?? "Unknown",
                Artist = mediaProps.Artist ?? "Unknown",
                Album = mediaProps.AlbumTitle ?? string.Empty,
                AlbumArt = albumArtBytes,
                Position = timeline?.Position ?? TimeSpan.Zero,
                Duration = timeline?.EndTime ?? TimeSpan.Zero,
                PlaybackStatus = status
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task PlayPauseAsync()
    {
        try
        {
            if (_currentSession == null) return;
            if (CurrentMedia.PlaybackStatus == MediaPlaybackStatus.Playing)
                await _currentSession.TryPauseAsync();
            else
                await _currentSession.TryPlayAsync();
        }
        catch { }
    }

    public async Task SkipNextAsync()
    {
        try
        {
            if (_currentSession != null)
                await _currentSession.TrySkipNextAsync();
        }
        catch { }
    }

    public async Task SkipPreviousAsync()
    {
        try
        {
            if (_currentSession != null)
                await _currentSession.TrySkipPreviousAsync();
        }
        catch { }
    }

    public void StopMonitoring()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopMonitoring();
        if (_smtcManager != null)
        {
            _smtcManager.CurrentSessionChanged -= OnCurrentSessionChanged;
            _smtcManager = null;
        }
        _currentSession = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
