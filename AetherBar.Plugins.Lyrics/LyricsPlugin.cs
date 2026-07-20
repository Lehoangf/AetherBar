using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AetherBar.Plugins;

namespace AetherBar.Plugins.Lyrics;

public class LyricsPlugin : IPluginWithSettings
{
    public string Name => "Lyrics";
    public string Author => "AetherBar";
    public Version Version => new(1, 0, 0);

    private PluginWidget? _widget;
    private IPluginContext? _context;
    private IMediaController? _media;

    private readonly LrclibClient _client = new();
    private readonly LyricsCache _cache = new();
    private CancellationTokenSource? _fetchCts;

    private SpicetifyWebSocketClient? _spotify;

    private List<LyricLine> _lines = [];
    private bool _instrumental;
    private int _currentLineIndex = -1;
    private string _lastTrackKey = string.Empty;
    private bool _isPlaying;
    private double _lastPositionMs;

    private double _fontSize = 10;
    private string _textColor = "#FFFFFF";
    private string _syncColor = "#FFD700";
    private string _noLyricsColor = "#888888";
    private int _offsetMs;
    private string _alignment = "center";
    private double _lineHeight = 1.35;
    private bool _autoSize;
    private string _lastWidgetContent = string.Empty;

    private Timer? _syncTimer;
    private Timer? _pollProgressTimer;

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;
        _media = context.MediaController;

        _widget = context.CreateWidget("Lyrics", 300, 28);
        _widget.SetFontSize(_fontSize);
        _widget.SetTextColor(_textColor);
        _widget.SetTextWrapping(true);
        _widget.SetMaxWidth(300);
        _widget.SetTextAlignment(_alignment);
        _widget.SetLineHeight(_lineHeight);
        _widget.SetAutoSize(_autoSize);
        _widget.SetContent("♪ Lyrics");

        StartSpicetify();

        if (_media != null)
        {
            _media.MediaInfoChanged += OnMediaInfoChanged;
            _media.StatusChanged += OnStatusChanged;

            _isPlaying = _media.CurrentStatus == MediaPlaybackStatus.Playing;

            if (_spotify == null || !_spotify.IsConnected)
            {
                var title = _media.Title;
                if (!string.IsNullOrWhiteSpace(title) && title != "No media playing")
                {
                    _context?.Log($"Lyrics: init with active track (SMTC): {_media.Artist} - {title}");
                    _lastTrackKey = $"{title}|{_media.Artist}";
                    _ = FetchLyricsAsync(title, _media.Artist, _media.Album, _media.Duration.TotalSeconds);
                }
            }
        }

        _syncTimer = new Timer(_ => SyncLyricPosition(), null, 100, 100);

        return Task.CompletedTask;
    }

    private void StartSpicetify()
    {
        try
        {
            _spotify = new SpicetifyWebSocketClient();
            _spotify.Connected += (_, _) =>
            {
                _context?.Log("Lyrics: Spicetify connected");
                _spotify.RequestPlayerState();
                _pollProgressTimer?.Dispose();
                _pollProgressTimer = new Timer(_ => _spotify?.RequestProgress(), null, 100, 100);
            };
            _spotify.Disconnected += (_, _) =>
            {
                _context?.Log("Lyrics: Spicetify disconnected, falling back to SMTC");
                _pollProgressTimer?.Dispose();
            };
            _spotify.TrackChanged += (_, _) =>
            {
                var title = _spotify.Title;
                var artist = _spotify.Artist;
                var key = $"{title}|{artist}";
                _context?.Log($"Lyrics: Spicetify track: {artist} - {title}");

                if (key == _lastTrackKey) return;
                _lastTrackKey = key;
                _lines = [];
                _currentLineIndex = -1;
                _lastPositionMs = 0;

                if (!string.IsNullOrWhiteSpace(title))
                    _ = FetchLyricsAsync(title, artist, _spotify.Album, _spotify.DurationMs / 1000.0);
            };
            _spotify.PlaybackStateChanged += (_, _) =>
            {
                _isPlaying = _spotify.IsPlaying;
            };
            _spotify.Start();
            _context?.Log("Lyrics: Spicetify WebSocket server started on port 9090");
        }
        catch (Exception ex)
        {
            _context?.Log($"Lyrics: Spicetify start failed: {ex.Message}, using SMTC only");
            _spotify?.Dispose();
            _spotify = null;
        }
    }

    private void OnMediaInfoChanged(object? sender, EventArgs e)
    {
        if (_media == null) return;

        var title = _media.Title;
        var artist = _media.Artist;
        var key = $"{title}|{artist}";

        _context?.Log($"Lyrics: MediaInfoChanged fired (SMTC): {artist} - {title}");

        if (key == _lastTrackKey)
            return;

        if (_spotify != null && _spotify.IsConnected && key == $"{_spotify.Title}|{_spotify.Artist}")
            return;

        _lastTrackKey = key;
        _lines = [];
        _currentLineIndex = -1;
        _lastPositionMs = 0;

        if (string.IsNullOrWhiteSpace(title) || title == "No media playing")
        {
            UpdateWidget("♪ Lyrics", _noLyricsColor);
            return;
        }

        _ = FetchLyricsAsync(title, artist, _media.Album, _media.Duration.TotalSeconds);
    }

    private void OnStatusChanged(MediaPlaybackStatus status)
    {
        if (_spotify != null && _spotify.IsConnected
            && $"{_spotify.Title}|{_spotify.Artist}" == $"{_media?.Title}|{_media?.Artist}")
            return;
        _isPlaying = status == MediaPlaybackStatus.Playing;
    }

    private async Task FetchLyricsAsync(string title, string artist, string album, double durationSeconds)
    {
        _fetchCts?.Cancel();
        _fetchCts = new CancellationTokenSource();
        var ct = _fetchCts.Token;

        try
        {
            UpdateWidget($"♪ {artist} - {title}", _noLyricsColor);

            if (_cache.TryGet(title, artist, out var cachedLines, out var cachedInstrumental))
            {
                _lines = cachedLines ?? [];
                _instrumental = cachedInstrumental;
                _context?.Log($"Lyrics: cache hit for {artist} - {title}");
                return;
            }

            var response = await _client.GetLyricsAsync(title, artist, album, durationSeconds, ct);

            if (ct.IsCancellationRequested)
                return;

            if (response == null || string.IsNullOrWhiteSpace(response.SyncedLyrics))
            {
                var unsynced = response?.PlainLyrics;
                if (!string.IsNullOrWhiteSpace(unsynced))
                {
                    _lines = LrcParser.ParseUnsynced(unsynced);
                    _instrumental = false;
                    _cache.Set(title, artist, _lines, false);
                    _context?.Log($"Lyrics: unsynced lyrics found for {artist} - {title}");
                    return;
                }

                _lines = [];
                UpdateWidget($"♪ {artist} - {title}", _noLyricsColor);
                _context?.Log($"Lyrics: no lyrics found for {artist} - {title}");
                return;
            }

            if (response.Instrumental)
            {
                _instrumental = true;
                _lines = [new LyricLine { TimeMs = 0, Text = "♪ Instrumental ♪" }];
                _cache.Set(title, artist, _lines, true);
                return;
            }

            _lines = LrcParser.ParseSynced(response.SyncedLyrics);
            _instrumental = false;
            _cache.Set(title, artist, _lines, false);
            _context?.Log($"Lyrics: synced lyrics found for {artist} - {title} ({_lines.Count} lines)");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _context?.Log($"Lyrics: fetch error: {ex.Message}");
        }
    }

    private void SyncLyricPosition()
    {
        try
        {
            SyncLyricPositionCore();
        }
        catch { }
    }

    private void SyncLyricPositionCore()
    {
        if (_widget == null) return;

        var useSpotify = _spotify != null && _spotify.IsConnected
            && $"{_spotify.Title}|{_spotify.Artist}" == $"{_media?.Title}|{_media?.Artist}";
        var title = useSpotify ? _spotify.Title : _media?.Title;
        var artist = useSpotify ? _spotify.Artist : _media?.Artist;

        if (string.IsNullOrWhiteSpace(title) || title == "No media playing")
        {
            UpdateWidget("♪ Lyrics", _noLyricsColor);
            return;
        }

        var key = $"{title}|{artist}";
        if (key != _lastTrackKey)
        {
            _lastTrackKey = key;
            _lines = [];
            _currentLineIndex = -1;
            _lastPositionMs = 0;

            var album = useSpotify ? _spotify.Album : _media?.Album ?? "";
            var durationSec = useSpotify ? _spotify.DurationMs / 1000.0 : _media?.Duration.TotalSeconds ?? 0;
            _ = FetchLyricsAsync(title, artist, album, durationSec);
            return;
        }

        _isPlaying = useSpotify ? _spotify.IsPlaying : _media?.CurrentStatus == MediaPlaybackStatus.Playing;

        if (_lines.Count == 0)
        {
            if (_isPlaying)
                UpdateWidget($"♪ {artist} - {title}", _noLyricsColor);
            return;
        }

        if (_instrumental)
        {
            UpdateWidget("♪ Instrumental ♪", _textColor);
            return;
        }

        var positionMs = (int)(useSpotify
            ? _spotify.GetPosition().TotalMilliseconds
            : _media!.Position.TotalMilliseconds) + _offsetMs;

        var newIdx = LrcParser.FindCurrentLineIndex(_lines, positionMs);
        var seeked = Math.Abs(positionMs - _lastPositionMs) > 2000;
        _lastPositionMs = positionMs;

        if (seeked)
        {
            _currentLineIndex = newIdx;
        }
        else if (newIdx > _currentLineIndex)
        {
            _currentLineIndex = newIdx;
        }

        if (_currentLineIndex >= 0 && _currentLineIndex < _lines.Count)
        {
            UpdateWidget(_lines[_currentLineIndex].Text, _syncColor);
        }
        else
        {
            UpdateWidget("...", _textColor);
        }
    }

    private void UpdateWidget(string content, string color)
    {
        if (_lastWidgetContent == content) return;
        _lastWidgetContent = content;
        _widget?.SetContent(content);
        _widget?.SetTextColor(color);
    }

    public Task ShutdownAsync()
    {
        _fetchCts?.Cancel();
        _fetchCts?.Dispose();

        if (_media != null)
        {
            _media.MediaInfoChanged -= OnMediaInfoChanged;
            _media.StatusChanged -= OnStatusChanged;
        }

        _spotify?.Dispose();
        _spotify = null;

        _syncTimer?.Dispose();
        _syncTimer = null;
        _pollProgressTimer?.Dispose();
        _pollProgressTimer = null;

        _widget?.Dispose();
        _widget = null;

        return Task.CompletedTask;
    }

    public List<PluginSettingDefinition> GetSettingDefinitions()
    {
        return
        [
            new PluginSettingDefinition("AutoSize", "Auto Size", "bool", "false", "Auto-adjust font size to fit available height"),
            new PluginSettingDefinition("FontSize", "Font Size", "int", "10", "Lyrics text size (8-20)"),
            new PluginSettingDefinition("LineHeight", "Line Height", "double", "1.35", "Line height multiplier (0.8-2.5, default 1.35)"),
            new PluginSettingDefinition("Alignment", "Text Alignment", "string", "center", "Text alignment when wrapping", new List<string> { "left", "center", "right" }),
            new PluginSettingDefinition("TextColor", "Text Color", "string", "#FFFFFF", "Default text color"),
            new PluginSettingDefinition("SyncColor", "Synced Line Color", "string", "#FFD700", "Color for current playing line"),
            new PluginSettingDefinition("OffsetMs", "Offset (ms)", "int", "0", "Lyrics offset in milliseconds (positive = lyrics earlier, negative = lyrics later)"),
        ];
    }

    public void OnSettingChanged(string key, string value)
    {
        switch (key)
        {
            case "FontSize" when double.TryParse(value, out var size):
                _fontSize = size;
                _widget?.SetFontSize(_fontSize);
                break;
            case "TextColor":
                _textColor = value;
                break;
            case "SyncColor":
                _syncColor = value;
                break;
            case "OffsetMs" when int.TryParse(value, out var offset):
                _offsetMs = offset;
                _currentLineIndex = -1;
                break;
            case "Alignment":
                _alignment = value;
                _widget?.SetTextAlignment(_alignment);
                break;
            case "LineHeight" when double.TryParse(value, out var lh):
                _lineHeight = lh;
                _widget?.SetLineHeight(_lineHeight);
                break;
            case "AutoSize" when bool.TryParse(value, out var auto):
                _autoSize = auto;
                _widget?.SetAutoSize(_autoSize);
                break;
        }
    }
}
