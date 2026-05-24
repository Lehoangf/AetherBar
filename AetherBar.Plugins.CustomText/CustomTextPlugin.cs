using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AetherBar.Plugins;

namespace AetherBar.Plugins.CustomText;

public class CustomTextPlugin : IPluginWithSettings
{
    public string Name => "Custom Text";
    public string Author => "AetherBar User";
    public Version Version => new(1, 0, 0);

    private PluginWidget? _widget;
    private string _text = "Hello World";
    private double _fontSize = 11;
    private double _verticalOffset = 0;
    private string _textColor = "#FFFFFF";

    public Task InitializeAsync(IPluginContext context)
    {
        _widget = context.CreateWidget("CustomText", 120, 28);
        ApplyWidgetState();
        return Task.CompletedTask;
    }

    public List<PluginSettingDefinition> GetSettingDefinitions()
    {
        return new List<PluginSettingDefinition>
        {
            new PluginSettingDefinition("Text", "Text Content", "string", "Hello World", "Text to display on the taskbar"),
            new PluginSettingDefinition("FontSize", "Font Size", "int", "11", "Size of the text (e.g., 8 to 24)"),
            new PluginSettingDefinition("VerticalShift", "Vertical Offset (Y)", "int", "0", "Shift text up (-) or down (+) in pixels (e.g., -10 to 10)"),
            new PluginSettingDefinition("TextColor", "Text Color", "string", "#FFFFFF", "Hex color for the text, for example #FFFFFF or #FF4444")
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
        }

        ApplyWidgetState();
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
