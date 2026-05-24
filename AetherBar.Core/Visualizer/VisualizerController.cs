using AetherBar.Core.Audio;
using AetherBar.Core.Models;

namespace AetherBar.Core.Visualizer;

public class VisualizerController : IDisposable
{
    private readonly AudioManager _audioManager;
    private readonly Dictionary<string, IVisualizerRenderer> _renderers = new();
    private IVisualizerRenderer? _currentRenderer;
    private AudioData? _lastData;
    private bool _disposed;

    public event Action<float[], float>? FrameDataReady;

    public string CurrentMode { get; private set; } = "Bar";
    public IVisualizerRenderer? CurrentRenderer => _currentRenderer;
    public RenderOptions Options { get; } = new();

    public VisualizerController(AudioManager audioManager)
    {
        _audioManager = audioManager;
        _audioManager.AudioDataAvailable += OnAudioData;

        RegisterRenderer(new BarVisualizer());
        RegisterRenderer(new LineVisualizer());
        RegisterRenderer(new DotVisualizer());
        RegisterRenderer(new CircleVisualizer());

        SetMode("Bar");
    }

    public void RegisterRenderer(IVisualizerRenderer renderer)
    {
        _renderers[renderer.Name] = renderer;
    }

    public void SetMode(string mode)
    {
        if (_renderers.TryGetValue(mode, out var renderer))
        {
            _currentRenderer = renderer;
            CurrentMode = mode;
        }
    }

    public IEnumerable<string> GetAvailableModes() => _renderers.Keys;

    private void OnAudioData(object? sender, AudioData data)
    {
        _lastData = data;
        FrameDataReady?.Invoke(data.FftBuffer, data.PeakLevel);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _audioManager.AudioDataAvailable -= OnAudioData;
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
