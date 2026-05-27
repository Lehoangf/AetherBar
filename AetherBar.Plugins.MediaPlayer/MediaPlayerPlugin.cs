using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AetherBar.Plugins.MediaPlayer;

public class MediaPlayerPlugin : IPluginWithSettings
{
    public string Name => "Media Player";
    public string Author => "AetherBar";
    public Version Version => new(1, 0, 0);

    private PluginWidget? _widget;
    private IPluginContext? _context;
    private bool _active;
    private bool _hovered;
    private string _playingColor = "#FFFFFF";
    private string _idleColor = "#666666";
    private string _hoverColor = "#FFFFFF";
    private double _iconSize = 14;
    private double _iconOffset = 0;
    private bool _hideWhenIdle = true;

    public Task InitializeAsync(IPluginContext context)
    {
        _context = context;
        _widget = context.CreateWidget("MediaPlayer", 80, 22);

        _widget.OnMouseHover += OnHover;

        ApplyVisual();

        var mc = context.MediaController;
        if (mc != null)
        {
            mc.StatusChanged += OnStatusChanged;
            UpdateState(mc.CurrentStatus);
        }
        else
        {
            UpdateState(null);
        }

        _widget.OnMouseClick += OnClick;
        return Task.CompletedTask;
    }

    public List<PluginSettingDefinition> GetSettingDefinitions()
    {
        return new List<PluginSettingDefinition>
        {
            new("PlayingColor", "Playing Color", "string", "#FFFFFF",
                "Icon color when media is playing"),
            new("IdleColor", "Idle Color", "string", "#666666",
                "Icon color when no media is playing"),
            new("HoverColor", "Hover Color", "string", "#FFFFFF",
                "Icon color on mouse hover"),
            new("IconSize", "Icon Size", "int", "14",
                "Size of media icons in pixels"),
            new("IconOffset", "Icon Spacing", "int", "0",
                "Spacing between icons in pixels"),
            new("HideWhenIdle", "Hide When Idle", "bool", "true",
                "Auto-hide widget when no media is playing"),
        };
    }

    public void OnSettingChanged(string key, string value)
    {
        switch (key)
        {
            case "PlayingColor":
                _playingColor = string.IsNullOrWhiteSpace(value) ? "#FFFFFF" : value;
                break;
            case "IdleColor":
                _idleColor = string.IsNullOrWhiteSpace(value) ? "#666666" : value;
                break;
            case "HoverColor":
                _hoverColor = string.IsNullOrWhiteSpace(value) ? "#FFFFFF" : value;
                break;
            case "IconSize":
                _iconSize = int.TryParse(value, out var size) && size > 0 ? size : 14;
                break;
            case "IconOffset":
                _iconOffset = int.TryParse(value, out var offset) && offset >= 0 ? offset : 0;
                break;
            case "HideWhenIdle":
                _hideWhenIdle = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                break;
        }
        ApplyVisual();
    }

    private string GetEffectiveColor()
    {
        if (_hovered)
            return _hoverColor;
        if (_active)
            return _playingColor;
        return _idleColor;
    }

    private void ApplyVisual()
    {
        if (_widget == null) return;
        _widget.SetIconColor(GetEffectiveColor());
        _widget.SetIconSize(_iconSize);
        _widget.SetIconSpacing(_iconOffset);
    }

    private void OnHover(bool hovered)
    {
        _hovered = hovered;
        ApplyVisual();
    }

    private void OnStatusChanged(MediaPlaybackStatus status)
    {
        UpdateState(status);
    }

    private void UpdateState(MediaPlaybackStatus? status)
    {
        if (_widget == null) return;

        if (status == MediaPlaybackStatus.Playing)
        {
            _active = true;
            _widget.SetOpacity(1);
            _widget.SetIcons(new List<string> { "prev", "pause", "next" });
        }
        else if (status == MediaPlaybackStatus.Paused)
        {
            _active = true;
            _widget.SetOpacity(1);
            _widget.SetIcons(new List<string> { "prev", "play", "next" });
        }
        else
        {
            _active = false;
            if (_hideWhenIdle)
            {
                _widget.SetOpacity(0);
                _widget.SetIcons(new List<string>());
            }
            else
            {
                _widget.SetOpacity(1);
                _widget.SetIcons(new List<string> { "prev", "play", "next" });
            }
        }
    }

    private void OnClick(string button, double x, double y)
    {
        if (!_active || _widget == null) return;

        var mc = _context?.MediaController;
        if (mc == null) return;

        int segment = (int)(x / (_widget.Width / 3.0));
        _ = (segment switch
        {
            0 => mc.SkipPreviousAsync(),
            1 => mc.PlayPauseAsync(),
            _ => mc.SkipNextAsync(),
        });
    }

    public Task ShutdownAsync()
    {
        var mc = _context?.MediaController;
        if (mc != null)
            mc.StatusChanged -= OnStatusChanged;
        _widget?.Dispose();
        _widget = null;
        return Task.CompletedTask;
    }
}
