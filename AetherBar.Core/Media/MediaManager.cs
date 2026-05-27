using System.IO;
using System.Runtime.InteropServices;
using Windows.Media.Control;
using AetherBar.Core.Models;

namespace AetherBar.Core.Media;

public class MediaManager : IDisposable
{
    private bool _disposed;
    private Timer? _pollTimer;
    private GlobalSystemMediaTransportControlsSession? _currentSession;

    public event EventHandler<MediaInfo>? MediaInfoChanged;

    public MediaInfo CurrentMedia { get; private set; } = new()
    {
        Title = "No media playing",
        PlaybackStatus = MediaPlaybackStatus.Closed
    };

    public bool StartMonitoring()
    {
        try
        {
            _pollTimer = new Timer(PollMediaInfo, null, 0, 1000);
            return true;
        }
        catch
        {
            return false;
        }
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
                    CurrentMedia = mediaInfo;
                    MediaInfoChanged?.Invoke(this, mediaInfo);
                }
            }
            else if (CurrentMedia.PlaybackStatus != MediaPlaybackStatus.Closed)
            {
                CurrentMedia = CreateClosedMediaInfo();
                _currentSession = null;
                MediaInfoChanged?.Invoke(this, CurrentMedia);
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
            var smtcManager = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetAwaiter().GetResult();
            var session = smtcManager.GetCurrentSession();
            if (session == null)
                return null;

            _currentSession = session;

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
        _currentSession = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
