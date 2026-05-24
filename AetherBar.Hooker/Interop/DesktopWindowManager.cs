using System.Runtime.InteropServices;
using static AetherBar.Hooker.Interop.NativeMethods;

namespace AetherBar.Hooker.Interop;

public static class DesktopWindowManager
{
    public const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public const uint DWMWA_MICA_EFFECT = 1029;
    public const uint DWMWA_SYSTEMBACKDROP_TYPE = 38;

    public enum DWM_SYSTEMBACKDROP_TYPE : int
    {
        DWMSBT_AUTO = 0,
        DWMSBT_NONE = 1,
        DWMSBT_MAINWINDOW = 2,
        DWMSBT_TRANSIENTWINDOW = 3,
        DWMSBT_TABBEDWINDOW = 4
    }

    public static void EnableMica(nint hwnd)
    {
        int backdropType = (int)DWM_SYSTEMBACKDROP_TYPE.DWMSBT_MAINWINDOW;
        DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE,
            ref backdropType, Marshal.SizeOf(typeof(int)));
    }

    public static void EnableDarkMode(nint hwnd, bool enable = true)
    {
        bool useDarkMode = enable;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref useDarkMode, Marshal.SizeOf(typeof(bool)));
    }

    public static void EnableAcrylic(nint hwnd)
    {
        var accent = new AccentPolicy
        {
            AccentState = AccentState.ACCENT_ENABLE_ACRYLIC,
            AccentFlags = 2,
            GradientColor = 0x22FFFFFF,
            AnimationId = 0
        };

        var accentStruct = Marshal.AllocHGlobal(Marshal.SizeOf(accent));
        try
        {
            Marshal.StructureToPtr(accent, accentStruct, false);

            var data = new WindowCompositionAttributeData
            {
                Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY,
                Data = accentStruct,
                SizeOfData = Marshal.SizeOf(accent)
            };

            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentStruct);
        }
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, uint dwAttribute, ref bool pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(nint hwnd, uint dwAttribute, ref int pvAttribute, int cbAttribute);
}
