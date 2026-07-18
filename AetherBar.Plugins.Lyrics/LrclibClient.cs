using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherBar.Plugins.Lyrics;

public sealed class LrclibResponse
{
    [JsonPropertyName("syncedLyrics")]
    public string? SyncedLyrics { get; set; }

    [JsonPropertyName("plainLyrics")]
    public string? PlainLyrics { get; set; }

    [JsonPropertyName("instrumental")]
    public bool Instrumental { get; set; }
}

public sealed class LrclibClient : IDisposable
{
    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private const string BaseUrl = "https://lrclib.net/api/get";
    private const string UserAgent = "AetherBar/v1 (https://github.com/AetherBar)";

    public async Task<LrclibResponse?> GetLyricsAsync(
        string trackName,
        string artistName,
        string? albumName,
        double durationSeconds,
        CancellationToken ct = default)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["track_name"] = trackName,
                ["artist_name"] = artistName,
                ["duration"] = ((int)durationSeconds).ToString()
            };

            if (!string.IsNullOrWhiteSpace(albumName))
                parameters["album_name"] = albumName;

            var query = string.Join("&",
                parameters.Select(kv =>
                    $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

            var url = $"{BaseUrl}?{query}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", UserAgent);

            using var response = await s_http.SendAsync(request, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<LrclibResponse>(json);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        s_http.Dispose();
    }
}
