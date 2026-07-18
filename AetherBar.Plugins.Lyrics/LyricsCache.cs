namespace AetherBar.Plugins.Lyrics;

public sealed class LyricsCache
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    private const int MaxEntries = 20;

    private sealed class CacheEntry
    {
        public List<LyricLine> Lines { get; init; } = [];
        public bool Instrumental { get; init; }
        public DateTime Accessed { get; set; }
    }

    private static string MakeKey(string title, string artist)
    {
        return $"{title.ToLowerInvariant()}|{artist.ToLowerInvariant()}";
    }

    public bool TryGet(string title, string artist, out List<LyricLine>? lines, out bool instrumental)
    {
        var key = MakeKey(title, artist);
        if (_cache.TryGetValue(key, out var entry))
        {
            entry.Accessed = DateTime.UtcNow;
            lines = entry.Lines;
            instrumental = entry.Instrumental;
            return true;
        }

        lines = null;
        instrumental = false;
        return false;
    }

    public void Set(string title, string artist, List<LyricLine> lines, bool instrumental)
    {
        var key = MakeKey(title, artist);

        if (_cache.Count >= MaxEntries)
        {
            var oldest = _cache
                .OrderBy(kv => kv.Value.Accessed)
                .First().Key;
            _cache.Remove(oldest);
        }

        _cache[key] = new CacheEntry
        {
            Lines = lines,
            Instrumental = instrumental,
            Accessed = DateTime.UtcNow
        };
    }

    public void Clear()
    {
        _cache.Clear();
    }
}
