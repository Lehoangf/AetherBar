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
using System.Linq;
using System.Collections.Generic;

namespace AetherBar.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsManager _settingsManager;
    private bool _loading;
    private Point _dragStartPoint;
    private bool _isDragging;

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
        PopulateCombo(DoubleClickActionCombo, "nothing", "settings", "url", "run");
        PopulateCombo(RightClickActionCombo, "menu", "nothing");

        BlockComboScroll(VisualizerModeCombo);
        BlockComboScroll(ColorThemeCombo);
        BlockComboScroll(PositionCombo);
        BlockComboScroll(BgEffectCombo);
        BlockComboScroll(WidgetTextColorCombo);
        BlockComboScroll(DoubleClickActionCombo);
        BlockComboScroll(RightClickActionCombo);

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
        PaddingXSlider.Value = s.Taskbar.WidgetPaddingX;
        PaddingYSlider.Value = s.Taskbar.WidgetPaddingY;
        VisualizerHeightSlider.Value = s.Taskbar.VisualizerHeight;
        ShowSongTitleCheck.IsChecked = s.Taskbar.ShowSongTitle;
        ShowAlbumArtCheck.IsChecked = s.Taskbar.ShowAlbumArt;
        AutoHideCheck.IsChecked = s.Taskbar.AutoHide;
        SelectItem(WidgetTextColorCombo, s.Taskbar.WidgetTextColor);
        WidgetTextRSlider.Value = s.Taskbar.WidgetTextColorR;
        WidgetTextGSlider.Value = s.Taskbar.WidgetTextColorG;
        WidgetTextBSlider.Value = s.Taskbar.WidgetTextColorB;
        UpdateWidgetTextColorPanel();
        UpdateWidgetTextPreview();

        AlbumArtSizeSlider.Value = s.Taskbar.AlbumArtSize;
        AlbumArtRadiusSlider.Value = s.Taskbar.AlbumArtCornerRadius;
        AlbumArtOpacitySlider.Value = s.Taskbar.AlbumArtOpacity;

        SelectItem(DoubleClickActionCombo, s.Taskbar.DoubleClickAction);
        DoubleClickValueBox.Text = s.Taskbar.DoubleClickValue ?? "";
        UpdateDoubleClickValueVisibility();
        SelectItem(RightClickActionCombo, s.Taskbar.RightClickAction);

        SelectItem(BgEffectCombo, s.Effects.BackgroundEffect);
        CornerRadiusSlider.Value = s.Effects.CornerRadius;
        AdaptiveThemeCheck.IsChecked = s.Effects.AdaptiveTheme;
        DarkModeCheck.IsChecked = s.Effects.EnableDarkMode;

        StartWithWindowsCheck.IsChecked = s.General.StartWithWindows;
        StartMinimizedCheck.IsChecked = s.General.StartMinimized;
        GameModeCheck.IsChecked = s.General.EnableGameMode;
        UpdateCheck.IsChecked = s.General.CheckForUpdates;

        PopulateCombo(PluginAlignmentCombo, "Left", "Center", "Right");
        BlockComboScroll(PluginSelectorCombo);
        BlockComboScroll(PluginAlignmentCombo);
        PopulatePluginsList();
        PopulatePluginsSortList();

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
            s.Taskbar.WidgetPaddingX = (int)PaddingXSlider.Value;
            s.Taskbar.WidgetPaddingY = (int)PaddingYSlider.Value;
            s.Taskbar.VisualizerHeight = (int)VisualizerHeightSlider.Value;
            s.Taskbar.ShowSongTitle = ShowSongTitleCheck.IsChecked ?? true;
            s.Taskbar.ShowAlbumArt = ShowAlbumArtCheck.IsChecked ?? true;
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

            s.Taskbar.AlbumArtSize = (int)AlbumArtSizeSlider.Value;
            s.Taskbar.AlbumArtCornerRadius = (int)AlbumArtRadiusSlider.Value;
            s.Taskbar.AlbumArtOpacity = AlbumArtOpacitySlider.Value;

            s.Taskbar.DoubleClickAction = GetSelected(DoubleClickActionCombo);
            s.Taskbar.DoubleClickValue = DoubleClickValueBox.Text;
            UpdateDoubleClickValueVisibility();
            s.Taskbar.RightClickAction = GetSelected(RightClickActionCombo);

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

    private void UpdateDoubleClickValueVisibility()
    {
        var action = GetSelected(DoubleClickActionCombo);
        DoubleClickValueBox.Visibility = (action == "url" || action == "run")
            ? Visibility.Visible : Visibility.Collapsed;
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
        PaddingXValue.Text = ((int)PaddingXSlider.Value).ToString();
        PaddingYValue.Text = ((int)PaddingYSlider.Value).ToString();
        AlbumArtSizeValue.Text = ((int)AlbumArtSizeSlider.Value).ToString();
        AlbumArtRadiusValue.Text = ((int)AlbumArtRadiusSlider.Value).ToString();
        AlbumArtOpacityValue.Text = AlbumArtOpacitySlider.Value.ToString("0.0");
        VisualizerHeightValue.Text = ((int)VisualizerHeightSlider.Value).ToString();
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

    private void PopulatePluginsList()
    {
        PluginSelectorCombo.Items.Clear();
        
        if (Application.Current.MainWindow is MainWindow mw && mw.PluginManager != null)
        {
            var plugins = mw.PluginManager.Plugins;
            if (plugins.Count > 0)
            {
                NoPluginsText.Visibility = Visibility.Collapsed;
                PluginSettingsPanel.Visibility = Visibility.Visible;
                
                foreach (var plugin in plugins)
                {
                    PluginSelectorCombo.Items.Add(new ComboBoxItem { Content = plugin.Name });
                }
                PluginSelectorCombo.SelectedIndex = 0;
                LoadSelectedPluginSettings();
            }
            else
            {
                NoPluginsText.Visibility = Visibility.Visible;
                PluginSettingsPanel.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            NoPluginsText.Visibility = Visibility.Visible;
            PluginSettingsPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void OnPluginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        LoadSelectedPluginSettings();
    }

    private void LoadSelectedPluginSettings()
    {
        if (PluginSelectorCombo.SelectedItem is not ComboBoxItem selectedItem) return;
        var pluginName = selectedItem.Content?.ToString();
        if (string.IsNullOrEmpty(pluginName)) return;

        var wasLoading = _loading;
        _loading = true;
        
        var s = _settingsManager.Current;
        PluginItemSettings? ps = null;
        if (s.Plugins != null)
        {
            s.Plugins.TryGetValue(pluginName, out ps);
        }

        var alignment = ps?.Alignment ?? "Right";
        var padding = ps?.Padding ?? 0;
        var width = ps?.Width ?? -1;
        var enabled = ps?.Enabled ?? true;

        SelectItem(PluginAlignmentCombo, alignment);
        PluginPaddingSlider.Value = padding;
        PluginEnabledCheck.IsChecked = enabled;
        
        if (width > 0)
        {
            PluginCustomWidthCheck.IsChecked = true;
            PluginWidthSlider.Value = width;
            PluginWidthPanel.IsEnabled = true;
        }
        else
        {
            PluginCustomWidthCheck.IsChecked = false;
            PluginWidthSlider.Value = 150; // default display value
            PluginWidthPanel.IsEnabled = false;
        }
        
        UpdatePluginLabels();
        LoadCustomSettingsUI(pluginName, ps);

        _loading = wasLoading;
    }

    private void UpdatePluginLabels()
    {
        PluginPaddingValue.Text = ((int)PluginPaddingSlider.Value).ToString();
        PluginWidthValue.Text = ((int)PluginWidthSlider.Value).ToString();
        PluginWidthPanel.Visibility = (PluginCustomWidthCheck.IsChecked == true) 
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPluginSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (PluginSelectorCombo.SelectedItem is not ComboBoxItem selectedItem) return;
        var pluginName = selectedItem.Content?.ToString();
        if (string.IsNullOrEmpty(pluginName)) return;

        _settingsManager.Update(s =>
        {
            if (s.Plugins == null)
            {
                s.Plugins = new Dictionary<string, PluginItemSettings>();
            }

            if (!s.Plugins.TryGetValue(pluginName, out var ps))
            {
                ps = new PluginItemSettings();
                s.Plugins[pluginName] = ps;
            }

            ps.Alignment = GetSelected(PluginAlignmentCombo);
            ps.Padding = (int)PluginPaddingSlider.Value;
            ps.Enabled = PluginEnabledCheck.IsChecked ?? true;
            
            if (PluginCustomWidthCheck.IsChecked == true)
            {
                ps.Width = (int)PluginWidthSlider.Value;
            }
            else
            {
                ps.Width = -1;
            }

            return s;
        });

        UpdatePluginLabels();

        if (Application.Current.MainWindow is MainWindow mw)
            mw.RefreshSettings();
    }

    private AetherBar.Plugins.IPlugin? GetActivePluginInstance(string pluginName)
    {
        if (Application.Current.MainWindow is MainWindow mw && mw.PluginManager != null)
        {
            return mw.PluginManager.Plugins.FirstOrDefault(p => p.Name == pluginName);
        }
        return null;
    }

    private void LoadCustomSettingsUI(string pluginName, PluginItemSettings? ps)
    {
        CustomSettingsContainer.Children.Clear();
        var activePlugin = GetActivePluginInstance(pluginName);

        if (activePlugin is AetherBar.Plugins.IPluginWithSettings pws)
        {
            PluginCustomSettingsCard.Visibility = Visibility.Visible;
            var defs = pws.GetSettingDefinitions();
            if (defs == null || defs.Count == 0)
            {
                PluginCustomSettingsCard.Visibility = Visibility.Collapsed;
                return;
            }

            foreach (var def in defs)
            {
                string currentValue = null;
                if (ps != null && ps.CustomSettings != null)
                {
                    ps.CustomSettings.TryGetValue(def.Key, out currentValue);
                }
                currentValue ??= def.DefaultValue;

                var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

                if (def.Type.Equals("bool", StringComparison.OrdinalIgnoreCase))
                {
                    var cb = new CheckBox
                    {
                        Content = def.DisplayName,
                        IsChecked = currentValue.Equals("true", StringComparison.OrdinalIgnoreCase),
                        Margin = new Thickness(0, 4, 0, 0),
                        FontSize = 13,
                        FontWeight = FontWeights.Medium
                    };
                    cb.Checked += (s, e) => SaveCustomSetting(pluginName, def.Key, "true");
                    cb.Unchecked += (s, e) => SaveCustomSetting(pluginName, def.Key, "false");
                    panel.Children.Add(cb);
                }
                else
                {
                    var header = new TextBlock
                    {
                        Text = def.DisplayName.ToUpper(),
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    panel.Children.Add(header);

                    if (def.Options != null && def.Options.Count > 0)
                    {
                        var combo = new ComboBox
                        {
                            Height = 32,
                            FontSize = 13,
                            Padding = new Thickness(4, 0, 4, 0),
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Background = (Brush)Application.Current.Resources["WindowBackground"],
                            Foreground = (Brush)Application.Current.Resources["TextPrimary"],
                            BorderBrush = (Brush)Application.Current.Resources["CardBorder"],
                            BorderThickness = new Thickness(1),
                            ItemsSource = def.Options,
                            SelectedItem = currentValue
                        };
                        combo.SelectionChanged += (s, e) =>
                        {
                            if (combo.SelectedItem is string val)
                                SaveCustomSetting(pluginName, def.Key, val);
                        };
                        panel.Children.Add(combo);
                    }
                    else
                    {
                        var tb = new TextBox
                        {
                            Text = currentValue,
                            Height = 32,
                            Padding = new Thickness(8, 4, 8, 4),
                            FontSize = 13,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Background = (Brush)Application.Current.Resources["WindowBackground"],
                            Foreground = (Brush)Application.Current.Resources["TextPrimary"],
                            BorderBrush = (Brush)Application.Current.Resources["CardBorder"],
                            BorderThickness = new Thickness(1),
                        };

                        tb.TextChanged += (s, e) => SaveCustomSetting(pluginName, def.Key, tb.Text);
                        panel.Children.Add(tb);
                    }
                }

                if (!string.IsNullOrEmpty(def.Description))
                {
                    var desc = new TextBlock
                    {
                        Text = def.Description,
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["TextTertiary"],
                        Margin = new Thickness(0, 4, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    panel.Children.Add(desc);
                }

                CustomSettingsContainer.Children.Add(panel);
            }
        }
        else
        {
            PluginCustomSettingsCard.Visibility = Visibility.Collapsed;
        }
    }

    private void SaveCustomSetting(string pluginName, string key, string value)
    {
        if (_loading) return;

        _settingsManager.Update(s =>
        {
            if (s.Plugins == null) s.Plugins = new Dictionary<string, PluginItemSettings>();
            if (!s.Plugins.TryGetValue(pluginName, out var ps))
            {
                ps = new PluginItemSettings();
                s.Plugins[pluginName] = ps;
            }
            if (ps.CustomSettings == null) ps.CustomSettings = new Dictionary<string, string>();
            ps.CustomSettings[key] = value;
            return s;
        });

        var activePlugin = GetActivePluginInstance(pluginName);
        if (activePlugin is AetherBar.Plugins.IPluginWithSettings pws)
        {
            try
            {
                pws.OnSettingChanged(key, value);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to notify setting change to plugin {pluginName}: {ex.Message}");
            }
        }
    }

    private void PopulatePluginsSortList()
    {
        PluginsSortListBox.Items.Clear();

        if (Application.Current.MainWindow is MainWindow mw && mw.PluginManager != null)
        {
            var s = _settingsManager.Current;
            var plugins = mw.PluginManager.Plugins.ToList();
            
            // Sort plugins by their SortOrder in settings
            plugins.Sort((a, b) =>
            {
                int orderA = s.Plugins != null && s.Plugins.TryGetValue(a.Name, out var psA) ? psA.SortOrder : 0;
                int orderB = s.Plugins != null && s.Plugins.TryGetValue(b.Name, out var psB) ? psB.SortOrder : 0;
                return orderA.CompareTo(orderB);
            });

            for (int i = 0; i < plugins.Count; i++)
            {
                PluginsSortListBox.Items.Add($"{i + 1}. {plugins[i].Name}");
            }
        }
    }

    private void PluginsSortListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void PluginsSortListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
        {
            Point position = e.GetPosition(null);

            if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                ListBox listBox = (ListBox)sender;
                var item = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);

                if (item != null)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop(listBox, item.Content, DragDropEffects.Move);
                    _isDragging = false;
                }
            }
        }
    }

    private void PluginsSortListBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void PluginsSortListBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(string)))
        {
            string droppedData = (string)e.Data.GetData(typeof(string));
            ListBox listBox = (ListBox)sender;

            var dropTargetItem = FindVisualParent<ListBoxItem>(e.OriginalSource as DependencyObject);
            int dropIndex = -1;

            if (dropTargetItem != null)
            {
                dropIndex = listBox.ItemContainerGenerator.IndexFromContainer(dropTargetItem);
            }
            else
            {
                dropIndex = listBox.Items.Count - 1;
            }

            int sourceIndex = listBox.Items.IndexOf(droppedData);
            if (sourceIndex != -1 && dropIndex != -1 && sourceIndex != dropIndex)
            {
                var items = new List<string>();
                foreach (var item in listBox.Items)
                {
                    items.Add(item.ToString() ?? "");
                }

                string itemToMove = items[sourceIndex];
                items.RemoveAt(sourceIndex);
                items.Insert(dropIndex, itemToMove);

                var pluginNamesInOrder = items.Select(x => {
                    int dotIndex = x.IndexOf('.');
                    if (dotIndex != -1)
                        return x.Substring(dotIndex + 1).Trim();
                    return x;
                }).ToList();

                _settingsManager.Update(s =>
                {
                    if (s.Plugins == null)
                    {
                        s.Plugins = new Dictionary<string, PluginItemSettings>();
                    }

                    for (int i = 0; i < pluginNamesInOrder.Count; i++)
                    {
                        string name = pluginNamesInOrder[i];
                        if (!s.Plugins.TryGetValue(name, out var ps))
                        {
                            ps = new PluginItemSettings();
                            s.Plugins[name] = ps;
                        }
                        ps.SortOrder = i;
                    }
                    return s;
                });

                PopulatePluginsSortList();

                if (Application.Current.MainWindow is MainWindow mw)
                    mw.RefreshSettings();
            }
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        if (child == null) return null;
        DependencyObject parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;
        if (parentObject is T parent)
            return parent;
        return FindVisualParent<T>(parentObject);
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

    private void OnPowerClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        _settingsManager.ResetToDefaults();
        LoadSettings();
    }
}
