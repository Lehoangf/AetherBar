using System.Reflection;
using System.Runtime.Loader;

namespace AetherBar.Plugins;

public class PluginManager : IDisposable
{
    private readonly List<IPlugin> _plugins = new();
    private readonly List<PluginLoadContext> _loadContexts = new();

    public event EventHandler<IPlugin>? PluginLoaded;
    public event EventHandler<IPlugin>? PluginUnloaded;

    public IReadOnlyList<IPlugin> Plugins => _plugins.AsReadOnly();

    public bool LoadPlugin(string assemblyPath)
    {
        try
        {
            var loadContext = new PluginLoadContext(assemblyPath);
            _loadContexts.Add(loadContext);

            var assembly = loadContext.LoadFromAssemblyName(
                AssemblyName.GetAssemblyName(assemblyPath));

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
        catch
        {
            return false;
        }
    }

    public async Task InitializeAllAsync(IPluginContext context)
    {
        foreach (var plugin in _plugins)
        {
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
