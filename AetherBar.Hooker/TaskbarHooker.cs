using System.Runtime.InteropServices;
using System.Text;
using AetherBar.Core.Models;
using AetherBar.Hooker.Interop;
using static AetherBar.Hooker.Interop.NativeMethods;

namespace AetherBar.Hooker;

public class TaskbarHooker : IDisposable
{
    private nint _taskbarHwnd;
    private nint _trayHwnd;
    private nint _startButtonHwnd;
    private nint _taskSwHwnd;
    private nint _rebarHwnd;
    private nint _embeddedWindowHwnd;
    private bool _disposed;

    public TaskbarInfo CurrentTaskbarInfo { get; private set; } = new();

    public bool IsHooked => _taskbarHwnd != 0;

    public bool Hook()
    {
        _taskbarHwnd = FindWindow(ShellTrayWnd, null);
        if (_taskbarHwnd == 0)
        {
            EnumWindows((hwnd, _) =>
            {
                var sb = new StringBuilder(256);
                GetClassName(hwnd, sb, sb.Capacity);
                if (sb.ToString() == ShellTrayWnd)
                {
                    _taskbarHwnd = hwnd;
                    return false;
                }
                return true;
            }, 0);
        }

        if (_taskbarHwnd == 0)
            return false;

        _trayHwnd = FindWindowEx(_taskbarHwnd, 0, TrayNotifyWnd, null);
        _rebarHwnd = FindWindowEx(_taskbarHwnd, 0, ReBarWindow32, null);
        _startButtonHwnd = FindWindowEx(_taskbarHwnd, 0, "Button", null);
        _taskSwHwnd = FindWindowEx(_rebarHwnd, 0, MSTaskSwWClass, null);

        if (_taskSwHwnd == 0)
        {
            EnumChildWindows(_taskbarHwnd, (child, _) =>
            {
                var sb = new StringBuilder(256);
                GetClassName(child, sb, sb.Capacity);
                var name = sb.ToString();
                if (name.Contains("MSTask") || name.Contains("TaskList"))
                {
                    _taskSwHwnd = child;
                    return false;
                }
                return true;
            }, 0);
        }

        RefreshTaskbarInfo();
        return true;
    }

    public bool EmbedWindow(nint childHwnd)
    {
        if (_taskbarHwnd == 0)
            return false;

        _embeddedWindowHwnd = childHwnd;

        var style = GetWindowLong(childHwnd, GWL_STYLE);
        style &= ~(WS_POPUP);
        style |= WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS;
        SetWindowLong(childHwnd, GWL_STYLE, style);

        var result = SetParent(childHwnd, _taskbarHwnd);
        if (result == 0 && Marshal.GetLastWin32Error() != 0)
        {
            _embeddedWindowHwnd = 0;
            return false;
        }

        PositionEmbeddedWindow();

        ShowWindow(childHwnd, 5);
        SetWindowPos(childHwnd, HWND_TOP, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);

        return true;
    }

    public bool EmbedWindowNextTo(nint childHwnd, nint anchorHwnd, bool after = true, int offsetX = 8)
    {
        if (_taskbarHwnd == 0)
            return false;

        _embeddedWindowHwnd = childHwnd;

        var style = GetWindowLong(childHwnd, GWL_STYLE);
        style &= ~(WS_POPUP);
        style |= WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS;
        SetWindowLong(childHwnd, GWL_STYLE, style);

        var result = SetParent(childHwnd, _taskbarHwnd);
        if (result == 0 && Marshal.GetLastWin32Error() != 0)
        {
            _embeddedWindowHwnd = 0;
            return false;
        }

        // position next to anchor
        if (!GetWindowRect(_taskbarHwnd, out RECT taskbarRect))
            return false;

        RECT anchorRect = default;
        if (anchorHwnd != 0 && IsWindow(anchorHwnd))
            GetWindowRect(anchorHwnd, out anchorRect);
        else
            anchorRect = taskbarRect;

        int widgetWidth = 200;
        if (GetWindowRect(childHwnd, out RECT embeddedRect))
            widgetWidth = embeddedRect.Width;

        int x = after ? anchorRect.Right + offsetX : anchorRect.Left - widgetWidth - offsetX;
        int y = taskbarRect.Top;
        int height = taskbarRect.Bottom - taskbarRect.Top;

        ShowWindow(childHwnd, 5);
        SetWindowPos(childHwnd, HWND_TOP, Math.Max(0, x), Math.Max(0, y), widgetWidth, Math.Max(30, height), SWP_NOACTIVATE);

        return true;
    }

    public void PositionEmbeddedWindow(string position = "Auto", int offsetX = 0)
    {
        if (_embeddedWindowHwnd == 0 || _taskbarHwnd == 0)
            return;

        if (!IsWindow(_embeddedWindowHwnd) || !IsWindow(_taskbarHwnd))
            return;

        RefreshTaskbarInfo();
        var info = CurrentTaskbarInfo;

        if (!GetWindowRect(_taskbarHwnd, out RECT taskbarRect))
            return;
        if (!GetWindowRect(_embeddedWindowHwnd, out RECT embeddedRect))
            return;
        int widgetWidth = embeddedRect.Width;
        if (widgetWidth < 50) widgetWidth = 280;

        int x = 0, y = 0, height = info.TaskbarHeight;

        if (info.Position is TaskbarPosition.Bottom or TaskbarPosition.Top)
        {
            RECT trayRect = default;
            if (_trayHwnd != 0)
                GetWindowRect(_trayHwnd, out trayRect);

            int availableWidth = taskbarRect.Width;
            int startButtonWidth = GetStartButtonWidth();
            int trayWidth = trayRect.Width;

            switch (position)
            {
                case "Left":
                    x = taskbarRect.Left + startButtonWidth + 8 + offsetX;
                    break;
                case "Center":
                    x = taskbarRect.Left + (availableWidth - widgetWidth) / 2 + offsetX;
                    break;
                case "Right":
                    x = taskbarRect.Right - widgetWidth - 8 - offsetX;
                    break;
                default:
                    x = (_trayHwnd != 0 ? trayRect.Left : taskbarRect.Right) - widgetWidth - 8 + offsetX;
                    break;
            }

            y = 0;
            height = taskbarRect.Bottom - taskbarRect.Top;
        }
        else if (info.Position == TaskbarPosition.Left)
        {
            x = 0;
            y = taskbarRect.Bottom - 50 + offsetX;
            height = 48;
        }
        else
        {
            x = taskbarRect.Right - widgetWidth + offsetX;
            y = taskbarRect.Bottom - 50;
            height = 48;
        }

        SetWindowPos(_embeddedWindowHwnd, HWND_TOP,
            Math.Max(0, x), Math.Max(0, y),
            widgetWidth, Math.Max(30, height),
            SWP_NOACTIVATE);
    }

    private int GetStartButtonWidth()
    {
        if (_startButtonHwnd == 0) return 60;
        GetWindowRect(_startButtonHwnd, out RECT r);
        return r.Width > 0 ? r.Width : 60;
    }

    public void Detach()
    {
        if (_embeddedWindowHwnd != 0 && _taskbarHwnd != 0)
        {
            SetParent(_embeddedWindowHwnd, GetDesktopWindow());
            _embeddedWindowHwnd = 0;
        }
    }

    public bool RefreshTaskbarInfo()
    {
        if (_taskbarHwnd == 0)
            return false;

        GetWindowRect(_taskbarHwnd, out RECT taskbarRect);
        var info = new TaskbarInfo
        {
            TaskbarHwnd = _taskbarHwnd,
            TaskbarWidth = taskbarRect.Width,
            TaskbarHeight = taskbarRect.Height,
            TaskbarX = taskbarRect.Left,
            TaskbarY = taskbarRect.Top,
            Position = DetectTaskbarPosition(taskbarRect),
            IconCount = CountTaskbarIcons(),
            TrayIconCount = CountTrayIcons()
        };

        CurrentTaskbarInfo = info;
        return true;
    }

    private TaskbarPosition DetectTaskbarPosition(RECT taskbarRect)
    {
        var screenWidth = GetSystemMetrics(78);
        var screenHeight = GetSystemMetrics(79);

        if (taskbarRect.Width < taskbarRect.Height)
            return taskbarRect.Left == 0 ? TaskbarPosition.Left : TaskbarPosition.Right;

        return taskbarRect.Top == 0 ? TaskbarPosition.Top : TaskbarPosition.Bottom;
    }

    private int CountTaskbarIcons()
    {
        if (_taskSwHwnd == 0) return 0;
        int count = 0;
        EnumChildWindows(_taskSwHwnd, (hwnd, _) =>
        {
            if (IsWindowVisible(hwnd)) count++;
            return true;
        }, 0);
        return count;
    }

    private int CountTrayIcons()
    {
        if (_trayHwnd == 0) return 0;
        int count = 0;
        EnumChildWindows(_trayHwnd, (hwnd, _) =>
        {
            if (IsWindowVisible(hwnd))
                count++;
            return true;
        }, 0);
        return count;
    }

    private static RECT GetRect(nint hwnd)
    {
        GetWindowRect(hwnd, out RECT rect);
        return rect;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

    private static string GetWindowClassName(nint hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public void Dispose()
    {
        if (_disposed) return;
        Detach();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
