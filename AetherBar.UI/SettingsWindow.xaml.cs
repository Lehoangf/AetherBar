using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using AetherBar.Core.Settings;
using AetherBar.Core.Visualizer;

namespace AetherBar.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private bool _loading;

    public SettingsWindow(SettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
        _loading = true;

        // Set tool window style to hide from Alt+Tab
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        };

        InitializeComponent();
        LoadSettings();
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Re-apply tool window style after activation
        if (IsLoaded)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private void LoadSettings()
    {
        _loading = true;
        var s = _settingsManager.Current;

        PopulateCombo(VisualizerModeCombo, "Bar", "Line", "Dot", "Circle", "Mirror", "Blocks");
        VisualizerModeCombo.SelectionChanged += OnVisualizerModeChanged;
        PopulateCombo(ColorThemeCombo, "Rainbow", "Neon Blue", "Matrix Green", "Fire", "Monochrome", "Sunset", "Ocean", "Cyberpunk", "Custom");
        PopulateCombo(PositionCombo, "Left", "Center", "Right", "Auto");
        PopulateCombo(BgEffectCombo, "None", "Acrylic (Blur)", "Mica");
        PopulateCombo(WidgetTextColorCombo, "Auto", "White", "Black", "Red", "Green", "Blue", "Cyan", "Yellow", "Custom");

        BlockComboScroll(VisualizerModeCombo);
        BlockComboScroll(ColorThemeCombo);
        BlockComboScroll(PositionCombo);
        BlockComboScroll(BgEffectCombo);
        BlockComboScroll(WidgetTextColorCombo);

        SelectItem(VisualizerModeCombo, s.Visualizer.Mode);
        var mode = GetSelected(VisualizerModeCombo);
        s.Visualizer.ModeSettings.TryGetValue(mode, out var ms);
        SelectItem(ColorThemeCombo, ms?.ColorTheme ?? "Rainbow");
        CustomRSlider.Value = ms?.CustomColorR ?? 0;
        CustomGSlider.Value = ms?.CustomColorG ?? 204;
        CustomBSlider.Value = ms?.CustomColorB ?? 255;
        UpdateColorPreview();
        OpacitySlider.Value = ms?.Opacity ?? 0.5;
        BarCountSlider.Value = ms?.BarCount ?? 32;
        SensitivitySlider.Value = ms?.Sensitivity ?? 1.0;
        ThresholdSlider.Value = ms?.Threshold ?? 0.0;
        BarStartOffsetSlider.Value = ms?.BarStartOffset ?? 0;
        ShowPeakCheck.IsChecked = ms?.ShowPeak ?? true;

        SelectItem(PositionCombo, s.Taskbar.Position);
        WidthSlider.Value = s.Taskbar.WidgetWidth;
        OffsetSlider.Value = s.Taskbar.OffsetX;
        PaddingSlider.Value = s.Taskbar.WidgetPadding;
        ShowMediaInfoCheck.IsChecked = s.Taskbar.ShowMediaInfo;
        AutoHideCheck.IsChecked = s.Taskbar.AutoHide;
        SelectItem(WidgetTextColorCombo, s.Taskbar.WidgetTextColor);
        WidgetTextRSlider.Value = s.Taskbar.WidgetTextColorR;
        WidgetTextGSlider.Value = s.Taskbar.WidgetTextColorG;
        WidgetTextBSlider.Value = s.Taskbar.WidgetTextColorB;
        UpdateWidgetTextColorPanel();
        UpdateWidgetTextPreview();

        SelectItem(BgEffectCombo, s.Effects.BackgroundEffect);
        CornerRadiusSlider.Value = s.Effects.CornerRadius;
        AdaptiveThemeCheck.IsChecked = s.Effects.AdaptiveTheme;
        DarkModeCheck.IsChecked = s.Effects.EnableDarkMode;

        StartWithWindowsCheck.IsChecked = s.General.StartWithWindows;
        StartMinimizedCheck.IsChecked = s.General.StartMinimized;
        GameModeCheck.IsChecked = s.General.EnableGameMode;
        UpdateCheck.IsChecked = s.General.CheckForUpdates;
        UpdateSliderValueLabels();
        _loading = false;
    }

    private static void PopulateCombo(ComboBox combo, params string[] items)
    {
        combo.Items.Clear();
        foreach (var item in items)
            combo.Items.Add(new ComboBoxItem { Content = item });
    }

    private void ApplyAppTheme()
    {
        bool isDark = DarkModeCheck.IsChecked ?? true;
        if (Application.Current is App app)
        {
            app.ApplyTheme(isDark);
        }

        // Force this window's DynamicResource bindings to re-evaluate
        var r = Resources;
        if (isDark)
        {
            r["WindowBackground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            r["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            r["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
            r["TextTertiary"] = new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60));
            r["CardBackground"] = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25));
            r["CardBorder"] = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            r["TitleBarBg"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            r["SurfaceAltColor"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
            r["CustomColorPanelBg"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            r["SliderTrack"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
            r["SliderThumb"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            r["SliderBorder"] = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
            r["SliderTickBar"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            r["ComboBoxBackground"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
            r["ComboBoxForeground"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            r["PopupBackground"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            r["PopupBorder"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x3D, 0x3D));
        }
        else
        {
            r["WindowBackground"] = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA));
            r["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            r["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
            r["TextTertiary"] = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
            r["CardBackground"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            r["CardBorder"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
            r["TitleBarBg"] = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            r["SurfaceAltColor"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            r["CustomColorPanelBg"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
            r["SliderTrack"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
            r["SliderThumb"] = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
            r["SliderBorder"] = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));
            r["SliderTickBar"] = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
            r["ComboBoxBackground"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
            r["ComboBoxForeground"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A));
            r["PopupBackground"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
            r["PopupBorder"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD0, 0xD0));
        }
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        _settingsManager.Update(s =>
        {
            var mode = GetSelected(VisualizerModeCombo);
            s.Visualizer.Mode = mode;
            if (!s.Visualizer.ModeSettings.ContainsKey(mode))
                s.Visualizer.ModeSettings[mode] = new ModeSettings();
            s.Visualizer.ModeSettings[mode].ColorTheme = GetSelected(ColorThemeCombo);
            s.Visualizer.ModeSettings[mode].CustomColorR = (int)CustomRSlider.Value;
            s.Visualizer.ModeSettings[mode].CustomColorG = (int)CustomGSlider.Value;
            s.Visualizer.ModeSettings[mode].CustomColorB = (int)CustomBSlider.Value;
            UpdateColorPreview();
            s.Visualizer.ModeSettings[mode].Opacity = OpacitySlider.Value;
            s.Visualizer.ModeSettings[mode].BarCount = (int)BarCountSlider.Value;
            s.Visualizer.ModeSettings[mode].Sensitivity = SensitivitySlider.Value;
            s.Visualizer.ModeSettings[mode].Threshold = ThresholdSlider.Value;
            s.Visualizer.ModeSettings[mode].BarStartOffset = (int)BarStartOffsetSlider.Value;
            s.Visualizer.ModeSettings[mode].ShowPeak = ShowPeakCheck.IsChecked ?? true;

            s.Taskbar.Position = GetSelected(PositionCombo);
            s.Taskbar.WidgetWidth = (int)WidthSlider.Value;
            s.Taskbar.OffsetX = (int)OffsetSlider.Value;
            s.Taskbar.WidgetPadding = (int)PaddingSlider.Value;
            s.Taskbar.ShowMediaInfo = ShowMediaInfoCheck.IsChecked ?? true;
            s.Taskbar.AutoHide = AutoHideCheck.IsChecked ?? false;
            s.Taskbar.WidgetTextColor = GetSelected(WidgetTextColorCombo);
            s.Taskbar.WidgetTextColorR = (int)WidgetTextRSlider.Value;
            s.Taskbar.WidgetTextColorG = (int)WidgetTextGSlider.Value;
            s.Taskbar.WidgetTextColorB = (int)WidgetTextBSlider.Value;
            UpdateWidgetTextColorPanel();
            UpdateWidgetTextPreview();

            s.Effects.BackgroundEffect = GetSelected(BgEffectCombo);
            s.Effects.CornerRadius = (int)CornerRadiusSlider.Value;
            s.Effects.AdaptiveTheme = AdaptiveThemeCheck.IsChecked ?? true;
            s.Effects.EnableDarkMode = DarkModeCheck.IsChecked ?? true;

            s.General.StartWithWindows = StartWithWindowsCheck.IsChecked ?? false;
            s.General.StartMinimized = StartMinimizedCheck.IsChecked ?? true;
            s.General.EnableGameMode = GameModeCheck.IsChecked ?? true;
            s.General.CheckForUpdates = UpdateCheck.IsChecked ?? true;

            return s;
        });

        App.SetStartWithWindows(StartWithWindowsCheck.IsChecked ?? false);
        ApplyAppTheme();
        UpdateSliderValueLabels();

        if (Application.Current.MainWindow is MainWindow mw)
            mw.RefreshSettings();
    }

    private void UpdateWidgetTextColorPanel()
    {
        WidgetTextColorPanel.Visibility = GetSelected(WidgetTextColorCombo) == "Custom"
            ? Visibility.Visible : Visibility.Collapsed;
        WidgetTextRText.Text = ((int)WidgetTextRSlider.Value).ToString();
        WidgetTextGText.Text = ((int)WidgetTextGSlider.Value).ToString();
        WidgetTextBText.Text = ((int)WidgetTextBSlider.Value).ToString();
    }

    private void UpdateWidgetTextPreview()
    {
        var mode = GetSelected(WidgetTextColorCombo);
        var r = (byte)WidgetTextRSlider.Value;
        var g = (byte)WidgetTextGSlider.Value;
        var b = (byte)WidgetTextBSlider.Value;
        var (pr, pg, pb) = mode switch
        {
            "White"  => ((byte)0xFF, (byte)0xFF, (byte)0xFF),
            "Black"  => ((byte)0x00, (byte)0x00, (byte)0x00),
            "Red"    => ((byte)0xFF, (byte)0x44, (byte)0x44),
            "Green"  => ((byte)0x44, (byte)0xFF, (byte)0x44),
            "Blue"   => ((byte)0x44, (byte)0x44, (byte)0xFF),
            "Cyan"   => ((byte)0x44, (byte)0xFF, (byte)0xFF),
            "Yellow" => ((byte)0xFF, (byte)0xFF, (byte)0x44),
            "Custom" => (r, g, b),
            _ => ((byte)0xBB, (byte)0xBB, (byte)0xBB),
        };
        WidgetTextColorPreview.Background = new SolidColorBrush(Color.FromRgb(pr, pg, pb));
    }

    private void UpdateSliderValueLabels()
    {
        OpacityValue.Text = OpacitySlider.Value.ToString("0.0");
        BarCountValue.Text = ((int)BarCountSlider.Value).ToString();
        SensitivityValue.Text = SensitivitySlider.Value.ToString("0.0");
        ThresholdValue.Text = ThresholdSlider.Value.ToString("0.00");
        BarStartOffsetValue.Text = ((int)BarStartOffsetSlider.Value).ToString();
        WidthValue.Text = ((int)WidthSlider.Value).ToString();
        OffsetValue.Text = ((int)OffsetSlider.Value).ToString();
        PaddingValue.Text = ((int)PaddingSlider.Value).ToString();
        CornerRadiusValue.Text = ((int)CornerRadiusSlider.Value).ToString();
    }

    private void UpdateColorPreview()
    {
        var theme = GetSelected(ColorThemeCombo);
        bool isCustom = theme == "Custom";
        CustomColorPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

        if (isCustom)
        {
            var r = (byte)CustomRSlider.Value;
            var g = (byte)CustomGSlider.Value;
            var b = (byte)CustomBSlider.Value;
            ColorPreview.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
            CustomRText.Text = r.ToString();
            CustomGText.Text = g.ToString();
            CustomBText.Text = b.ToString();
        }
        else
        {
            var cc = Color.FromRgb((byte)CustomRSlider.Value, (byte)CustomGSlider.Value, (byte)CustomBSlider.Value);
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };

            if (theme == "Rainbow")
            {
                grad.GradientStops.Add(new GradientStop(BarVisualizer.GetThemeColor(theme, 0f, 0.8f, cc), 0.00));
                grad.GradientStops.Add(new GradientStop(BarVisualizer.GetThemeColor(theme, 0.2f, 0.8f, cc), 0.20));
                grad.GradientStops.Add(new GradientStop(BarVisualizer.GetThemeColor(theme, 0.4f, 0.8f, cc), 0.40));
                grad.GradientStops.Add(new GradientStop(BarVisualizer.GetThemeColor(theme, 0.6f, 0.8f, cc), 0.60));
                grad.GradientStops.Add(new GradientStop(BarVisualizer.GetThemeColor(theme, 0.8f, 0.8f, cc), 0.80));
                grad.GradientStops.Add(new GradientStop(BarVisualizer.GetThemeColor(theme, 1f, 0.8f, cc), 1.00));
            }
            else
            {
                grad.GradientStops.Add(new GradientStop(BarVisualizer.GetThemeColor(theme, 0f, 0.8f, cc), 0.0));
                grad.GradientStops.Add(new GradientStop(BarVisualizer.GetThemeColor(theme, 1f, 0.8f, cc), 1.0));
            }

            ColorPreview.Background = grad;
        }
    }

    private void OnVisualizerModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        _loading = true;
        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is ComboBoxItem oldItem)
        {
            var oldMode = oldItem.Content?.ToString();
            if (!string.IsNullOrEmpty(oldMode))
            {
                _settingsManager.Update(s =>
                {
                    if (!s.Visualizer.ModeSettings.ContainsKey(oldMode))
                        s.Visualizer.ModeSettings[oldMode] = new ModeSettings();
                    var sm = s.Visualizer.ModeSettings[oldMode];
                    sm.ColorTheme = GetSelected(ColorThemeCombo);
                    sm.CustomColorR = (int)CustomRSlider.Value;
                    sm.CustomColorG = (int)CustomGSlider.Value;
                    sm.CustomColorB = (int)CustomBSlider.Value;
                    sm.Opacity = OpacitySlider.Value;
                    sm.BarCount = (int)BarCountSlider.Value;
                    sm.Sensitivity = SensitivitySlider.Value;
                    sm.Threshold = ThresholdSlider.Value;
                    sm.BarStartOffset = (int)BarStartOffsetSlider.Value;
                    sm.ShowPeak = ShowPeakCheck.IsChecked ?? true;
                    return s;
                });
            }
        }

        var newMode = GetSelected(VisualizerModeCombo);
        _settingsManager.Update(s =>
        {
            s.Visualizer.Mode = newMode;
            return s;
        });

        if (_settingsManager.Current.Visualizer.ModeSettings.TryGetValue(newMode, out var ms))
        {
            SelectItem(ColorThemeCombo, ms.ColorTheme);
            CustomRSlider.Value = ms.CustomColorR;
            CustomGSlider.Value = ms.CustomColorG;
            CustomBSlider.Value = ms.CustomColorB;
            UpdateColorPreview();
            OpacitySlider.Value = ms.Opacity;
            BarCountSlider.Value = ms.BarCount;
            SensitivitySlider.Value = ms.Sensitivity;
            ThresholdSlider.Value = ms.Threshold;
            BarStartOffsetSlider.Value = ms.BarStartOffset;
            ShowPeakCheck.IsChecked = ms.ShowPeak;
        }

        UpdateBarCountTickFrequency();
        UpdateSliderValueLabels();

        _loading = false;

        if (Application.Current.MainWindow is MainWindow mw)
            mw.RefreshSettings();
    }

    private void UpdateBarCountTickFrequency()
    {
        bool isDot = GetSelected(VisualizerModeCombo) == "Dot";
        BarCountSlider.TickFrequency = isDot ? 1 : 8;
        BarCountSlider.IsSnapToTickEnabled = true;
        // Snap current value to the nearest tick
        var freq = BarCountSlider.TickFrequency;
        var snapped = Math.Round(BarCountSlider.Value / freq) * freq;
        snapped = Math.Max(BarCountSlider.Minimum, Math.Min(BarCountSlider.Maximum, snapped));
        BarCountSlider.Value = snapped;
    }

    private static void SelectItem(ComboBox combo, string content)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem cbi && cbi.Content?.ToString() == content)
            {
                combo.SelectedItem = cbi;
                return;
            }
        }
        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;
    }

    private static void BlockComboScroll(ComboBox combo)
    {
        combo.PreviewMouseWheel += (_, e) =>
        {
            if (!combo.IsDropDownOpen)
                e.Handled = true;
        };
    }

    private static string GetSelected(ComboBox combo)
    {
        if (combo.SelectedItem is ComboBoxItem item)
            return item.Content?.ToString() ?? "";
        return "";
    }

    private void OnTitleBarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _settingsManager.ResetToDefaults();
        LoadSettings();
    }
}
