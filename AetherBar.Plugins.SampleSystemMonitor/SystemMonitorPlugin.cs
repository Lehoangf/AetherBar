using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
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

    private string _singleClickAction = "url";
    private string _singleClickValue = "https://google.com";
    private string _doubleClickAction = "run";
    private string _doubleClickValue = "notepad.exe";
    private string _hoverAction = "tooltip";
    private string _hoverValue = "System Monitor: click to refresh, double-click for colors";
    private bool _hoverChangeColor = true;
    private string _hoverColor = "#FFFFFF";

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
            _cpuCounter.NextValue();

            _widget = context.CreateWidget("SystemMonitor", 200, 28);
            _widget.SetContent("CPU: --  RAM: --");
            ApplyWidgetColors();

            _widget.OnMouseClick += OnSingleClick;
            _widget.OnMouseDoubleClick += OnDoubleClick;
            _widget.OnMouseHover += OnHover;

            if (_hoverAction == "tooltip" && !string.IsNullOrEmpty(_hoverValue))
                _widget.SetTooltip(_hoverValue);

            _timer = new Timer(_ => Sample(), null, 1000, 1000);
        }
        catch (Exception ex)
        {
            context.Log($"SystemMonitorPlugin initialization failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private void OnSingleClick(string button, double x, double y)
    {
        _context?.Log($"SystemMonitor single-click: {button}");
        ExecuteAction(_singleClickAction, _singleClickValue);
    }

    private void OnDoubleClick(string button, double x, double y)
    {
        _context?.Log($"SystemMonitor double-click: {button}");
        ExecuteAction(_doubleClickAction, _doubleClickValue);
    }

    private void OnHover(bool hovering)
    {
        _context?.Log($"SystemMonitor hover: {hovering}");
        if (_hoverChangeColor)
        {
            if (hovering)
                _widget?.SetLineColors(_hoverColor, _hoverColor);
            else
                ApplyWidgetColors();
        }
    }

    private static void ExecuteAction(string action, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        try
        {
            switch (action)
            {
                case "url":
                    Process.Start(new ProcessStartInfo { FileName = value, UseShellExecute = true });
                    break;
                case "run":
                    Process.Start(new ProcessStartInfo { FileName = value, UseShellExecute = true });
                    break;
            }
        }
        catch { }
    }

    public List<PluginSettingDefinition> GetSettingDefinitions()
    {
        return new List<PluginSettingDefinition>
        {
            new("CpuColor", "CPU Color", "string", "#66D9EF", "Hex color for CPU text"),
            new("RamColor", "RAM Color", "string", "#A6E22E", "Hex color for RAM text"),
            new("SingleClickAction", "Single Click Action", "string", "url", "Action on single click", new List<string>{"nothing", "url", "run"}),
            new("SingleClickValue", "Single Click Value", "string", "https://google.com", "URL or program path"),
            new("DoubleClickAction", "Double Click Action", "string", "run", "Action on double click", new List<string>{"nothing", "url", "run"}),
            new("DoubleClickValue", "Double Click Value", "string", "notepad.exe", "URL or program path"),
            new("HoverAction", "Hover Action", "string", "tooltip", "Action on hover", new List<string>{"nothing", "tooltip"}),
            new("HoverValue", "Hover Tooltip Text", "string", "System Monitor: click to refresh, double-click for colors", "Tooltip text on hover"),
            new("HoverColor", "Hover Color", "string", "#FFFFFF", "Hex color when hovering"),
            new("HoverChangeColor", "Hover Change Color", "bool", "true", "Toggle hover color change")
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
            case "SingleClickAction":
                _singleClickAction = value;
                break;
            case "SingleClickValue":
                _singleClickValue = value;
                break;
            case "DoubleClickAction":
                _doubleClickAction = value;
                break;
            case "DoubleClickValue":
                _doubleClickValue = value;
                break;
            case "HoverAction":
                _hoverAction = value;
                break;
            case "HoverValue":
                _hoverValue = value;
                break;
            case "HoverColor":
                _hoverColor = value;
                break;
            case "HoverChangeColor":
                _hoverChangeColor = value == "true";
                break;
        }

        ApplyWidgetColors();

        if (_widget != null)
        {
            if (_hoverAction == "tooltip" && !string.IsNullOrEmpty(_hoverValue))
                _widget.SetTooltip(_hoverValue);
            else
                _widget.SetTooltip(null);
        }
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
