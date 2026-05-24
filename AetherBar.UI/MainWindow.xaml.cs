using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AetherBar.Core.Audio;
using AetherBar.Core.Media;
using AetherBar.Core.Models;
using AetherBar.Core.Settings;
using AetherBar.Core.Visualizer;
using AetherBar.Hooker;
using AetherBar.Hooker.Interop;

namespace AetherBar.UI;

public partial class MainWindow : Window
{
    private TaskbarHooker? _hooker;
    private AudioManager? _audioManager;
    private VisualizerController? _visualizer;
    private MediaManager? _mediaManager;
    private DispatcherTimer? _positionTimer;
    private GameModeDetector? _gameMode;
    private SettingsManager _settingsManager = null!;
    private bool _embedded;
    private bool _mediaActive;
    private int _retryCount;
    private bool _hasAlbumArt;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle &= ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        _settingsManager = ((App)Application.Current).Settings;

        _hooker = new TaskbarHooker();
        _retryCount = 0;
        TryEmbed();

        _audioManager = new AudioManager();
        _audioManager.StartCapture();

        _mediaManager = new MediaManager();
        _mediaManager.MediaInfoChanged += OnMediaInfoChanged;
        _mediaManager.StartMonitoring();

        _visualizer = new VisualizerController(_audioManager);
        VisualizerControl.SetController(_visualizer);

        _gameMode = new GameModeDetector();
        _gameMode.FullscreenStateChanged += OnFullscreenStateChanged;

        _settingsManager.SettingsChanged -= OnSettingsChanged;
        _settingsManager.SettingsChanged += OnSettingsChanged;

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _positionTimer.Tick += (_, _) =>
        {
            try
            {
                if (_embedded)
                    RepositionWidget();
            }
            catch
            {
            }
        };
        _positionTimer.Start();

        RefreshSettings();
        SetupTrayIcon();

        if (!_settingsManager.Current.General.StartMinimized)
        {
            Show();
        }
    }

    private void OnSettingsChanged(object? sender, AetherBarSettings e)
    {
        try
        {
            Dispatcher.Invoke(RefreshSettings);
        }
        catch
        {
        }
    }

    private void OnFullscreenStateChanged(object? sender, bool isFullscreen)
    {
    }

    private void OnMediaInfoChanged(object? sender, MediaInfo media)
    {
        Dispatcher.Invoke(() =>
        {
            bool showMedia = _settingsManager.Current.Taskbar.ShowMediaInfo;

            if (media.PlaybackStatus == MediaPlaybackStatus.Playing)
            {
                _mediaActive = true;
                VisualizerControl.Visibility = Visibility.Visible;

                if (media.AlbumArt != null && media.AlbumArt.Length > 0)
                {
                    _hasAlbumArt = true;
                    using var ms = new MemoryStream(media.AlbumArt);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    AlbumArtImage.Source = bmp;
                    AlbumArtImage.Visibility = showMedia ? Visibility.Visible : Visibility.Collapsed;

                    if (_settingsManager.Current.Effects.AdaptiveTheme &&
                        _settingsManager.Current.Effects.BackgroundEffect != "None")
                    {
                        var color = DominantColorExtractor.ExtractFromBytes(media.AlbumArt);
                        WidgetContainer.Background = new SolidColorBrush(
                            Color.FromArgb(40, color.R, color.G, color.B));
                    }
                }
                else
                {
                    _hasAlbumArt = false;
                    AlbumArtImage.Visibility = Visibility.Collapsed;
                }

                if (showMedia)
                {
                    var info = string.IsNullOrEmpty(media.Artist)
                        ? media.Title
                        : $"{media.Artist} - {media.Title}";
                    SongInfoText.Text = info;
                    SongInfoText.Visibility = Visibility.Visible;
                }
                else
                {
                    SongInfoText.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                _mediaActive = false;
                _hasAlbumArt = false;
                AlbumArtImage.Visibility = Visibility.Collapsed;
                SongInfoText.Visibility = Visibility.Collapsed;
                VisualizerControl.Visibility = Visibility.Visible;
                var isDark = _settingsManager.Current.Effects.EnableDarkMode;
                WidgetContainer.Background = isDark
                    ? new SolidColorBrush(Color.FromArgb(34, 0, 0, 0))
                    : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            }
        });
    }

    public void RefreshSettings()
    {
        try
        {
            var s = _settingsManager.Current;
            if (_embedded)
            {
                Width = s.Taskbar.WidgetWidth;
                RepositionWidget();
            }
            WidgetContainer.Padding = new Thickness(s.Taskbar.WidgetPadding);
            WidgetContainer.CornerRadius = new CornerRadius(s.Effects.CornerRadius);
            bool showMedia = s.Taskbar.ShowMediaInfo;
            SongInfoText.Visibility = showMedia && _mediaActive ? Visibility.Visible : Visibility.Collapsed;
            AlbumArtImage.Visibility = showMedia && _hasAlbumArt ? Visibility.Visible : Visibility.Collapsed;
            _visualizer?.SetMode(s.Visualizer.Mode);

            if (_visualizer != null)
            {
                var mode = s.Visualizer.Mode;
                s.Visualizer.ModeSettings.TryGetValue(mode, out var ms);
                _visualizer.Options.ColorTheme = ms?.ColorTheme ?? "Rainbow";
                _visualizer.Options.Opacity = ms?.Opacity ?? 0.5;
                _visualizer.Options.BarCount = ms?.BarCount ?? 32;
                _visualizer.Options.Sensitivity = ms?.Sensitivity ?? 1.0;
                _visualizer.Options.Threshold = ms?.Threshold ?? 0.0;
                _visualizer.Options.BarStartOffset = ms?.BarStartOffset ?? 0;
                _visualizer.Options.CustomColor = Color.FromRgb(
                    (byte)(ms?.CustomColorR ?? 0),
                    (byte)(ms?.CustomColorG ?? 204),
                    (byte)(ms?.CustomColorB ?? 255));
                _visualizer.Options.ShowPeak = ms?.ShowPeak ?? true;
            }

            bool isDark = s.Effects.EnableDarkMode;

            if (!_mediaActive)
            {
                if (isDark)
                    WidgetContainer.Background = new SolidColorBrush(Color.FromArgb(34, 0, 0, 0));
                else
                    WidgetContainer.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            }

            switch (s.Effects.BackgroundEffect)
            {
                case "None":
                    WidgetContainer.Background = Brushes.Transparent;
                    break;
                case "Acrylic (Blur)":
                    WidgetContainer.Background = Brushes.Transparent;
                    break;
                case "Mica":
                    WidgetContainer.Background = Brushes.Transparent;
                    break;
            }

            SongInfoText.Foreground = GetTextColorBrush(s.Taskbar.WidgetTextColor,
                s.Taskbar.WidgetTextColorR, s.Taskbar.WidgetTextColorG, s.Taskbar.WidgetTextColorB, isDark);

            if (_embedded)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                switch (s.Effects.BackgroundEffect)
                {
                    case "Acrylic (Blur)":
                        DesktopWindowManager.EnableAcrylic(hwnd);
                        break;
                    case "Mica":
                        DesktopWindowManager.EnableMica(hwnd);
                        break;
                }
                DesktopWindowManager.EnableDarkMode(hwnd, isDark);
            }
        }
        catch
        {
        }
    }

    private void TryEmbed()
    {
        if (_hooker is null) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0) return;

        if (!_hooker.Hook())
        {
            ScheduleRetry();
            return;
        }

        if (!_hooker.EmbedWindow(handle))
        {
            ScheduleRetry();
            return;
        }

        _embedded = true;
        _retryCount = 0;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.None;
    }

    private void ScheduleRetry()
    {
        if (_retryCount >= 20) return;
        _retryCount++;
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        t.Tick += (_, _) =>
        {
            t.Stop();
            TryEmbed();
            if (!_embedded)
                ShowFallbackWindow();
        };
        t.Start();
        ShowFallbackWindow();
    }

    private void ShowFallbackWindow()
    {
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = true;
        Width = 400;
        Height = 60;
        Topmost = true;
    }

    private void RepositionWidget()
    {
        try
        {
            var s = _settingsManager.Current;
            if (_embedded && _hooker is not null)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                var parent = NativeMethods.GetParent(hwnd);
                var taskbarHwnd = NativeMethods.FindWindow("Shell_TrayWnd", null);
                if (parent != taskbarHwnd && taskbarHwnd != 0)
                {
                    _hooker.Detach();
                    _embedded = false;
                    _retryCount = 0;
                    TryEmbed();
                    return;
                }
            }
            _hooker?.PositionEmbeddedWindow(s.Taskbar.Position, s.Taskbar.OffsetX);
        }
        catch
        {
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _positionTimer?.Stop();
        if (_settingsManager != null)
            _settingsManager.SettingsChanged -= OnSettingsChanged;
        _gameMode?.StopMonitoring();
        _gameMode?.Dispose();
        VisualizerControl.Cleanup();
        _visualizer?.Dispose();
        _mediaManager?.Dispose();
        _audioManager?.Dispose();
        _hooker?.Detach();
        _hooker?.Dispose();
    }

    private void OnTrayShowHide(object sender, RoutedEventArgs e)
    {
        if (Visibility == Visibility.Visible)
            Hide();
        else
        {
            Show();
            Activate();
        }
    }

    private void OnTraySettings(object sender, RoutedEventArgs e)
    {
        ((App)Application.Current).ShowSettingsWindow();
    }

    private void OnTrayExit(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    private static Brush GetTextColorBrush(string mode, int cr, int cg, int cb, bool isDark)
    {
        var a = isDark ? (byte)0xDD : (byte)0xCC;
        return mode switch
        {
            "White"  => new SolidColorBrush(Color.FromArgb(0xDD, 0xFF, 0xFF, 0xFF)),
            "Black"  => new SolidColorBrush(Color.FromArgb(0xDD, 0x00, 0x00, 0x00)),
            "Red"    => new SolidColorBrush(Color.FromArgb(a, (byte)0xFF, (byte)0x44, (byte)0x44)),
            "Green"  => new SolidColorBrush(Color.FromArgb(a, (byte)0x44, (byte)0xFF, (byte)0x44)),
            "Blue"   => new SolidColorBrush(Color.FromArgb(a, (byte)0x44, (byte)0x44, (byte)0xFF)),
            "Cyan"   => new SolidColorBrush(Color.FromArgb(a, (byte)0x44, (byte)0xFF, (byte)0xFF)),
            "Yellow" => new SolidColorBrush(Color.FromArgb(a, (byte)0xFF, (byte)0xFF, (byte)0x44)),
            "Custom" => new SolidColorBrush(Color.FromArgb(a, (byte)cr, (byte)cg, (byte)cb)),
            _ => isDark
                ? new SolidColorBrush(Color.FromArgb(0xBB, 0xFF, 0xFF, 0xFF))
                : new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0x1A, 0x1A))
        };
    }

    private void SetupTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AetherBar.ico");
        if (File.Exists(iconPath))
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(iconPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                TrayIcon.IconSource = bmp;
            }
            catch
            {
            }
        }

        TrayIcon.RightClickCommand = new RelayCommand(ShowTrayMenu);
    }

    private void ShowTrayMenu()
    {
        var menu = new ContextMenu
        {
            Placement = PlacementMode.MousePoint,
            HorizontalOffset = 0,
            VerticalOffset = 0,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
            FontSize = 13,
        };
        menu.SetResourceReference(ContextMenu.BackgroundProperty, "WindowBackground");
        menu.SetResourceReference(ContextMenu.BorderBrushProperty, "CardBorder");

        menu.Items.Add(MakeTrayItem("Show/Hide", OnTrayShowHide, menu));
        menu.Items.Add(MakeTrayItem("Settings", OnTraySettings, menu));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeTrayItem("Exit", OnTrayExit, menu));

        menu.IsOpen = true;
    }

    private static MenuItem MakeTrayItem(string header, RoutedEventHandler handler, ContextMenu menu)
    {
        var item = new MenuItem
        {
            Header = header,
            Background = Brushes.Transparent,
        };
        item.SetResourceReference(MenuItem.ForegroundProperty, "TextPrimary");
        item.Click += handler;
        item.Click += (_, _) => menu.IsOpen = false;
        return item;
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}

internal class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public RelayCommand(Action execute) => _execute = execute;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
