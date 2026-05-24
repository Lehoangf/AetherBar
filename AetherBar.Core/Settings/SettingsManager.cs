using System.IO;
using System.Text.Json;

namespace AetherBar.Core.Settings;

public class SettingsManager
{
    private readonly string _filePath;
    private AetherBarSettings _settings;

    public event EventHandler<AetherBarSettings>? SettingsChanged;

    public AetherBarSettings Current => _settings;

    public SettingsManager()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "AetherBar");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "settings.json");
        _settings = new AetherBarSettings();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<AetherBarSettings>(json);
                if (loaded != null)
                {
                    _settings = loaded;

                    // Migrate from old flat format: check if old BarCount existed
                    using var doc = JsonDocument.Parse(json);
                    bool oldFormat = doc.RootElement.TryGetProperty("Visualizer", out var vis) &&
                                     vis.TryGetProperty("BarCount", out _);

                    if (oldFormat && _settings.Visualizer.ModeSettings.Count == 0)
                    {
                        var defaults = new ModeSettings
                        {
                            BarCount = vis.GetProperty("BarCount").GetInt32(),
                            Sensitivity = vis.GetProperty("Sensitivity").GetDouble(),
                            Threshold = vis.GetProperty("Threshold").GetDouble(),
                            BarStartOffset = vis.GetProperty("BarStartOffset").GetInt32()
                        };
                        // Migrate flat color/opacity if they exist
                        if (vis.TryGetProperty("ColorTheme", out var ct))
                            defaults.ColorTheme = ct.GetString() ?? "Rainbow";
                        if (vis.TryGetProperty("Opacity", out var op))
                            defaults.Opacity = op.GetDouble();
                        if (vis.TryGetProperty("ShowPeak", out var sp))
                            defaults.ShowPeak = sp.GetBoolean();
                        if (vis.TryGetProperty("CustomColorR", out var ccr))
                            defaults.CustomColorR = ccr.GetInt32();
                        if (vis.TryGetProperty("CustomColorG", out var ccg))
                            defaults.CustomColorG = ccg.GetInt32();
                        if (vis.TryGetProperty("CustomColorB", out var ccb))
                            defaults.CustomColorB = ccb.GetInt32();
                        foreach (var m in new[] { "Bar", "Line", "Dot", "Circle" })
                            _settings.Visualizer.ModeSettings[m] = new ModeSettings
                            {
                                BarCount = defaults.BarCount,
                                Sensitivity = defaults.Sensitivity,
                                Threshold = defaults.Threshold,
                                BarStartOffset = defaults.BarStartOffset,
                                ColorTheme = defaults.ColorTheme,
                                Opacity = defaults.Opacity,
                                ShowPeak = defaults.ShowPeak,
                                CustomColorR = defaults.CustomColorR,
                                CustomColorG = defaults.CustomColorG,
                                CustomColorB = defaults.CustomColorB
                            };
                    }
                }
            }
        }
        catch
        {
            _settings = new AetherBarSettings();
        }

        // Ensure all 4 modes exist
        foreach (var m in new[] { "Bar", "Line", "Dot", "Circle" })
            if (!_settings.Visualizer.ModeSettings.ContainsKey(m))
                _settings.Visualizer.ModeSettings[m] = new ModeSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_filePath, json);
            SettingsChanged?.Invoke(this, _settings);
        }
        catch
        {
        }
    }

    public T Update<T>(Func<AetherBarSettings, T> updateFunc)
    {
        var result = updateFunc(_settings);
        Save();
        return result;
    }

    public void ResetToDefaults()
    {
        _settings = new AetherBarSettings();
        Save();
    }
}
