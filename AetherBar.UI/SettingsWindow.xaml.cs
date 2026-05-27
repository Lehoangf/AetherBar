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
    private DispatcherTimer? _previewTimer;
    private const int MaxCustomColors = 10;
    private readonly List<CustomColorRow> _customColorRows = new();
    private ComboBox[] _allCombos = null!;

    private class CustomColorRow
    {
        public Border Container { get; set; } = null!;
        public Border Swatch { get; set; } = null!;
        public Slider R { get; set; } = null!;
        public Slider G { get; set; } = null!;
        public Slider B { get; set; } = null!;
        public TextBlock RText { get; set; } = null!;
        public TextBlock GText { get; set; } = null!;
        public TextBlock BText { get; set; } = null!;
    }

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
        _allCombos = new[] { VisualizerModeCombo, ColorThemeCombo, AnimatedDirectionCombo,
            PositionCombo, WidgetTextColorCombo, DoubleClickActionCombo, RightClickActionCombo,
            BgEffectCombo, PluginSelectorCombo, PluginAlignmentCombo };
        Closing += (_, _) => _previewTimer?.Stop();
        PreviewMouseWheel += (_, _) =>
        {
            var mouseOver = Mouse.DirectlyOver as DependencyObject;
            bool overComboItem = false;
            while (mouseOver != null)
            {
                if (mouseOver is ComboBoxItem) { overComboItem = true; break; }
                mouseOver = VisualTreeHelper.GetParent(mouseOver);
            }
            if (!overComboItem)
            {
                foreach (var combo in _allCombos)
                {
                    if (combo.IsDropDownOpen)
                    {
                        combo.IsDropDownOpen = false;
                        break;
                    }
                }
            }
        };
        LoadSettings();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _previewTimer.Tick += (_, _) => UpdateColorPreview();
        _previewTimer.Start();

        _loading = false;
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

        foreach (var combo in _allCombos)
            combo.MaxDropDownHeight = 160;

        SelectItem(VisualizerModeCombo, s.Visualizer.Mode);
        var mode = GetSelected(VisualizerModeCombo);
        s.Visualizer.ModeSettings.TryGetValue(mode, out var ms);
        SelectItem(ColorThemeCombo, ms?.ColorTheme ?? "Rainbow");
        LoadCustomGradientFromSettings(ms?.CustomGradientColors, (byte)(ms?.CustomColorR ?? 0), (byte)(ms?.CustomColorG ?? 204), (byte)(ms?.CustomColorB ?? 255));
        UpdateColorPreview();
        OpacitySlider.Value = ms?.Opacity ?? 0.5;
        BarCountSlider.Value = ms?.BarCount ?? 32;
        SensitivitySlider.Value = ms?.Sensitivity ?? 1.0;
        ThresholdSlider.Value = ms?.Threshold ?? 0.0;
        BarStartOffsetSlider.Value = ms?.BarStartOffset ?? 0;
        ShowPeakCheck.IsChecked = ms?.ShowPeak ?? true;
        AlbumArtColorCheck.IsChecked = ms?.AlbumArtColor ?? false;
        AlbumArtBrightnessPanel.Visibility = AlbumArtColorCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AlbumArtMinLightnessSlider.Value = ms?.AlbumArtMinLightness ?? 0.3;
        AlbumArtMaxLightnessSlider.Value = ms?.AlbumArtMaxLightness ?? 0.85;
        PopulateCombo(AnimatedDirectionCombo, "MoveRight", "MoveLeft", "Wave");
        AnimatedGradientCheck.IsChecked = ms?.AnimatedGradientEnabled ?? false;
        AnimatedGradientPanel.Visibility = AnimatedGradientCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SelectItem(AnimatedDirectionCombo, ms?.AnimatedGradientDirection ?? "MoveRight");
        AnimatedSpeedSlider.Value = ms?.AnimatedGradientSpeed ?? 1.0;
        UpdateCheckStates();

        SelectItem(PositionCombo, s.Taskbar.Position);
        WidthSlider.Value = s.Taskbar.WidgetWidth;
        OffsetSlider.Value = s.Taskbar.OffsetX;
        PaddingXSlider.Value = s.Taskbar.WidgetPaddingX;
        PaddingYSlider.Value = s.Taskbar.WidgetPaddingY;
        VisualizerHeightSlider.Value = s.Taskbar.VisualizerHeight;
        VisualizerOffsetYSlider.Value = s.Taskbar.VisualizerOffsetY;
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

        TitleOpacitySlider.Value = s.Taskbar.TitleOpacity;

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

        if (sender == AlbumArtColorCheck)
        {
            AlbumArtBrightnessPanel.Visibility =
                AlbumArtColorCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            UpdateCheckStates();
        }

        if (sender == AnimatedGradientCheck)
        {
            AnimatedGradientPanel.Visibility =
                AnimatedGradientCheck.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            UpdateCheckStates();
        }

        if (sender == ColorThemeCombo)
        {
            UpdateCheckStates();
        }

        _settingsManager.Update(s =>
        {
            var mode = GetSelected(VisualizerModeCombo);
            s.Visualizer.Mode = mode;
            if (!s.Visualizer.ModeSettings.ContainsKey(mode))
                s.Visualizer.ModeSettings[mode] = new ModeSettings();
            s.Visualizer.ModeSettings[mode].ColorTheme = GetSelected(ColorThemeCombo);
            s.Visualizer.ModeSettings[mode].CustomColorR = _customColorRows.Count > 0 ? (int)_customColorRows[0].R.Value : 0;
            s.Visualizer.ModeSettings[mode].CustomColorG = _customColorRows.Count > 0 ? (int)_customColorRows[0].G.Value : 204;
            s.Visualizer.ModeSettings[mode].CustomColorB = _customColorRows.Count > 0 ? (int)_customColorRows[0].B.Value : 255;
            s.Visualizer.ModeSettings[mode].CustomGradientColors = BuildCustomGradientColors();
            s.Visualizer.ModeSettings[mode].Opacity = OpacitySlider.Value;
            s.Visualizer.ModeSettings[mode].BarCount = (int)BarCountSlider.Value;
            s.Visualizer.ModeSettings[mode].Sensitivity = SensitivitySlider.Value;
            s.Visualizer.ModeSettings[mode].Threshold = ThresholdSlider.Value;
            s.Visualizer.ModeSettings[mode].BarStartOffset = (int)BarStartOffsetSlider.Value;
            s.Visualizer.ModeSettings[mode].ShowPeak = ShowPeakCheck.IsChecked ?? true;
            s.Visualizer.ModeSettings[mode].AlbumArtColor = AlbumArtColorCheck.IsChecked ?? false;
            s.Visualizer.ModeSettings[mode].AlbumArtMinLightness = AlbumArtMinLightnessSlider.Value;
            s.Visualizer.ModeSettings[mode].AlbumArtMaxLightness = AlbumArtMaxLightnessSlider.Value;
            s.Visualizer.ModeSettings[mode].AnimatedGradientEnabled = AnimatedGradientCheck.IsChecked ?? false;
            s.Visualizer.ModeSettings[mode].AnimatedGradientDirection = GetSelected(AnimatedDirectionCombo);
            s.Visualizer.ModeSettings[mode].AnimatedGradientSpeed = AnimatedSpeedSlider.Value;

            s.Taskbar.Position = GetSelected(PositionCombo);
            s.Taskbar.WidgetWidth = (int)WidthSlider.Value;
            s.Taskbar.OffsetX = (int)OffsetSlider.Value;
            s.Taskbar.WidgetPaddingX = (int)PaddingXSlider.Value;
            s.Taskbar.WidgetPaddingY = (int)PaddingYSlider.Value;
            s.Taskbar.VisualizerHeight = (int)VisualizerHeightSlider.Value;
            s.Taskbar.VisualizerOffsetY = (int)VisualizerOffsetYSlider.Value;
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

            s.Taskbar.TitleOpacity = TitleOpacitySlider.Value;

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
        UpdateColorPreview();

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
        VisualizerOffsetYValue.Text = ((int)VisualizerOffsetYSlider.Value).ToString();
        TitleOpacityValue.Text = TitleOpacitySlider.Value.ToString("0.0");
        AlbumArtMinLightnessValue.Text = AlbumArtMinLightnessSlider.Value.ToString("0.0");
        AlbumArtMaxLightnessValue.Text = AlbumArtMaxLightnessSlider.Value.ToString("0.0");
        AnimatedDirectionValue.Text = GetSelected(AnimatedDirectionCombo);
        AnimatedSpeedValue.Text = AnimatedSpeedSlider.Value.ToString("0.0");
        foreach (var row in _customColorRows)
        {
            row.RText.Text = ((int)row.R.Value).ToString();
            row.GText.Text = ((int)row.G.Value).ToString();
            row.BText.Text = ((int)row.B.Value).ToString();
        }
        CornerRadiusValue.Text = ((int)CornerRadiusSlider.Value).ToString();
    }

    private void UpdateCheckStates()
    {
        bool albumArtChecked = AlbumArtColorCheck.IsChecked == true;
        bool animatedChecked = AnimatedGradientCheck.IsChecked == true;
        bool isCustom = GetSelected(ColorThemeCombo) == "Custom";
        int gradientColorCount = _customColorRows.Count;

        ColorThemeCombo.IsEnabled = !albumArtChecked;
        AlbumArtColorCheck.IsEnabled = !animatedChecked;

        if (isCustom)
            AnimatedGradientCheck.IsEnabled = gradientColorCount >= 2;
        else
            AnimatedGradientCheck.IsEnabled = true;

        if (AnimatedGradientCheck.IsEnabled == false)
            AnimatedGradientCheck.IsChecked = false;
    }

    private List<string> BuildCustomGradientColors()
    {
        var list = new List<string>();
        foreach (var row in _customColorRows)
            list.Add(ColorUtils.ToHex(Color.FromRgb((byte)row.R.Value, (byte)row.G.Value, (byte)row.B.Value)));
        return list;
    }

    private void LoadCustomGradientFromSettings(List<string>? colors, byte r = 0, byte g = 204, byte b = 255)
    {
        CustomColorsContainer.Children.Clear();
        _customColorRows.Clear();

        if (colors == null || colors.Count == 0)
        {
            AddCustomColorRow(Color.FromRgb(r, g, b), canRemove: false);
        }
        else
        {
            for (int i = 0; i < colors.Count && i < MaxCustomColors; i++)
            {
                var parsed = ColorUtils.ParseHexColor(colors[i]);
                AddCustomColorRow(parsed ?? (i == 0 ? Color.FromRgb(r, g, b) : Colors.White), canRemove: i > 0);
            }
        }

        UpdateAddColorButton();
        UpdateCustomGradientPreview();
    }

    private CustomColorRow AddCustomColorRow(Color color, bool canRemove)
    {
        var row = new CustomColorRow();

        var border = new Border { Margin = new Thickness(0, 8, 0, 0) };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0 = swatch
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 1 = R
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); // 2 = gap
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 3 = G
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); // 4 = gap
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 5 = B
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 6 = remove btn

        // Swatch
        var swatch = new Border
        {
            Width = 16, Height = 16, CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(color),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center
        };
        swatch.SetResourceReference(Border.BorderBrushProperty, "CardBorder");
        Grid.SetColumn(swatch, 0);
        grid.Children.Add(swatch);
        row.Swatch = swatch;

        // R/G/B slider helpers
        void AddSliderColumn(int col, string label, byte value, Brush labelColor, out Slider slider, out TextBlock text)
        {
            var innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelTb = new TextBlock
            {
                Text = label, Foreground = labelColor,
                Width = 14, VerticalAlignment = VerticalAlignment.Center, FontSize = 12
            };
            Grid.SetColumn(labelTb, 0);
            innerGrid.Children.Add(labelTb);

            slider = new Slider { Minimum = 0, Maximum = 255, TickFrequency = 1, Value = value };
            slider.ValueChanged += OnSettingChanged;
            Grid.SetColumn(slider, 1);
            innerGrid.Children.Add(slider);

            text = new TextBlock
            {
                Text = ((int)value).ToString(), Width = 26,
                VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right
            };
            text.SetResourceReference(TextBlock.StyleProperty, "SliderValue");
            Grid.SetColumn(text, 2);
            innerGrid.Children.Add(text);

            Grid.SetColumn(innerGrid, col);
            grid.Children.Add(innerGrid);
        }

        AddSliderColumn(1, "R", color.R, new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)), out var sliderR, out var textR);
        row.R = sliderR;
        row.RText = textR;
        AddSliderColumn(3, "G", color.G, new SolidColorBrush(Color.FromRgb(0x44, 0xFF, 0x44)), out var sliderG, out var textG);
        row.G = sliderG;
        row.GText = textG;
        AddSliderColumn(5, "B", color.B, new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0xFF)), out var sliderB, out var textB);
        row.B = sliderB;
        row.BText = textB;

        // Remove button
        if (canRemove)
        {
            var removeBtn = new Button
            {
                Content = "−", Width = 22, Height = 22, FontSize = 14,
                Padding = new Thickness(0), Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0), Foreground = Brushes.White
            };
            removeBtn.SetResourceReference(Button.BackgroundProperty, "CardBorder");
            int capturedIndex = _customColorRows.Count;
            removeBtn.Click += (_, _) => RemoveCustomColorAt(capturedIndex);
            Grid.SetColumn(removeBtn, 6);
            grid.Children.Add(removeBtn);
        }

        border.Child = grid;
        CustomColorsContainer.Children.Add(border);
        row.Container = border;
        _customColorRows.Add(row);
        return row;
    }

    private void UpdateAddColorButton()
    {
        AddColorBtn.Visibility = _customColorRows.Count >= MaxCustomColors
            ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateCustomGradientPreview()
    {
        var colors = new List<Color>();
        foreach (var row in _customColorRows)
        {
            var c = Color.FromRgb((byte)row.R.Value, (byte)row.G.Value, (byte)row.B.Value);
            colors.Add(c);
            row.Swatch.Background = new SolidColorBrush(c);
        }

        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        if (colors.Count == 1)
        {
            brush.GradientStops.Add(new GradientStop(colors[0], 0));
            brush.GradientStops.Add(new GradientStop(colors[0], 1));
        }
        else
        {
            for (int i = 0; i < colors.Count; i++)
                brush.GradientStops.Add(new GradientStop(colors[i], (double)i / (colors.Count - 1)));
        }
        CustomGradientPreview.Background = brush;
    }

    private void AddColor_Click(object sender, RoutedEventArgs e)
    {
        if (_customColorRows.Count >= MaxCustomColors) return;
        AddCustomColorRow(Colors.White, canRemove: true);
        UpdateAddColorButton();
        UpdateCustomGradientPreview();
        UpdateCheckStates();
        OnSettingChanged(sender, e);
    }

    private void RemoveCustomColorAt(int index)
    {
        if (index < 0 || index >= _customColorRows.Count) return;
        CustomColorsContainer.Children.Remove(_customColorRows[index].Container);
        _customColorRows.RemoveAt(index);
        UpdateAddColorButton();
        UpdateCustomGradientPreview();
        UpdateCheckStates();
        OnSettingChanged(null, null!);
    }

    private void UpdateColorPreview()
    {
        var theme = GetSelected(ColorThemeCombo);
        bool isCustom = theme == "Custom";
        CustomColorPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

        var albumColor = MainWindow.CurrentAlbumArtColor;
        if (AlbumArtColorCheck.IsChecked == true)
        {
            if (albumColor != default && albumColor != Colors.Transparent)
            {
                var min = AlbumArtMinLightnessSlider.Value;
                var max = AlbumArtMaxLightnessSlider.Value;
                var clamped = ColorUtils.ClampLightness(albumColor, min, max);
                ColorPreview.Background = new SolidColorBrush(clamped);
            }
            else
            {
                ColorPreview.Background = new SolidColorBrush(Color.FromRgb(80, 80, 80));
                ColorPreview.Opacity = 0.4;
                return;
            }
            ColorPreview.Opacity = 1.0;
            return;
        }

        ColorPreview.Opacity = 1.0;

        if (isCustom)
        {
            UpdateCustomGradientPreview();
            var gradientColors = BuildCustomGradientColors();
            var cc = _customColorRows.Count > 0
                ? Color.FromRgb((byte)_customColorRows[0].R.Value, (byte)_customColorRows[0].G.Value, (byte)_customColorRows[0].B.Value)
                : Color.FromRgb(0, 204, 255);

            if (_customColorRows.Count > 0)
            {
                _customColorRows[0].RText.Text = ((int)_customColorRows[0].R.Value).ToString();
                _customColorRows[0].GText.Text = ((int)_customColorRows[0].G.Value).ToString();
                _customColorRows[0].BText.Text = ((int)_customColorRows[0].B.Value).ToString();
            }

            if (gradientColors.Count >= 2)
            {
                var grad = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
                for (int i = 0; i < gradientColors.Count; i++)
                {
                    var c = ColorUtils.ParseHexColor(gradientColors[i]);
                    if (c.HasValue)
                        grad.GradientStops.Add(new GradientStop(c.Value, (double)i / (gradientColors.Count - 1)));
                }
                ColorPreview.Background = grad;
            }
            else
            {
                ColorPreview.Background = new SolidColorBrush(cc);
            }
        }
        else
        {
            var cc = _customColorRows.Count > 0
                ? Color.FromRgb((byte)_customColorRows[0].R.Value, (byte)_customColorRows[0].G.Value, (byte)_customColorRows[0].B.Value)
                : Color.FromRgb(0, 204, 255);
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
                    sm.CustomColorR = _customColorRows.Count > 0 ? (int)_customColorRows[0].R.Value : 0;
                    sm.CustomColorG = _customColorRows.Count > 0 ? (int)_customColorRows[0].G.Value : 204;
                    sm.CustomColorB = _customColorRows.Count > 0 ? (int)_customColorRows[0].B.Value : 255;
                    sm.CustomGradientColors = BuildCustomGradientColors();
                    sm.Opacity = OpacitySlider.Value;
                    sm.BarCount = (int)BarCountSlider.Value;
                    sm.Sensitivity = SensitivitySlider.Value;
                    sm.Threshold = ThresholdSlider.Value;
                    sm.BarStartOffset = (int)BarStartOffsetSlider.Value;
                    sm.ShowPeak = ShowPeakCheck.IsChecked ?? true;
                    sm.AlbumArtColor = AlbumArtColorCheck.IsChecked ?? false;
                    sm.AlbumArtMinLightness = AlbumArtMinLightnessSlider.Value;
                    sm.AlbumArtMaxLightness = AlbumArtMaxLightnessSlider.Value;
                    sm.AnimatedGradientEnabled = AnimatedGradientCheck.IsChecked ?? false;
                    sm.AnimatedGradientDirection = GetSelected(AnimatedDirectionCombo);
                    sm.AnimatedGradientSpeed = AnimatedSpeedSlider.Value;
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
            LoadCustomGradientFromSettings(ms.CustomGradientColors, (byte)ms.CustomColorR, (byte)ms.CustomColorG, (byte)ms.CustomColorB);
            UpdateColorPreview();
            OpacitySlider.Value = ms.Opacity;
            BarCountSlider.Value = ms.BarCount;
            SensitivitySlider.Value = ms.Sensitivity;
            ThresholdSlider.Value = ms.Threshold;
            BarStartOffsetSlider.Value = ms.BarStartOffset;
            ShowPeakCheck.IsChecked = ms.ShowPeak;
            AlbumArtColorCheck.IsChecked = ms.AlbumArtColor;
            AlbumArtBrightnessPanel.Visibility = ms.AlbumArtColor ? Visibility.Visible : Visibility.Collapsed;
            AlbumArtMinLightnessSlider.Value = ms.AlbumArtMinLightness;
            AlbumArtMaxLightnessSlider.Value = ms.AlbumArtMaxLightness;
            AnimatedGradientCheck.IsChecked = ms.AnimatedGradientEnabled;
            AnimatedGradientPanel.Visibility = ms.AnimatedGradientEnabled ? Visibility.Visible : Visibility.Collapsed;
            SelectItem(AnimatedDirectionCombo, ms.AnimatedGradientDirection);
            AnimatedSpeedSlider.Value = ms.AnimatedGradientSpeed;
            UpdateCheckStates();
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

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t)
                yield return t;
            foreach (var descendant in FindVisualChildren<T>(child))
                yield return descendant;
        }
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
        var opacity = ps?.Opacity ?? 1.0;
        var verticalOffset = ps?.VerticalOffset ?? 0;

        SelectItem(PluginAlignmentCombo, alignment);
        PluginPaddingSlider.Value = padding;
        PluginEnabledCheck.IsChecked = enabled;
        PluginOpacitySlider.Value = opacity;
        PluginVerticalOffsetSlider.Value = verticalOffset;
        
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
        PluginOpacityValue.Text = PluginOpacitySlider.Value.ToString("0.0");
        PluginVerticalOffsetValue.Text = ((int)PluginVerticalOffsetSlider.Value).ToString();
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
            ps.Opacity = PluginOpacitySlider.Value;
            ps.VerticalOffset = (int)PluginVerticalOffsetSlider.Value;
            
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
                string? currentValue = null;
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

    private void OnTabControlPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (IsMouseOverComboBox(Mouse.DirectlyOver as DependencyObject))
        {
            return;
        }

        if (sender is TabControl tc && tc.SelectedContent is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        static bool IsMouseOverComboBox(DependencyObject? obj)
        {
            while (obj != null)
            {
                if (obj is ComboBox || obj is ComboBoxItem)
                {
                    return true;
                }

                var parent = VisualTreeHelper.GetParent(obj);
                if (parent == null)
                {
                    if (obj is FrameworkElement fe)
                    {
                        parent = fe.Parent ?? fe.TemplatedParent;
                    }
                    else if (obj is FrameworkContentElement fce)
                    {
                        parent = fce.Parent ?? fce.TemplatedParent;
                    }
                }
                obj = parent;
            }
            return false;
        }
    }
}
