namespace AetherBar.Core.Models;

public class TaskbarInfo
{
    public nint TaskbarHwnd { get; set; }
    public int TaskbarWidth { get; set; }
    public int TaskbarHeight { get; set; }
    public int TaskbarX { get; set; }
    public int TaskbarY { get; set; }
    public TaskbarPosition Position { get; set; }
    public int IconCount { get; set; }
    public int TrayIconCount { get; set; }
    public SystemDrawingSize WorkArea { get; set; }
}

public enum TaskbarPosition
{
    Bottom,
    Top,
    Left,
    Right
}

public readonly record struct SystemDrawingSize(int Width, int Height);
