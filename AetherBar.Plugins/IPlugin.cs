namespace AetherBar.Plugins;

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
}

public class PluginWidget : IDisposable
{
    public string Name { get; }
    public nint Handle { get; private set; }
    public int Width { get; set; }
    public int Height { get; set; }

    internal PluginWidget(string name, int width, int height)
    {
        Name = name;
        Width = width;
        Height = height;
    }

    internal void SetHandle(nint handle) => Handle = handle;

    public void Dispose()
    {
        if (Handle != 0)
        {
            PInvoke.User32.DestroyWindow(Handle);
            Handle = 0;
        }
    }
}
