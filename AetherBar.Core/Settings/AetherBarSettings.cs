namespace AetherBar.Core.Settings;

public class AetherBarSettings
{
    public VisualizerSettings Visualizer { get; set; } = new();
    public TaskbarSettings Taskbar { get; set; } = new();
    public EffectsSettings Effects { get; set; } = new();
    public GeneralSettings General { get; set; } = new();
}

public class VisualizerSettings
{
    public string Mode { get; set; } = "Bar";
    public Dictionary<string, ModeSettings> ModeSettings { get; set; } = new();
}

public class ModeSettings
{
    public int BarCount { get; set; } = 32;
    public double Sensitivity { get; set; } = 1.0;
    public double Threshold { get; set; } = 0.0;
    public int BarStartOffset { get; set; } = 0;
    public string ColorTheme { get; set; } = "Rainbow";
    public double Opacity { get; set; } = 0.5;
    public bool ShowPeak { get; set; } = true;
    public int CustomColorR { get; set; } = 255;
    public int CustomColorG { get; set; } = 68;
    public int CustomColorB { get; set; } = 68;
}

public class TaskbarSettings
{
    public int WidgetWidth { get; set; } = 180;
    public string Position { get; set; } = "Auto";
    public int OffsetX { get; set; } = 0;
    public bool ShowClock { get; set; } = true;
    public bool ShowMediaInfo { get; set; } = true;
    public bool AutoHide { get; set; } = false;
    public int WidgetPadding { get; set; } = 2;
    public string WidgetTextColor { get; set; } = "Auto";
    public int WidgetTextColorR { get; set; } = 255;
    public int WidgetTextColorG { get; set; } = 255;
    public int WidgetTextColorB { get; set; } = 255;
}

public class EffectsSettings
{
    public string BackgroundEffect { get; set; } = "Transparent";
    public bool AdaptiveTheme { get; set; } = true;
    public int CornerRadius { get; set; } = 4;
    public bool EnableDarkMode { get; set; } = true;
}

public class GeneralSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool StartMinimized { get; set; } = true;
    public bool EnableGameMode { get; set; } = true;
    public bool CheckForUpdates { get; set; } = true;
    public string Language { get; set; } = "en";
}
