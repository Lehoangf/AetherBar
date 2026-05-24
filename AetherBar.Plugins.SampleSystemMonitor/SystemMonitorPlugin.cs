using System.Diagnostics;
using System.Threading;
using AetherBar.Plugins;

namespace AetherBar.Plugins.SampleSystemMonitor;

public class SystemMonitorPlugin : IPluginWithSettings
{
    public string Name => "System Monitor (Sample)";
    public string Author => "AetherBar Team";
    public Version Version => new(1, 0, 0);

    private Timer? _timer;
    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _memCounter;
    private PluginWidget? _widget;
    private IPluginContext? _context;
    private string _cpuColor = "#66D9EF";
    private string _ramColor = "#A6E22E";

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            // Prime the CPU counter
            _cpuCounter.NextValue();

            _widget = context.CreateWidget("SystemMonitor", 200, 28);
            _widget.SetContent("CPU: --  RAM: --");
            ApplyWidgetColors();

            _timer = new Timer(_ => Sample(), null, 1000, 1000);
        }
        catch (Exception ex)
        {
            context.Log($"SystemMonitorPlugin initialization failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    public List<PluginSettingDefinition> GetSettingDefinitions()
    {
        return new List<PluginSettingDefinition>
        {
            new("CpuColor", "CPU Color", "string", "#66D9EF", "Hex color for CPU text, for example #66D9EF"),
            new("RamColor", "RAM Color", "string", "#A6E22E", "Hex color for RAM text, for example #A6E22E")
        };
    }

    public void OnSettingChanged(string key, string value)
    {
        switch (key)
        {
            case "CpuColor":
                _cpuColor = value;
                break;
            case "RamColor":
                _ramColor = value;
                break;
        }

        ApplyWidgetColors();
    }

    private void Sample()
    {
        try
        {
            var cpu = _cpuCounter?.NextValue() ?? 0f;
            var mem = _memCounter?.NextValue() ?? 0f;
            var text = $"CPU: {cpu:0.0}%  RAM: {mem:0.0}%";
            _widget?.SetContent(text);
        }
        catch (Exception ex)
        {
            _context?.Log($"SystemMonitorPlugin sample error: {ex.Message}");
        }
    }

    public Task ShutdownAsync()
    {
        _timer?.Dispose();
        _timer = null;
        _cpuCounter?.Dispose();
        _memCounter?.Dispose();
        _cpuCounter = null;
        _memCounter = null;
        _widget?.Dispose();
        _widget = null;
        return Task.CompletedTask;
    }

    private void ApplyWidgetColors()
    {
        _widget?.SetLineColors(_cpuColor, _ramColor);
    }
}
