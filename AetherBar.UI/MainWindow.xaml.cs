using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    private AetherBar.Plugins.PluginManager? _pluginManager;
    private bool _pluginsInitialized;
    private bool _mediaActive;
    private int _retryCount;
    private bool _hasAlbumArt;
    private MediaInfo? _currentMedia;
    private readonly HashSet<AetherBar.Plugins.IPlugin> _runningPlugins = new();
    private Storyboard? _marqueeStoryboard;

    public class ActivePluginWidgetInfo
    {
        public AetherBar.Plugins.IPlugin? Plugin { get; set; }
        public string WidgetName { get; set; } = string.Empty;
        public Border Host { get; set; } = null!;
        public int DefaultWidth { get; set; }
    }

    private readonly List<ActivePluginWidgetInfo> _activePluginWidgets = new();

    public AetherBar.Plugins.PluginManager? PluginManager => _pluginManager;

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

        // attempt plugin initialization (TryEmbed will call InitializePluginsIfNeeded on success)

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

            try
            {
                if (_audioManager != null && _audioManager.HasReceivedData &&
                    (DateTime.UtcNow - _audioManager.LastDataTime).TotalSeconds > 3)
                {
                    _audioManager.RestartCapture();
                }
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
            var s = _settingsManager.Current;

            if (media.PlaybackStatus == MediaPlaybackStatus.Playing)
            {
                _mediaActive = true;
                _currentMedia = media;
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
                    ApplyAlbumArtSettings();

                    if (s.Effects.AdaptiveTheme &&
                        s.Effects.BackgroundEffect != "None")
                    {
                        var color = DominantColorExtractor.ExtractFromBytes(media.AlbumArt);
                        WidgetContainer.Background = new SolidColorBrush(
                            Color.FromArgb(40, color.R, color.G, color.B));
                    }
                }
                else
                {
                    _hasAlbumArt = false;
                    ApplyAlbumArtSettings();
                }

                if (s.Taskbar.ShowSongTitle)
                {
                    var info = string.IsNullOrEmpty(media.Artist)
                        ? media.Title
                        : $"{media.Artist} - {media.Title}";
                    SongInfoText.Text = info;
                    SongInfoText.Visibility = Visibility.Visible;
                    StartMarquee();
                }
                else
                {
                    SongInfoText.Visibility = Visibility.Collapsed;
                    StopMarquee();
                }

                ApplyBackgroundForCurrentState(media);
            }
            else
            {
                _mediaActive = false;
                _hasAlbumArt = false;
                _currentMedia = null;
                ApplyAlbumArtSettings();
                SongInfoText.Visibility = Visibility.Collapsed;
                StopMarquee();
                VisualizerControl.Visibility = Visibility.Visible;
                ApplyBackgroundForCurrentState(null);
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
            WidgetContainer.Padding = new Thickness(s.Taskbar.WidgetPaddingX, s.Taskbar.WidgetPaddingY, s.Taskbar.WidgetPaddingX, s.Taskbar.WidgetPaddingY);
            WidgetContainer.CornerRadius = new CornerRadius(s.Effects.CornerRadius);
            var widgetScale = Math.Clamp(s.Taskbar.WidgetWidth / 180.0, 0.85, 1.25);
            SongInfoText.FontSize = 10 * widgetScale;
            bool showTitle = s.Taskbar.ShowSongTitle && _mediaActive;
            SongInfoText.Visibility = showTitle ? Visibility.Visible : Visibility.Collapsed;
            if (showTitle)
                StartMarquee();
            else
                StopMarquee();
            _visualizer?.SetMode(s.Visualizer.Mode);
            ApplyAlbumArtSettings();

            VisualizerControl.Height = s.Taskbar.VisualizerHeight;
            double contentH = s.Taskbar.VisualizerHeight;
            if (s.Taskbar.ShowSongTitle && _mediaActive)
                contentH += 14;
            Height = WidgetContainer.Margin.Top + WidgetContainer.Margin.Bottom
                     + s.Taskbar.WidgetPaddingY * 2
                     + contentH;

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
            ApplyBackgroundForCurrentState();

            SongInfoText.Foreground = GetTextColorBrush(s.Taskbar.WidgetTextColor,
                s.Taskbar.WidgetTextColorR, s.Taskbar.WidgetTextColorG, s.Taskbar.WidgetTextColorB, isDark);

            // Handle dynamically enabling/disabling plugins
            if (_pluginManager != null)
            {
                foreach (var plugin in _pluginManager.Plugins)
                {
                    var pluginName = plugin.Name;
                    PluginItemSettings? ps = null;
                    bool shouldBeEnabled = true;
                    if (s.Plugins != null && s.Plugins.TryGetValue(pluginName, out ps))
                    {
                        shouldBeEnabled = ps.Enabled;
                    }

                    bool isRunning = _runningPlugins.Contains(plugin);

                    if (shouldBeEnabled && !isRunning)
                    {
                        // Dynamically enable
                        _runningPlugins.Add(plugin);
                        var panel = PluginPanelRight ?? (WidgetContainer.Child as Panel) ?? new Grid();
                        if (ps != null)
                        {
                            if (ps.Alignment == "Left") panel = PluginPanelLeft;
                            else if (ps.Alignment == "Center") panel = PluginPanelCenter;
                        }
                        var context = new PluginHostContext(_hooker!, new WindowInteropHelper(this).Handle, panel, this)
                        {
                            CurrentPlugin = plugin
                        };

                        // Run custom settings mapping
                        if (plugin is AetherBar.Plugins.IPluginWithSettings pws)
                        {
                            var defs = pws.GetSettingDefinitions();
                            foreach (var def in defs)
                            {
                                string val = null;
                                if (ps?.CustomSettings != null) ps.CustomSettings.TryGetValue(def.Key, out val);
                                pws.OnSettingChanged(def.Key, val ?? def.DefaultValue);
                            }
                        }

                        // Initialize
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await plugin.InitializeAsync(context);
                                Dispatcher.Invoke(RefreshSettings);
                            }
                            catch (Exception ex)
                            {
                                try { context.Log($"Dynamic init error for {plugin.Name}: {ex.Message}"); } catch { }
                            }
                        });
                    }
                    else if (!shouldBeEnabled && isRunning)
                    {
                        // Dynamically disable
                        _runningPlugins.Remove(plugin);
                        
                        // Shutdown plugin
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await plugin.ShutdownAsync();
                            }
                            catch { }
                        });

                        // Remove active widgets associated with this plugin
                        var widgetsToRemove = _activePluginWidgets.Where(w => w.Plugin == plugin).ToList();
                        foreach (var widget in widgetsToRemove)
                        {
                            if (widget.Host.Parent is Panel parent)
                            {
                                parent.Children.Remove(widget.Host);
                            }
                            _activePluginWidgets.Remove(widget);
                        }
                    }
                }
            }

            // Remove all plugin hosts from parent panels temporarily to prepare for sorting
            foreach (var info in _activePluginWidgets)
            {
                if (info.Host.Parent is Panel parent)
                {
                    parent.Children.Remove(info.Host);
                }
            }

            // Sort active widgets list
            _activePluginWidgets.Sort((a, b) =>
            {
                var nameA = a.Plugin?.Name ?? a.WidgetName;
                var nameB = b.Plugin?.Name ?? b.WidgetName;
                
                int orderA = s.Plugins != null && s.Plugins.TryGetValue(nameA, out var psA) ? psA.SortOrder : 0;
                int orderB = s.Plugins != null && s.Plugins.TryGetValue(nameB, out var psB) ? psB.SortOrder : 0;
                
                return orderA.CompareTo(orderB);
            });

            // Update plugin layout and styling dynamically
            foreach (var info in _activePluginWidgets)
            {
                var pluginName = info.Plugin?.Name ?? info.WidgetName;
                
                // Get setting for this plugin
                PluginItemSettings? ps = null;
                if (s.Plugins != null)
                {
                    s.Plugins.TryGetValue(pluginName, out ps);
                }

                var alignment = ps?.Alignment ?? "Right";
                var padding = ps?.Padding ?? 6;
                var width = ps?.Width ?? -1;

                // Determine target panel
                Panel targetPanel = PluginPanelRight;
                if (alignment == "Left")
                    targetPanel = PluginPanelLeft;
                else if (alignment == "Center")
                    targetPanel = PluginPanelCenter;

                // Add to target panel
                targetPanel.Children.Add(info.Host);

                // Reset internal padding to safe original value to prevent text cropping
                info.Host.Padding = new Thickness(6, 2, 6, 2);

                // Apply horizontal shift using TranslateTransform
                var translate = info.Host.RenderTransform as TranslateTransform;
                if (translate == null)
                {
                    translate = new TranslateTransform();
                    info.Host.RenderTransform = translate;
                }
                translate.X = padding; // Use padding setting as translation X offset

                // Apply width and min-width
                if (width > 0)
                {
                    info.Host.Width = width;
                    info.Host.MinWidth = 0;
                }
                else
                {
                    info.Host.Width = double.NaN; // Auto / Default
                    info.Host.MinWidth = Math.Min(info.DefaultWidth, 80);
                }
            }

            if (_embedded)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                switch (s.Effects.BackgroundEffect)
                {
                    case "None":
                        DesktopWindowManager.DisableBackdrop(hwnd);
                        break;
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

    private void ApplyBackgroundForCurrentState(MediaInfo? media = null)
    {
        var s = _settingsManager.Current;
        var currentMedia = media ?? _currentMedia;

        if (s.Effects.BackgroundEffect == "None")
        {
            WidgetContainer.Background = Brushes.Transparent;
            return;
        }

        if (_mediaActive && currentMedia?.PlaybackStatus == MediaPlaybackStatus.Playing)
        {
            if (currentMedia.AlbumArt != null && currentMedia.AlbumArt.Length > 0 && s.Effects.AdaptiveTheme)
            {
                var color = DominantColorExtractor.ExtractFromBytes(currentMedia.AlbumArt);
                WidgetContainer.Background = new SolidColorBrush(
                    Color.FromArgb(40, color.R, color.G, color.B));
                return;
            }

            WidgetContainer.Background = Brushes.Transparent;
            return;
        }

        WidgetContainer.Background = s.Effects.EnableDarkMode
            ? new SolidColorBrush(Color.FromArgb(34, 0, 0, 0))
            : new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    }

    private void ApplyAlbumArtSettings()
    {
        var s = _settingsManager?.Current;
        if (s == null) return;

        bool visible = s.Taskbar.ShowAlbumArt && _hasAlbumArt;

        AlbumArtBorder.Width = s.Taskbar.AlbumArtSize;
        AlbumArtBorder.Height = s.Taskbar.AlbumArtSize;
        AlbumArtBorder.CornerRadius = new CornerRadius(s.Taskbar.AlbumArtCornerRadius);
        AlbumArtBorder.Opacity = s.Taskbar.AlbumArtOpacity;
        AlbumArtBorder.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;

        AlbumArtImage.Width = s.Taskbar.AlbumArtSize;
        AlbumArtImage.Height = s.Taskbar.AlbumArtSize;
        AlbumArtImage.Visibility = Visibility.Visible;
    }

    private void StartMarquee()
    {
        StopMarquee();

        if (string.IsNullOrEmpty(SongInfoText.Text) ||
            SongInfoText.Visibility != Visibility.Visible)
            return;

        SongInfoText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        TitleClip.Height = SongInfoText.DesiredSize.Height;

        double containerWidth = TitleClip.ActualWidth;
        double textWidth = SongInfoText.DesiredSize.Width;

        if (containerWidth <= 0 || textWidth <= 0)
        {
            Dispatcher.BeginInvoke(StartMarquee, DispatcherPriority.Background);
            return;
        }

        if (textWidth <= containerWidth + 2)
            return;

        double distance = textWidth - containerWidth + 4;
        double scrollTime = Math.Max(3, distance / 30);
        double pauseTime = 1.5;

        Canvas.SetLeft(SongInfoText, 0);

        var anim = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever
        };

        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseTime))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-distance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseTime + scrollTime))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(-distance, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseTime + scrollTime + pauseTime))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(pauseTime + scrollTime + pauseTime + scrollTime))));

        Storyboard.SetTarget(anim, SongInfoText);
        Storyboard.SetTargetProperty(anim, new PropertyPath("(Canvas.Left)"));

        _marqueeStoryboard = new Storyboard();
        _marqueeStoryboard.Children.Add(anim);
        _marqueeStoryboard.Begin();
    }

    private void StopMarquee()
    {
        _marqueeStoryboard?.Stop();
        _marqueeStoryboard = null;
        Canvas.SetLeft(SongInfoText, 0);
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

        InitializePluginsIfNeeded();
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

    private void InitializePluginsIfNeeded()
    {
        if (_pluginsInitialized) return;
        try
        {
            _pluginManager = new AetherBar.Plugins.PluginManager();
            var pluginsDir = Path.Combine(AppContext.BaseDirectory ?? ".", "plugins");
            Directory.CreateDirectory(pluginsDir);
            _pluginManager.LoadPluginsFromDirectory(pluginsDir);

            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(appData, "AetherBar");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "plugin.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] Plugins discovered: {_pluginManager.Plugins.Count}\r\n");
            }
            catch { }

            _pluginsInitialized = true;
            RefreshSettings();
        }
        catch
        {
        }
    }

    private class PluginHostContext : AetherBar.Plugins.IPluginContext
    {
        private readonly TaskbarHooker _hooker;
        private readonly Panel _container;
        private readonly MainWindow _mainWindow;
        public AetherBar.Plugins.IPlugin? CurrentPlugin { get; set; }

        public PluginHostContext(TaskbarHooker hooker, nint anchor, Panel container, MainWindow mainWindow)
        {
            _hooker = hooker;
            _container = container;
            _mainWindow = mainWindow;
        }

        public nint TaskbarHwnd => _hooker.CurrentTaskbarInfo.TaskbarHwnd;

        public AetherBar.Plugins.PluginWidget CreateWidget(string name, int width, int height)
        {
            try
            {
                try
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var dir = Path.Combine(appData, "AetherBar");
                    Directory.CreateDirectory(dir);
                    var logPath = Path.Combine(dir, "plugin.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now:O}] CreateWidget called: {name}\r\n");
                }
                catch { }

                TextBlock topText = null!;
                TextBlock bottomText = null!;
                StackPanel stack = null!;
                Border host = null!;
                double currentFontSize = 11;

                Brush? CreateColorBrush(string color)
                {
                    try
                    {
                        return new BrushConverter().ConvertFromString(color) as Brush;
                    }
                    catch
                    {
                        return null;
                    }
                }

                void UpdateTextLayoutMetrics()
                {
                    topText.LineHeight = Math.Ceiling(currentFontSize * 1.35);
                    bottomText.FontSize = Math.Max(6, currentFontSize - 2);
                    bottomText.LineHeight = Math.Ceiling(bottomText.FontSize * 1.35);

                    var contentHeight = bottomText.Visibility == Visibility.Visible
                        ? topText.LineHeight + bottomText.LineHeight
                        : topText.LineHeight;
                    host.MinHeight = Math.Max(height, contentHeight + host.Padding.Top + host.Padding.Bottom);
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    topText = new TextBlock
                    {
                        Text = name,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 0),
                        Foreground = (Brush)Application.Current.Resources["TextPrimary"],
                        FontSize = 11,
                        LineHeight = 15,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                        FontWeight = FontWeights.SemiBold,
                        TextAlignment = TextAlignment.Center
                    };

                    bottomText = new TextBlock
                    {
                        Text = string.Empty,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, -2, 0, 0),
                        Foreground = (Brush)Application.Current.Resources["TextSecondary"],
                        FontSize = 9,
                        LineHeight = 13,
                        LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                        TextAlignment = TextAlignment.Center,
                        Visibility = Visibility.Collapsed
                    };

                    stack = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Center,
                    };
                    stack.Children.Add(topText);
                    stack.Children.Add(bottomText);

                    host = new Border
                    {
                        Width = double.NaN,
                        MinWidth = Math.Min(width, 80),
                        Height = double.NaN,
                        MinHeight = height,
                        Child = stack,
                        Background = Brushes.Transparent,
                        Padding = new Thickness(6, 2, 6, 2),
                        CornerRadius = new CornerRadius(4),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(6, 0, 0, 0)
                    };

                    // append after existing children to chain plugins
                    _container.Children.Add(host);
                    try
                    {
                        var info = new ActivePluginWidgetInfo
                        {
                            Plugin = CurrentPlugin,
                            WidgetName = name,
                            Host = host,
                            DefaultWidth = width
                        };
                        _mainWindow._activePluginWidgets.Add(info);
                    }
                    catch (Exception ex)
                    {
                        try { Log($"Failed to register active widget info: {ex.Message}"); } catch { }
                    }
                    try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AetherBar", "plugin.log"), $"[{DateTime.Now:O}] Container children after add: {_container.Children.Count}\r\n"); } catch { }
                });

                // log creation for diagnostics
                try { Log($"Plugin host created: {name} (w:{width} h:{height})"); } catch { }

                var widget = new AetherBar.Plugins.PluginWidget(name, width, height, s =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // Support two-line content: "CPU: x%\nRAM: y%" or inline form
                            if (s.Contains("\n"))
                            {
                                var parts = s.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                topText.Text = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                                bottomText.Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                            }
                            else if (s.Contains("CPU:") && s.Contains("RAM:"))
                            {
                                // try split by RAM label
                                var ramIndex = s.IndexOf("RAM:", StringComparison.OrdinalIgnoreCase);
                                if (ramIndex > 0)
                                {
                                    topText.Text = s.Substring(0, ramIndex).Trim();
                                    bottomText.Text = s.Substring(ramIndex).Trim();
                                }
                                else
                                {
                                    topText.Text = s;
                                    bottomText.Text = string.Empty;
                                }
                            }
                            else
                            {
                                topText.Text = s;
                                bottomText.Text = string.Empty;
                            }

                            bottomText.Visibility = string.IsNullOrEmpty(bottomText.Text) 
                                ? Visibility.Collapsed 
                                : Visibility.Visible;
                            UpdateTextLayoutMetrics();
                        });
                        try { Log($"Plugin widget update: {name} => {s}"); } catch { }
                    }
                    catch (Exception ex)
                    {
                        try { Log($"Plugin widget update failed: {name} => {ex.Message}"); } catch { }
                    }
                }, size =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            currentFontSize = size;
                            topText.FontSize = size;
                            UpdateTextLayoutMetrics();
                        });
                        try { Log($"Plugin widget font size update: {name} => {size}"); } catch { }
                    }
                    catch (Exception ex)
                    {
                        try { Log($"Plugin widget font size update failed: {name} => {ex.Message}"); } catch { }
                    }
                }, offset =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var translate = host.RenderTransform as TranslateTransform;
                            if (translate == null)
                            {
                                translate = new TranslateTransform();
                                host.RenderTransform = translate;
                            }
                            translate.Y = offset;
                        });
                        try { Log($"Plugin widget vertical offset update: {name} => {offset}"); } catch { }
                    }
                    catch (Exception ex)
                    {
                        try { Log($"Plugin widget vertical offset update failed: {name} => {ex.Message}"); } catch { }
                    }
                }, color =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var brush = CreateColorBrush(color);
                            if (brush == null)
                                return;

                            topText.Foreground = brush;
                            bottomText.Foreground = brush;
                        });
                        try { Log($"Plugin widget text color update: {name} => {color}"); } catch { }
                    }
                    catch (Exception ex)
                    {
                        try { Log($"Plugin widget text color update failed: {name} => {ex.Message}"); } catch { }
                    }
                }, (topColor, bottomColor) =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var topBrush = CreateColorBrush(topColor);
                            var bottomBrush = CreateColorBrush(bottomColor);

                            if (topBrush != null)
                                topText.Foreground = topBrush;
                            if (bottomBrush != null)
                                bottomText.Foreground = bottomBrush;
                        });
                        try { Log($"Plugin widget line color update: {name} => {topColor}, {bottomColor}"); } catch { }
                    }
                    catch (Exception ex)
                    {
                        try { Log($"Plugin widget line color update failed: {name} => {ex.Message}"); } catch { }
                    }
                }, tooltip =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            host.ToolTip = tooltip;
                        });
                    }
                    catch { }
                });

                // no native handle when hosted in WPF panel; set handle to 0
                widget.SetHandle(0);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (host != null)
                    {
                        host.MouseDown += (s, me) =>
                        {
                            var pos = me.GetPosition(host);
                            var btn = me.ChangedButton.ToString();
                            if (me.ClickCount >= 2)
                                widget.OnMouseDoubleClick?.Invoke(btn, pos.X, pos.Y);
                            else
                                widget.OnMouseClick?.Invoke(btn, pos.X, pos.Y);
                        };
                        host.MouseEnter += (s, me) => widget.OnMouseHover?.Invoke(true);
                        host.MouseLeave += (s, me) => widget.OnMouseHover?.Invoke(false);
                    }
                });

                return widget;
            }
            catch (Exception ex)
            {
                try { Log($"CreateWidget exception: {ex.Message}"); } catch { }
                return new AetherBar.Plugins.PluginWidget(name, width, height);
            }
        }

        public void Log(string message)
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dir = Path.Combine(appData, "AetherBar");
                Directory.CreateDirectory(dir);
                var logPath = Path.Combine(dir, "plugin.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:O}] {message}\r\n");
            }
            catch { }
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

    public event Action<string, double, double>? OnWidgetMouseClick;
    public event Action<string, double, double>? OnWidgetMouseDoubleClick;
    public event Action<bool>? OnWidgetMouseHover;

    private void OnWidgetPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        var btn = e.ChangedButton.ToString();

        if (e.ChangedButton == MouseButton.Right || e.ChangedButton == MouseButton.Middle)
        {
            var rightClickAction = _settingsManager?.Current.Taskbar.RightClickAction ?? "menu";
            if (rightClickAction == "menu")
            {
                ShowTrayMenu();
                e.Handled = true;
            }
            return;
        }

        if (e.ClickCount >= 2)
        {
            OnWidgetMouseDoubleClick?.Invoke(btn, pos.X, pos.Y);
            var dcAction = _settingsManager?.Current.Taskbar.DoubleClickAction ?? "settings";
            var dcValue = _settingsManager?.Current.Taskbar.DoubleClickValue ?? "";
            HandleWidgetAction(dcAction, dcValue);
        }
        else
        {
            OnWidgetMouseClick?.Invoke(btn, pos.X, pos.Y);
        }
    }

    private void OnWidgetPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
    }

    private static void HandleWidgetAction(string action, string value)
    {
        switch (action)
        {
            case "settings":
                ((App)Application.Current).ShowSettingsWindow();
                break;
            case "url" when !string.IsNullOrEmpty(value):
                try { Process.Start(new ProcessStartInfo { FileName = value, UseShellExecute = true }); } catch { }
                break;
            case "run" when !string.IsNullOrEmpty(value):
                try { Process.Start(new ProcessStartInfo { FileName = value, UseShellExecute = true }); } catch { }
                break;
        }
    }

    private void OnWidgetMouseEnter(object sender, MouseEventArgs e)
    {
        OnWidgetMouseHover?.Invoke(true);
    }

    private void OnWidgetMouseLeave(object sender, MouseEventArgs e)
    {
        OnWidgetMouseHover?.Invoke(false);
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

    private void OnTrayRestart(object sender, RoutedEventArgs e)
    {
        var exePath = Environment.ProcessPath;
        if (exePath != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }
            catch { }
        }
        Application.Current.Shutdown();
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
        menu.Items.Add(MakeTrayItem("Restart", OnTrayRestart, menu));
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
