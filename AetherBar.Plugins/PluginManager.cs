using System.Reflection;
using System.Runtime.Loader;
using System.IO;

namespace AetherBar.Plugins;

public class PluginManager : IDisposable
{
    private readonly List<IPlugin> _plugins = new();
    private readonly List<PluginLoadContext> _loadContexts = new();

    public event EventHandler<IPlugin>? PluginLoaded;
    public event EventHandler<IPlugin>? PluginUnloaded;

    public IReadOnlyList<IPlugin> Plugins => _plugins.AsReadOnly();

    public void LoadPluginsFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var dll in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                LoadPlugin(dll);
            }
            catch
            {
                // ignore individual plugin load failures, but write to host log
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var dir = Path.Combine(appData, "AetherBar");
                    Directory.CreateDirectory(dir);
                    var logPath = Path.Combine(dir, "plugin.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now:O}] Failed to load {dll}\r\n");
                }
                catch { }
            }
        }
    }

    public bool LoadPlugin(string assemblyPath)
    {
        try
        {
            // Load plugin into default load context so it shares host types (PluginWidget, IPluginContext, etc.)
            var assembly = Assembly.LoadFrom(assemblyPath);

            foreach (var type in assembly.GetExportedTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    if (Activator.CreateInstance(type) is IPlugin plugin)
                    {
                        _plugins.Add(plugin);
                        PluginLoaded?.Invoke(this, plugin);
                        return true;
                    }
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(appData, "AetherBar");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "plugin.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] LoadPlugin failed for {assemblyPath}: {ex}\r\n");
            }
            catch { }
            return false;
        }
    }

    public async Task InitializeAllAsync(IPluginContext context)
    {
        foreach (var plugin in _plugins)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(appData, "AetherBar");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "plugin.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] Initializing plugin: {plugin.Name} ({plugin.GetType().FullName})\r\n");
            }
            catch { }

            await plugin.InitializeAsync(context);
        }
    }

    public async Task ShutdownAllAsync()
    {
        foreach (var plugin in _plugins.Reverse<IPlugin>())
        {
            await plugin.ShutdownAsync();
            PluginUnloaded?.Invoke(this, plugin);
        }
        _plugins.Clear();
    }

    public void Dispose()
    {
        foreach (var loadContext in _loadContexts)
        {
            loadContext.Unload();
        }
        _loadContexts.Clear();
        _plugins.Clear();
        GC.SuppressFinalize(this);
    }

    private class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Prefer using the default load context for shared host assemblies so plugin types
            // like AetherBar.Plugins.PluginWidget remain identical between host and plugin.
            if (assemblyName.Name == "AetherBar.Plugins" || assemblyName.Name == "AetherBar.Core")
                return null; // fallback to default context

            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
                return LoadFromAssemblyPath(assemblyPath);
            return null;
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
                return LoadUnmanagedDllFromPath(libraryPath);
            return 0;
        }
    }
}
