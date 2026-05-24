using System.Runtime.InteropServices;
using AetherBar.Core.Models;

namespace AetherBar.Hooker;

public class GameModeDetector : IDisposable
{
    private Timer? _pollTimer;
    private bool _isFullscreen;
    private bool _disposed;

    public event EventHandler<bool>? FullscreenStateChanged;

    public bool IsFullscreen => _isFullscreen;

    public void StartMonitoring(int intervalMs = 1000)
    {
        _pollTimer = new Timer(CheckFullscreen, null, 0, intervalMs);
    }

    private void CheckFullscreen(object? state)
    {
        try
        {
            var foregroundHwnd = GetForegroundWindow();
            if (foregroundHwnd == 0)
            {
                SetFullscreenState(false);
                return;
            }

            GetWindowRect(foregroundHwnd, out RECT fgRect);
            int screenWidth = GetSystemMetrics(78);
            int screenHeight = GetSystemMetrics(79);

            bool isFullscreenNow = fgRect.Right - fgRect.Left >= screenWidth &&
                                   fgRect.Bottom - fgRect.Top >= screenHeight &&
                                   IsTopLevelWindow(foregroundHwnd);

            SetFullscreenState(isFullscreenNow);
        }
        catch
        {
        }
    }

    private void SetFullscreenState(bool isFullscreen)
    {
        if (_isFullscreen != isFullscreen)
        {
            _isFullscreen = isFullscreen;
            FullscreenStateChanged?.Invoke(this, isFullscreen);
        }
    }

    private static bool IsTopLevelWindow(nint hwnd)
    {
        var style = GetWindowLong(hwnd, -16);
        var exStyle = GetWindowLong(hwnd, -20);
        bool hasCaption = (style & 0x00C00000) != 0;
        bool isToolWindow = (exStyle & 0x00000080) != 0;
        return hasCaption && !isToolWindow;
    }

    public void StopMonitoring()
    {
        _pollTimer?.Dispose();
        _pollTimer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        StopMonitoring();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
