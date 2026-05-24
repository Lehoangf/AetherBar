using System.Runtime.InteropServices;
using AetherBar.Core.Models;
using static AetherBar.Hooker.Interop.NativeMethods;

namespace AetherBar.Hooker;

public class TaskbarWatcher : IDisposable
{
    private readonly TaskbarHooker _hooker;
    private const int ShellHookWindowClassId = 1;

    private const int HSHELL_WINDOWCREATED = 1;
    private const int HSHELL_WINDOWDESTROYED = 2;
    private const int HSHELL_WINDOWACTIVATED = 4;
    private const int HSHELL_REDRAW = 6;
    private const int HSHELL_TASKMAN = 7;
    private const int HSHELL_ENDTASK = 8;

    private const int WH_SHELL = 10;

    private delegate nint ShellHookProc(int nCode, nint wParam, nint lParam);

    private nint _hookHandle;
    private ShellHookProc? _hookProc;
    private bool _disposed;

    public event EventHandler<TaskbarInfo>? TaskbarLayoutChanged;

    public TaskbarWatcher(TaskbarHooker hooker)
    {
        _hooker = hooker;

        _hookProc = ShellHookCallback;

        _hookHandle = SetWindowsHookEx(WH_SHELL, _hookProc,
            PInvoke.Kernel32.GetModuleHandle("user32"), 0);
    }

    private nint ShellHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            switch (nCode)
            {
                case HSHELL_WINDOWCREATED:
                case HSHELL_WINDOWDESTROYED:
                case HSHELL_REDRAW:
                    _hooker.RefreshTaskbarInfo();
                    TaskbarLayoutChanged?.Invoke(this, _hooker.CurrentTaskbarInfo);
                    break;
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_hookHandle != 0)
            UnhookWindowsHookEx(_hookHandle);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, ShellHookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);
}
