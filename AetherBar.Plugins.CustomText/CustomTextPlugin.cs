using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AetherBar.Plugins;

namespace AetherBar.Plugins.CustomText;

public class CustomTextPlugin : IPluginWithSettings
{
    public string Name => "Custom Text";
    public string Author => "AetherBar User";
    public Version Version => new(1, 0, 0);

    private PluginWidget? _widget;
    private IPluginContext? _context;
    private string _text = "Hello World";
    private double _fontSize = 11;
    private double _verticalOffset = 0;
    private string _textColor = "#FFFFFF";
    private static readonly string[] Presets = { "Hello World", "Click me!", "Try hovering!", "Double-click!" };
    private int _presetIndex;

    private string _singleClickAction = "nothing";
    private string _singleClickValue = "";
    private string _doubleClickAction = "url";
    private string _doubleClickValue = "https://google.com";
    private string _hoverAction = "tooltip";
    private string _hoverValue = "Custom Text Plugin";
    private bool _hoverChangeColor = true;
    private string _hoverColor = "#FFD700";

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;
        _widget = context.CreateWidget("CustomText", 120, 28);
        ApplyWidgetState();

        _widget.OnMouseClick += OnSingleClick;
        _widget.OnMouseDoubleClick += OnDoubleClick;
        _widget.OnMouseHover += OnHover;

        if (_hoverAction == "tooltip" && !string.IsNullOrEmpty(_hoverValue))
            _widget.SetTooltip(_hoverValue);

        return Task.CompletedTask;
    }

    private void OnSingleClick(string button, double x, double y)
    {
        _context?.Log($"CustomText single-click: {button}");
        if (_singleClickAction == "nothing")
        {
            _presetIndex = (_presetIndex + 1) % Presets.Length;
            _text = Presets[_presetIndex];
            ApplyWidgetState();
        }
        else
        {
            ExecuteAction(_singleClickAction, _singleClickValue);
        }
    }

    private void OnDoubleClick(string button, double x, double y)
    {
        _context?.Log($"CustomText double-click: {button}");
        if (_doubleClickAction == "nothing")
        {
            _presetIndex = 0;
            _text = "Reset!";
            ApplyWidgetState();
        }
        else
        {
            ExecuteAction(_doubleClickAction, _doubleClickValue);
        }
    }

    private void OnHover(bool hovering)
    {
        _context?.Log($"CustomText hover: {hovering}");
        if (_hoverChangeColor)
        {
            _widget?.SetTextColor(hovering ? _hoverColor : _textColor);
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
            new("Text", "Text Content", "string", "Hello World", "Text to display on the taskbar"),
            new("FontSize", "Font Size", "int", "11", "Size of the text (e.g., 8 to 24)"),
            new("VerticalShift", "Vertical Offset (Y)", "int", "0", "Shift text up (-) or down (+) in pixels (e.g., -10 to 10)"),
            new("TextColor", "Text Color", "string", "#FFFFFF", "Hex color for the text"),

            new("SingleClickAction", "Single Click Action", "string", "nothing", "Action on single click", new List<string>{"nothing", "url", "run"}),
            new("SingleClickValue", "Single Click Value", "string", "", "URL or program path"),
            new("DoubleClickAction", "Double Click Action", "string", "url", "Action on double click", new List<string>{"nothing", "url", "run"}),
            new("DoubleClickValue", "Double Click Value", "string", "https://google.com", "URL or program path"),
            new("HoverAction", "Hover Action", "string", "tooltip", "Action on hover", new List<string>{"nothing", "tooltip"}),
            new("HoverValue", "Hover Tooltip Text", "string", "Custom Text Plugin", "Tooltip text on hover"),
            new("HoverColor", "Hover Color", "string", "#FFD700", "Hex color when hovering"),
            new("HoverChangeColor", "Hover Change Color", "bool", "true", "Toggle hover color change")
        };
    }

    public void OnSettingChanged(string key, string value)
    {
        switch (key)
        {
            case "Text":
                _text = value;
                break;
            case "FontSize" when double.TryParse(value, out var size):
                _fontSize = size;
                break;
            case "VerticalShift" when double.TryParse(value, out var offset):
                _verticalOffset = offset;
                break;
            case "TextColor":
                _textColor = value;
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

        ApplyWidgetState();

        if (_widget != null)
        {
            if (_hoverAction == "tooltip" && !string.IsNullOrEmpty(_hoverValue))
                _widget.SetTooltip(_hoverValue);
            else
                _widget.SetTooltip(null);
        }
    }

    public Task ShutdownAsync()
    {
        _widget?.Dispose();
        _widget = null;
        return Task.CompletedTask;
    }

    private void ApplyWidgetState()
    {
        if (_widget == null)
            return;

        _widget.SetContent(_text);
        _widget.SetFontSize(_fontSize);
        _widget.SetVerticalOffset(_verticalOffset);
        _widget.SetTextColor(_textColor);
    }
}
