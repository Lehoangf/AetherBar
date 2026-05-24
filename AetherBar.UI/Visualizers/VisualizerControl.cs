using System.Windows;
using System.Windows.Media;
using AetherBar.Core.Visualizer;

namespace AetherBar.UI.Visualizers;

public class VisualizerControl : FrameworkElement
{
    private VisualizerController? _controller;
    private float[] _fftData = Array.Empty<float>();
    private float _peakLevel;

    public VisualizerControl()
    {
        SnapsToDevicePixels = true;
    }

    public void SetController(VisualizerController controller)
    {
        _controller = controller;
        _controller.FrameDataReady += OnFrameDataReady;
    }

    private void OnFrameDataReady(float[] fftData, float peakLevel)
    {
        UpdateData(fftData, peakLevel);
    }

    protected override void OnRender(DrawingContext ctx)
    {
        base.OnRender(ctx);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        ctx.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        var controller = _controller;
        var renderer = controller?.CurrentRenderer;
        if (renderer != null && _fftData.Length > 0)
        {
            renderer.Render(ctx, _fftData, _peakLevel, new Size(w, h), controller!.Options);
        }
    }

    public void UpdateData(float[] fftData, float peakLevel)
    {
        _fftData = fftData;
        _peakLevel = peakLevel;
        Dispatcher.Invoke(() => InvalidateVisual(),
            System.Windows.Threading.DispatcherPriority.Render);
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        return new System.Windows.Size(Math.Min(200, availableSize.Width),
                                        Math.Min(30, availableSize.Height));
    }

    public void Cleanup()
    {
        if (_controller != null)
            _controller.FrameDataReady -= OnFrameDataReady;
    }
}
