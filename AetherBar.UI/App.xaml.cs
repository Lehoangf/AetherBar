using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AetherBar.Core.Settings;
using Wpf.Ui.Appearance;

// Resolve Color/Icon ambiguity: System.Windows.Media.Color used unless qualified
using WpfColor = System.Windows.Media.Color;
using Application = System.Windows.Application;

namespace AetherBar.UI;

public partial class App : Application
{
    private SettingsManager? _settingsManager;
    private SettingsWindow? _settingsWindow;

    public SettingsManager Settings => _settingsManager ??= new SettingsManager();

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        _settingsManager = new SettingsManager();
        _settingsManager.Load();

        ApplyTheme(_settingsManager.Current.Effects.EnableDarkMode);

        if (_settingsManager.Current.General.StartWithWindows)
            SetStartWithWindows(true);

        base.OnStartup(e);
    }

    public void ApplyTheme(bool isDark)
    {
        ApplicationThemeManager.Apply(
            isDark ? ApplicationTheme.Dark : ApplicationTheme.Light,
            Wpf.Ui.Controls.WindowBackdropType.Mica,
            false);

        var r = Resources;
        var s = isDark;
        r["FluentAccent"] = new SolidColorBrush(s ? WpfColor.FromRgb(0xFF, 0x44, 0x44) : WpfColor.FromRgb(0xD1, 0x34, 0x38));
        r["FluentAccentDark"] = new SolidColorBrush(s ? WpfColor.FromRgb(0xCC, 0x00, 0x00) : WpfColor.FromRgb(0xA4, 0x26, 0x2C));
        r["SurfaceColor"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x2D, 0x2D, 0x2D) : WpfColor.FromRgb(0xF0, 0xF0, 0xF0));
        r["SurfaceAltColor"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x3D, 0x3D, 0x3D) : WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        r["TextPrimary"] = new SolidColorBrush(s ? WpfColor.FromRgb(0xFF, 0xFF, 0xFF) : WpfColor.FromRgb(0x1A, 0x1A, 0x1A));
        r["TextSecondary"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x99, 0x99, 0x99) : WpfColor.FromRgb(0x66, 0x66, 0x66));
        r["TextTertiary"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x60, 0x60, 0x60) : WpfColor.FromRgb(0x99, 0x99, 0x99));
        r["CardBackground"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x25, 0x25, 0x25) : WpfColor.FromRgb(0xFF, 0xFF, 0xFF));
        r["CardBorder"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x33, 0x33, 0x33) : WpfColor.FromRgb(0xD0, 0xD0, 0xD0));
        r["CardHover"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x2A, 0x2A, 0x2A) : WpfColor.FromRgb(0xE8, 0xE8, 0xE8));
        r["TabInactive"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x2D, 0x2D, 0x2D) : WpfColor.FromRgb(0xF0, 0xF0, 0xF0));
        r["TabHover"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x35, 0x35, 0x35) : WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        r["TitleBarBg"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x1A, 0x1A, 0x1A) : WpfColor.FromRgb(0xE8, 0xE8, 0xE8));
        r["FooterBg"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x1A, 0x1A, 0x1A) : WpfColor.FromRgb(0xE8, 0xE8, 0xE8));
        r["WindowBackground"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x1E, 0x1E, 0x1E) : WpfColor.FromRgb(0xFA, 0xFA, 0xFA));
        r["CustomColorPanelBg"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x1E, 0x1E, 0x1E) : WpfColor.FromRgb(0xF5, 0xF5, 0xF5));
        r["PopupBackground"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x1E, 0x1E, 0x1E) : WpfColor.FromRgb(0xFF, 0xFF, 0xFF));
        r["PopupBorder"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x3D, 0x3D, 0x3D) : WpfColor.FromRgb(0xD0, 0xD0, 0xD0));
        r["ComboBoxBackground"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x3D, 0x3D, 0x3D) : WpfColor.FromRgb(0xF0, 0xF0, 0xF0));
        r["ComboBoxForeground"] = new SolidColorBrush(s ? WpfColor.FromRgb(0xFF, 0xFF, 0xFF) : WpfColor.FromRgb(0x1A, 0x1A, 0x1A));
        r["ComboBoxHover"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x4D, 0x4D, 0x4D) : WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        r["ComboBoxSelected"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x50, 0x50, 0x50) : WpfColor.FromRgb(0xCC, 0xCC, 0xCC));
        r["ButtonHover"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x5D, 0x5D, 0x5D) : WpfColor.FromRgb(0xD0, 0xD0, 0xD0));
        r["ButtonPressed"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x6D, 0x6D, 0x6D) : WpfColor.FromRgb(0xB0, 0xB0, 0xB0));
        r["TabHeaderBg"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x15, 0x15, 0x15) : WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        r["TabHeaderBorder"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x2A, 0x2A, 0x2A) : WpfColor.FromRgb(0xCC, 0xCC, 0xCC));
        r["SliderArrow"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x99, 0x99, 0x99) : WpfColor.FromRgb(0x66, 0x66, 0x66));
        r["SliderTrack"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x3D, 0x3D, 0x3D) : WpfColor.FromRgb(0xD0, 0xD0, 0xD0));
        r["SliderTrackHover"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x4D, 0x4D, 0x4D) : WpfColor.FromRgb(0xC0, 0xC0, 0xC0));
        r["SliderThumb"] = new SolidColorBrush(s ? WpfColor.FromRgb(0xCC, 0xCC, 0xCC) : WpfColor.FromRgb(0x66, 0x66, 0x66));
        r["SliderThumbHover"] = new SolidColorBrush(s ? WpfColor.FromRgb(0xFF, 0xFF, 0xFF) : WpfColor.FromRgb(0x33, 0x33, 0x33));
        r["SliderTickBar"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x55, 0x55, 0x55) : WpfColor.FromRgb(0xAA, 0xAA, 0xAA));
        r["SliderBorder"] = new SolidColorBrush(s ? WpfColor.FromRgb(0x33, 0x33, 0x33) : WpfColor.FromRgb(0xCC, 0xCC, 0xCC));
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow is not null && _settingsWindow.IsLoaded)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(_settingsManager!);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.ShowDialog();
    }

    private void OnDispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
    }

    private void OnUnhandledException(object sender,
        System.UnhandledExceptionEventArgs e)
    {
    }

    public static void SetStartWithWindows(bool enable)
    {
        const string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "AetherBar";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyName, writable: true);
        if (key is null) return;
        if (enable)
            key.SetValue(valueName, Environment.ProcessPath ?? "");
        else
            key.DeleteValue(valueName, throwOnMissingValue: false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settingsManager?.Save();
        base.OnExit(e);
    }
}
