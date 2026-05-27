namespace AetherBar.Plugins;

using System.IO;

public interface IPlugin
{
    string Name { get; }
    string Author { get; }
    Version Version { get; }
    Task InitializeAsync(IPluginContext context);
    Task ShutdownAsync();
}

public interface IPluginContext
{
    nint TaskbarHwnd { get; }
    PluginWidget CreateWidget(string name, int width, int height);
    void Log(string message);
    IMediaController? MediaController { get; }
}

public class PluginWidget : IDisposable
{
    public string Name { get; }
    public nint Handle { get; private set; }
    public int Width { get; set; }
    public int Height { get; set; }
    private Action<string>? _updateContent;
    private Action<double>? _updateFontSize;
    private Action<double>? _updateVerticalOffset;
    private Action<string>? _updateTextColor;
    private Action<string, string>? _updateLineColors;
    private Action<string?>? _updateTooltip;
    private Action<double>? _updateOpacity;
    private Action<IList<string>>? _updateIcons;
    private Action<string>? _updateIconColor;
    private Action<double>? _updateIconSize;
    private Action<double>? _updateIconSpacing;

    public Action<string, double, double>? OnMouseClick { get; set; }
    public Action<string, double, double>? OnMouseDoubleClick { get; set; }
    public Action<bool>? OnMouseHover { get; set; }

    public PluginWidget(
        string name, 
        int width, 
        int height, 
        Action<string>? updateContent = null, 
        Action<double>? updateFontSize = null,
        Action<double>? updateVerticalOffset = null,
        Action<string>? updateTextColor = null,
        Action<string, string>? updateLineColors = null,
        Action<string?>? updateTooltip = null,
        Action<double>? updateOpacity = null,
        Action<IList<string>>? updateIcons = null,
        Action<string>? updateIconColor = null,
        Action<double>? updateIconSize = null,
        Action<double>? updateIconSpacing = null)
    {
        Name = name;
        Width = width;
        Height = height;
        _updateContent = updateContent;
        _updateFontSize = updateFontSize;
        _updateVerticalOffset = updateVerticalOffset;
        _updateTextColor = updateTextColor;
        _updateLineColors = updateLineColors;
        _updateTooltip = updateTooltip;
        _updateOpacity = updateOpacity;
        _updateIcons = updateIcons;
        _updateIconColor = updateIconColor;
        _updateIconSize = updateIconSize;
        _updateIconSpacing = updateIconSpacing;
    }

    public void SetHandle(nint handle) => Handle = handle;

    public void SetContent(string text)
    {
        try
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(appData, "AetherBar");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "plugin.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] PluginWidget.SetContent called => {text}\r\n");
            }
            catch { }

            _updateContent?.Invoke(text);
        }
        catch
        {
        }
    }

    public void SetFontSize(double size)
    {
        try
        {
            _updateFontSize?.Invoke(size);
        }
        catch
        {
        }
    }

    public void SetVerticalOffset(double offset)
    {
        try
        {
            _updateVerticalOffset?.Invoke(offset);
        }
        catch
        {
        }
    }

    public void SetTextColor(string color)
    {
        try
        {
            _updateTextColor?.Invoke(color);
        }
        catch
        {
        }
    }

    public void SetLineColors(string topColor, string bottomColor)
    {
        try
        {
            _updateLineColors?.Invoke(topColor, bottomColor);
        }
        catch
        {
        }
    }

    public void SetTooltip(string? text)
    {
        try
        {
            _updateTooltip?.Invoke(text);
        }
        catch
        {
        }
    }

    public void SetOpacity(double opacity)
    {
        try
        {
            _updateOpacity?.Invoke(opacity);
        }
        catch
        {
        }
    }

    public void SetIcons(IList<string> iconNames)
    {
        try
        {
            _updateIcons?.Invoke(iconNames);
        }
        catch
        {
        }
    }

    public void SetIconColor(string color)
    {
        try
        {
            _updateIconColor?.Invoke(color);
        }
        catch
        {
        }
    }

    public void SetIconSize(double size)
    {
        try
        {
            _updateIconSize?.Invoke(size);
        }
        catch
        {
        }
    }

    public void SetIconSpacing(double spacing)
    {
        try
        {
            _updateIconSpacing?.Invoke(spacing);
        }
        catch
        {
        }
    }

    public void SetOnClickCallback(Action<string, double, double>? onClick)
    {
        OnMouseClick = onClick;
    }

    public void SetOnDoubleClickCallback(Action<string, double, double>? onDoubleClick)
    {
        OnMouseDoubleClick = onDoubleClick;
    }

    public void SetOnHoverCallback(Action<bool>? onHover)
    {
        OnMouseHover = onHover;
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            PInvoke.User32.DestroyWindow(Handle);
            Handle = 0;
        }
    }
}

public class PluginSettingDefinition
{
    public string Key { get; }
    public string DisplayName { get; }
    public string Type { get; } // "string", "bool", "int", "double"
    public string DefaultValue { get; }
    public string? Description { get; }
    public List<string>? Options { get; }

    public PluginSettingDefinition(string key, string displayName, string type, string defaultValue, string? description = null, List<string>? options = null)
    {
        Key = key;
        DisplayName = displayName;
        Type = type;
        DefaultValue = defaultValue;
        Description = description;
        Options = options;
    }
}

public interface IPluginWithSettings : IPlugin
{
    List<PluginSettingDefinition> GetSettingDefinitions();
    void OnSettingChanged(string key, string value);
}
