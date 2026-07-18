using System.Text.RegularExpressions;

namespace AetherBar.Plugins.Lyrics;

public sealed class LyricLine
{
    public int TimeMs { get; init; }
    public string Text { get; init; } = string.Empty;
}

public static partial class LrcParser
{
    [GeneratedRegex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\]")]
    private static partial Regex TimestampRegex();

    public static List<LyricLine> ParseSynced(string lrcContent)
    {
        var result = new List<LyricLine>();
        if (string.IsNullOrWhiteSpace(lrcContent))
            return result;

        foreach (var rawLine in lrcContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var matches = TimestampRegex().Matches(rawLine);
            if (matches.Count == 0)
                continue;

            var text = TimestampRegex().Replace(rawLine, string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
                continue;

            foreach (Match match in matches)
            {
                var min = int.Parse(match.Groups[1].Value);
                var sec = int.Parse(match.Groups[2].Value);
                var fracStr = match.Groups[3].Value;
                var ms = fracStr.Length == 2
                    ? int.Parse(fracStr) * 10
                    : int.Parse(fracStr);

                result.Add(new LyricLine
                {
                    TimeMs = min * 60_000 + sec * 1_000 + ms,
                    Text = text
                });
            }
        }

        result.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return result;
    }

    public static List<LyricLine> ParseUnsynced(string plainText)
    {
        var result = new List<LyricLine>();
        if (string.IsNullOrWhiteSpace(plainText))
            return result;

        foreach (var rawLine in plainText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            result.Add(new LyricLine { TimeMs = -1, Text = line });
        }

        return result;
    }

    public static int FindCurrentLineIndex(List<LyricLine> lines, int positionMs)
    {
        if (lines.Count == 0)
            return -1;

        var idx = lines.FindLastIndex(l => l.TimeMs >= 0 && l.TimeMs <= positionMs);
        return idx >= 0 ? idx : 0;
    }
}
