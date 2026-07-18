using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace AetherBar.Plugins.Lyrics;

public sealed class SpicetifyWebSocketClient : IDisposable
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private WebSocket? _socket;

    public bool IsConnected => _socket is { State: WebSocketState.Open };

    public string Title { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public string Album { get; private set; } = string.Empty;
    public long DurationMs { get; private set; }
    public bool IsPlaying { get; private set; }

    private long _progressMs;
    private long _progressAnchorTicks;
    private readonly object _lock = new();

    public event EventHandler? TrackChanged;
    public event EventHandler? PlaybackStateChanged;
    public event EventHandler? Connected;
    public event EventHandler? Disconnected;

    private static void Log(string msg)
    {
        try
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AetherBar", "plugin.log");
            System.IO.File.AppendAllText(logPath,
                $"[{DateTime.Now:O}] Lyrics WS: {msg}\r\n");
        }
        catch { }
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _listener = new HttpListener();
        _listener.Prefixes.Add("http://127.0.0.1:9090/");
        _listener.Start();
        _listenTask = ListenLoop(_cts.Token);
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener!.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var old = _socket;
                    _socket = wsContext.WebSocket;
                    old?.Dispose();
                    Log("WebSocket connected, sending GetPlayerState");
                    Connected?.Invoke(this, EventArgs.Empty);
                    _ = SendRequest("GetPlayerState");
                    await ReceiveLoop(_socket, ct);
                    Disconnected?.Invoke(this, EventArgs.Empty);
                    Log("WebSocket disconnected, waiting for new connection...");
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (ObjectDisposedException) { break; }
            catch (HttpListenerException) when (!ct.IsCancellationRequested)
            {
                Log("HttpListener error, retrying in 2s...");
                try { await Task.Delay(2000, ct); } catch { break; }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Log($"ListenLoop error: {ex.Message}, retrying in 2s...");
                try { await Task.Delay(2000, ct); } catch { break; }
            }
        }
    }

    private async Task ReceiveLoop(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            try
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Log("WebSocket closed by remote");
                        return;
                    }
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                var json = sb.ToString();
                HandleMessage(json);
            }
            catch (WebSocketException ex) { Log($"WebSocket error: {ex.Message}"); break; }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { Log($"ReceiveLoop error: {ex.Message}"); break; }
        }
    }

    private void HandleMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var status) && status.GetString() == "ok" &&
                root.TryGetProperty("requestName", out var reqName))
            {
                var req = reqName.GetString();
                if (req == "GetProgress" && root.TryGetProperty("payload", out var progPayload) &&
                    progPayload.TryGetProperty("progress", out var progress))
                {
                    lock (_lock)
                    {
                        _progressMs = progress.GetInt64();
                        _progressAnchorTicks = Stopwatch.GetTimestamp();
                    }
                }
                else if (req == "GetPlayerState" && root.TryGetProperty("payload", out var statePayload))
                {
                    HandlePlayerState(statePayload);
                }
            }
            else if (root.TryGetProperty("eventName", out var eventName))
            {
                var name = eventName.GetString();
                Log($"Event: {name}");
                if (name == "InitialState" || name == "SongChanged")
                {
                    if (root.TryGetProperty("payload", out var payload))
                        HandlePlayerState(payload);
                }
                else if (name == "PlayPauseChanged")
                {
                    if (root.TryGetProperty("payload", out var payload) &&
                        payload.TryGetProperty("isPlaying", out var playing))
                    {
                        lock (_lock)
                        {
                            IsPlaying = playing.GetBoolean();
                            _progressAnchorTicks = Stopwatch.GetTimestamp();
                        }
                        PlaybackStateChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
            else
            {
                Log($"Unknown message format: {json}");
            }
        }
        catch (Exception ex)
        {
            Log($"HandleMessage error: {ex.Message}");
        }
    }

    private void HandlePlayerState(JsonElement payload)
    {
        var changed = false;

        // GetPlayerState wraps track under "item"; SongChanged sends track directly as payload
        var track = payload.TryGetProperty("currentTrack", out var ct) ? ct :
                    payload.TryGetProperty("item", out var item) ? item :
                    payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("name", out _) ? payload :
                    default;

        if (track.ValueKind == JsonValueKind.Object)
        {
            var name = track.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var artistName = "";
            if (track.TryGetProperty("artists", out var artists) && artists.GetArrayLength() > 0)
                artistName = artists[0].TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
            var albumName = track.TryGetProperty("album", out var alb) && alb.TryGetProperty("name", out var albn)
                ? albn.GetString() ?? "" : "";

            if (name != Title || artistName != Artist)
                changed = true;

            Title = name;
            Artist = artistName;
            Album = albumName;

            Log($"Track: {artistName} - {name} (changed={changed})");
        }
        else
        {
            Log("No currentTrack/item in payload");
        }

        if (payload.TryGetProperty("duration", out var dur))
        {
            DurationMs = dur.ValueKind == JsonValueKind.Number
                ? dur.GetInt64()
                : dur.TryGetProperty("milliseconds", out var ms) ? ms.GetInt64() : DurationMs;
        }

        lock (_lock)
        {
            if (payload.TryGetProperty("progress", out var prog))
                _progressMs = prog.GetInt64();
            else if (payload.TryGetProperty("positionAsOfTimestamp", out var pos))
                _progressMs = pos.GetInt64();

            _progressAnchorTicks = Stopwatch.GetTimestamp();

            if (payload.TryGetProperty("isPlaying", out var playing))
                IsPlaying = playing.GetBoolean();
            else if (payload.TryGetProperty("isPaused", out var paused))
                IsPlaying = !paused.GetBoolean();
        }

        Log($"State: playing={IsPlaying} progress={_progressMs}ms duration={DurationMs}ms");

        if (changed)
        {
            Log("Firing TrackChanged");
            TrackChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public TimeSpan GetPosition()
    {
        lock (_lock)
        {
            if (!IsPlaying)
                return TimeSpan.FromMilliseconds(_progressMs);

            var elapsed = Stopwatch.GetElapsedTime(_progressAnchorTicks);
            var pos = _progressMs + (long)elapsed.TotalMilliseconds;
            return TimeSpan.FromMilliseconds(Math.Clamp(pos, 0, DurationMs > 0 ? DurationMs : pos));
        }
    }

    public void RequestProgress()
    {
        _ = SendRequest("GetProgress");
    }

    public void RequestPlayerState()
    {
        _ = SendRequest("GetPlayerState");
    }

    public void RequestPlay() => _ = SendRequest("Play");
    public void RequestPause() => _ = SendRequest("Pause");
    public void RequestTogglePlay() => _ = SendRequest("TogglePlay");
    public void RequestNext() => _ = SendRequest("NextSong");
    public void RequestPrevious() => _ = SendRequest("Back");

    public void RequestSeek(long positionMs)
    {
        _ = SendRequest("Seek", JsonSerializer.Serialize(new { position = positionMs }));
    }

    private async Task SendRequest(string requestName, string? payload = null)
    {
        var socket = _socket;
        if (socket is not { State: WebSocketState.Open }) return;

        var msg = payload != null
            ? $"{{\"requestName\":\"{requestName}\",\"requestId\":\"{Guid.NewGuid():N}\",\"payload\":{payload}}}"
            : $"{{\"requestName\":\"{requestName}\",\"requestId\":\"{Guid.NewGuid():N}\",\"payload\":{{}}}}";

        try
        {
            var bytes = Encoding.UTF8.GetBytes(msg);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _socket?.Dispose();
        _listener?.Stop();
        _listener?.Close();
    }
}
