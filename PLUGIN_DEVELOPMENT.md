# AetherBar Plugin Development

This guide is the shared contract for in-repo plugins. Use the two sample plugins as references:

- `AetherBar.Plugins.CustomText`: configurable text widget with custom settings.
- `AetherBar.Plugins.SampleSystemMonitor`: background timer widget that updates periodically.

## 1. Create a Plugin Project

Create a class library beside the other plugin projects:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\AetherBar.Plugins\AetherBar.Plugin.targets" />
</Project>
```

If the plugin needs NuGet packages, add them before the import:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Some.Package" Version="1.0.0" />
  </ItemGroup>

  <Import Project="..\AetherBar.Plugins\AetherBar.Plugin.targets" />
</Project>
```

`AetherBar.Plugin.targets` sets the target framework, references `AetherBar.Plugins`, and copies the plugin output, dependencies, and `plugin.json` into:

- `AetherBar.UI/bin/<Configuration>/net8.0-windows10.0.26100.0/plugins/`
- `publish/plugins/`

## 2. Add plugin.json

Every plugin project should include a `plugin.json` manifest:

```json
{
  "id": "com.aetherbar.example.myplugin",
  "name": "My Plugin",
  "version": "1.0.0",
  "author": "Your Name",
  "enabled": true
}
```

Keep `name` aligned with the plugin class `Name` property. The settings UI stores plugin layout settings by `Name`.

## 3. Implement IPlugin

Use `IPlugin` for a normal widget:

```csharp
using AetherBar.Plugins;

namespace AetherBar.Plugins.MyPlugin;

public class MyPlugin : IPlugin
{
    public string Name => "My Plugin";
    public string Author => "Your Name";
    public Version Version => new(1, 0, 0);

    private PluginWidget? _widget;

    public Task InitializeAsync(IPluginContext context)
    {
        _widget = context.CreateWidget("MyPlugin", 120, 28);
        _widget.SetContent("Hello");
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        _widget?.Dispose();
        _widget = null;
        return Task.CompletedTask;
    }
}
```

## 4. Add Custom Settings

Use `IPluginWithSettings` when the plugin needs controls in the Settings window. Supported setting types are:

- `string`
- `bool`
- `int`
- `double`

Example:

```csharp
public class MyPlugin : IPluginWithSettings
{
    public List<PluginSettingDefinition> GetSettingDefinitions()
    {
        return new List<PluginSettingDefinition>
        {
            new("Text", "Text Content", "string", "Hello", "Text shown on the taskbar"),
            new("Enabled", "Enabled", "bool", "true")
        };
    }

    public void OnSettingChanged(string key, string value)
    {
        // Store the setting, then update the widget if it already exists.
    }
}
```

The host calls `OnSettingChanged` before initialization to apply saved/default values, and again whenever the user edits the setting.

## 5. Widget API

`PluginWidget` is the host-owned UI surface. A plugin should keep one field for it and update through these methods:

- `SetContent(string text)`: updates the displayed text.
- `SetFontSize(double size)`: updates text size for text-based widgets.
- `SetVerticalOffset(double offset)`: shifts the widget up or down.
- `SetTextColor(string color)`: applies one color to all text. Use hex values such as `#FFFFFF`.
- `SetLineColors(string topColor, string bottomColor)`: applies separate colors to the top and bottom text lines.
- `Dispose()`: releases the widget during shutdown.

The host supports one-line text and simple two-line text. A string with `\n` splits into top and bottom lines. Inline text containing `CPU:` and `RAM:` is also split for the system monitor sample.

## 6. Lifecycle Rules

- Create widgets only inside `InitializeAsync`.
- Dispose timers, counters, subscriptions, and widgets inside `ShutdownAsync`.
- Keep background callbacks small and catch exceptions so one plugin cannot break the host.
- Use `context.Log(message)` for plugin diagnostics. Logs are written under `%LOCALAPPDATA%\AetherBar\plugin.log`.
- Do not write directly to WPF controls. Use `PluginWidget` methods so the host owns threading and layout.

## 7. Recommended Starting Points

- Copy `AetherBar.Plugins.CustomText` for a user-configurable static widget.
- Copy `AetherBar.Plugins.SampleSystemMonitor` for a periodically updating widget.
- Rename the namespace, class, assembly name, and `plugin.json` values.
- Add the new project to `AetherBar.slnx`.
- Run `dotnet build AetherBar.slnx`.
