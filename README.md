# AetherBar

**Open-source Windows Taskbar Widget Engine** — Embed audio visualizers, media info, and live widgets directly into your Windows taskbar via native Win32 embedding.

![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platform](https://img.shields.io/badge/platform-Windows%2011-success)

---

## Demo

![AetherBar Demo](AetherBar.UI/Assets/demo.gif)

*Audio visualizer running in the Windows taskbar with 6 rendering modes.*

---

## Features

### 🎵 Audio Visualizer
Real-time FFT audio visualization at 60fps directly in the taskbar using NAudio WASAPI loopback capture. Six rendering modes:

| Mode | Description |
|------|-------------|
| **Bar** | Vertical bars from bottom edge, one per frequency band |
| **Line** | Filled area under curve + thick outline + glow dots |
| **Dot** | Glowing dot matrix in a grid layout (max 4 rows) |
| **Circle** | Fan-shaped wedges radiating from bottom-center with center pulse |
| **Mirror** | Bars mirrored symmetrically from center outward |
| **Blocks** | Horizontal segmented blocks stacking vertically per frequency |

**8 Color Themes:** Rainbow, Neon Blue, Matrix Green, Fire, Monochrome, Sunset, Ocean, Cyberpunk + Custom R/G/B sliders. Colors are applied left-to-right across the widget width.

### 🎚 Audio Pipeline
- WASAPI loopback capture (all system audio) via NAudio
- 1024-point FFT with Hann window
- Logarithmic frequency binning (40 Hz – 16 kHz, 256 bins, min 2 FFT bins per bar)
- Exponential smoothing (factor 0.35) for fluid motion
- Noise gate (threshold), sensitivity multiplier, and bar start offset (skip low freqs)
- Configurable per-mode: BarCount (8–256), Opacity (0.1–1.0), ShowPeak indicator

### 🖱 Widget Click & Hover Events
Full mouse interaction system for the main widget and plugin widgets:
- **Single Click** on widget triggers configurable action (open URL, run program, or cycle presets)
- **Double Click** on widget with independent action (open Settings, open URL, run program)
- **Right Click** on widget shows tray context menu
- **Hover** events propagate to plugins for color changes and tooltip display
- Plugin widgets support `OnMouseClick`, `OnMouseDoubleClick`, and `OnMouseHover` callbacks
- Plugin tooltips via `SetTooltip()` for hover descriptions

### 📻 Media Info Display
Shows currently playing song metadata from any Windows Media Transport Controls source (Spotify, Chrome, YouTube, Media Player, etc.):
- Title, artist text overlay
- 24×24 album art thumbnail
- Adaptive Theme — extracts dominant color from album art to tint the widget background
- Play/pause state detection

### 🪟 Taskbar Integration
Native Win32 embedding via `SetParent` into `Shell_TrayWnd`:
- Widget is a true CHILD of the taskbar (not overlay/popup)
- **4 position modes:** Left, Center, Right, Auto
- **200ms reposition timer** adapts to icon changes dynamically
- Detects start button width, tray area width, and taskbar orientation (Bottom/Top/Left/Right)
- SetWindowsHookEx(WH_SHELL) for layout change events
- Alt+Tab hidden via WS_EX_TOOLWINDOW

### 🎨 Visual Effects
| Effect | Support |
|--------|---------|
| **Acrylic (Blur)** | Windows 11 via `SetWindowCompositionAttribute` |
| **Mica** | Windows 11+ via `DwmSetWindowAttribute` |
| **Immersive Dark Mode** | Title bar + widget background |
| **Corner Radius** | Configurable 0–12 px |
| **Widget Padding** | Configurable 0–20 px |

### ⚙️ Settings Dashboard
Fluent Design settings window with 4 tabs. Every change applies live — no Save button needed.

- **Visualizer tab:** Mode, Color Theme, Custom Color (R/G/B sliders), Opacity, Bar Count, Sensitivity, Threshold, Bar Start Offset, Show Peak, Visualizer Height
- **Taskbar tab:** Position, Widget Width, Horizontal Offset, Widget Padding, Show Song Title, Show Album Art, Album Art (Size/Corner Radius/Opacity), Text Color (Auto/White/Black/Red/Green/Blue/Cyan/Yellow/Custom + R/G/B), Auto-hide
- **Effects tab:** Background Effect (None/Acrylic/Mica), Corner Radius, Adaptive Theme
- **General tab:** Start with Windows, Start Minimized (tray only), Dark Mode, Game Mode, Check for Updates, Reset to Defaults

### 🔌 Plugin System
Extensible via collectible `AssemblyLoadContext`:
- `IPlugin` interface with `InitializeAsync` / `ShutdownAsync`
- `IPluginContext` provides `TaskbarHwnd` and `CreateWidget()`
- `PluginManager` loads/unloads assemblies at runtime
- `IPluginWithSettings` lets plugins expose custom controls in the Settings window
- `PluginWidget` supports text content, font size, vertical offset, single text color, and separate top/bottom line colors
- Plugin projects can import `AetherBar.Plugins/AetherBar.Plugin.targets` to auto-reference the plugin API and copy outputs to the app `plugins` folder
- Included sample plugins:
  - `Custom Text`: editable taskbar text with font size, vertical offset, and text color settings
  - `System Monitor (Sample)`: live CPU/RAM widget with separate CPU and RAM color settings
- See `PLUGIN_DEVELOPMENT.md` for the plugin project template, lifecycle rules, settings API, and sample plugin patterns.

### 🖥 System Tray Icon
H.NotifyIcon.Wpf `TaskbarIcon` with context menu (Show/Hide, Settings, Exit). Icon loaded from multi-resolution `.ico` (16×16 – 256×256).

### 🎮 Game Mode
Detects fullscreen foreground windows (games) via polling `GetForegroundWindow` — auto-hides the widget.

### 🎨 Theme System
- **Dark/Light mode** via WPF-UI `ApplicationThemeManager.Apply()`
- **Red accent color** across tabs, icons, and controls
- 25+ custom resource brushes for Surface, Text, Card, ComboBox, Slider, Tab colors
- Tool window style hidden from Alt+Tab

---

## Architecture

```
AetherBar.slnx
├── AetherBar.Core         — Core logic library
│   ├── Audio/             — NAudio WASAPI capture + FFT pipeline
│   ├── Media/             — WinRT media metadata + DominantColorExtractor
│   ├── Models/            — AudioData, MediaInfo, TaskbarInfo
│   ├── Settings/          — AetherBarSettings (JSON persistence)
│   └── Visualizer/        — IVisualizerRenderer + 6 modes + color engine
├── AetherBar.Hooker       — Win32 interop library
│   ├── Interop/           — NativeMethods, DesktopWindowManager (DWM)
│   ├── TaskbarHooker.cs   — Find Shell_TrayWnd, SetParent, positioning
│   ├── TaskbarWatcher.cs  — WH_SHELL hook for layout changes
│   └── GameModeDetector.cs— Fullscreen app detection
├── AetherBar.UI           — WPF application (WinExe)
│   ├── MainWindow.xaml    — Widget window (tray icon, visualizer, album art)
│   ├── SettingsWindow.xaml— 4-tab settings dialog (Fluent Design)
│   ├── Visualizers/       — VisualizerControl (FrameworkElement)
│   ├── Styles/            — FluentStyles.xaml, LightTheme.xaml
│   └── Assets/            — AetherBar.ico (multi-resolution)
├── AetherBar.Plugins      — Plugin interface, shared plugin targets, PluginManager
├── AetherBar.Plugins.CustomText
│                           — Sample configurable text plugin
├── AetherBar.Plugins.SampleSystemMonitor
│                           — Sample CPU/RAM plugin
├── PLUGIN_DEVELOPMENT.md   — Plugin authoring guide
└── AetherBar.Tests         — xUnit unit tests (6 tests)
```

### Data Flow

```
System Audio → NAudio WASAPI → FFT (1024, Hann, log bins)
                                    ↓
                              VisualizerController
                                    ↓
                              VisualizerControl.OnRender()
                                    ↓
                              IVisualizerRenderer.Render()
                                    ↓
                              DrawingContext (WPF, 60fps)

WinRT Media Session → MediaManager (1s poll) → MainWindow UI update
                                                      ↓
                                              DominantColorExtractor
                                                      ↓
                                              Adaptive background tint

Taskbar Layout → WH_SHELL hook → TaskbarHooker.RefreshTaskbarInfo()
                                          ↓
                                  200ms timer → RepositionWidget()
```

### Tech Stack

| Component | Technology |
|-----------|-----------|
| Framework | .NET 8 WPF (`net8.0-windows10.0.26100.0`) |
| Audio | NAudio 2.3.0 (WASAPI loopback + FFT) |
| Win32 | PInvoke.User32 / PInvoke.Kernel32 |
| WinRT | Microsoft.Windows.CsWinRT (Windows.Media.Control) |
| Tray Icon | H.NotifyIcon.Wpf 2.3.2 |
| UI Theme | WPF-UI 4.2.1 |
| Testing | xUnit 2.5.3 + coverlet |

---

## Requirements

- **OS:** Windows 11
- **Runtime:** [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Permission:** Must run as **Administrator** (required for `SetParent` into `Shell_TrayWnd`)

---

## Getting Started

### Build & Run

```bash
git clone https://github.com/Lehoangf/AetherBar.git
cd AetherBar
dotnet restore AetherBar.slnx
dotnet build AetherBar.slnx --configuration Release
.\AetherBar.UI\bin\Release\net8.0-windows10.0.26100.0\AetherBar.UI.exe
```

If AetherBar is already running while you build, Windows may lock plugin DLLs under `AetherBar.UI/bin/.../plugins/`. Exit AetherBar before rebuilding plugins if MSBuild reports copy warnings for plugin DLLs.

### One-click Install

Run `install.cmd` as Administrator to install dependencies, build, and optionally register for automatic startup.

### Plugin Development

Use `PLUGIN_DEVELOPMENT.md` as the source of truth for creating plugins. The shortest in-repo plugin project file is:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="..\AetherBar.Plugins\AetherBar.Plugin.targets" />
</Project>
```

Each plugin should include a `plugin.json` manifest and one exported class implementing `IPlugin` or `IPluginWithSettings`. Build output is copied automatically into the app `plugins` folder.

---

## Settings

Settings are persisted as JSON at `%LOCALAPPDATA%\AetherBar\settings.json`.

### Visualizer (per mode)

| Setting | Default | Range |
|---------|---------|-------|
| BarCount | 32 | 8–256 |
| Sensitivity | 1.0 | 0.1–3.0 |
| Threshold | 0.0 | 0.0–0.5 |
| BarStartOffset | 0 | 0–250 |
| ColorTheme | "Rainbow" | 9 themes |
| Opacity | 0.5 | 0.1–1.0 |
| ShowPeak | true | bool |
| VisualizerHeight | 22 | 10–48 |

### Taskbar

| Setting | Default | Range |
|---------|---------|-------|
| WidgetWidth | 180 | 100–800 |
| Position | "Auto" | Left/Center/Right/Auto |
| OffsetX | 0 | –100 – 2000 |
| WidgetPadding | 2 | 0–20 |
| WidgetTextColor | "Auto" | Auto/White/Black/Red/Green/Blue/Cyan/Yellow/Custom |
| ShowSongTitle | true | bool |
| ShowAlbumArt | true | bool |
| AlbumArtSize | 24 | 16–48 |
| AlbumArtCornerRadius | 4 | 0–24 |
| AlbumArtOpacity | 1.0 | 0.1–1.0 |
| DoubleClickAction | "settings" | settings / url / run / nothing |
| DoubleClickValue | "" | URL or program path |
| RightClickAction | "menu" | menu / nothing |

### Effects

| Setting | Default | Options |
|---------|---------|---------|
| BackgroundEffect | "Transparent" | None/Acrylic (Blur)/Mica |
| CornerRadius | 4 | 0–12 |
| AdaptiveTheme | true | bool |
| EnableDarkMode | true | bool |

### General

| Setting | Default |
|---------|---------|
| StartWithWindows | false |
| StartMinimized | true |
| EnableGameMode | true |
| CheckForUpdates | true |

### Plugins

Plugins have shared layout controls in the Settings window:

| Setting | Description |
|---------|-------------|
| Enabled | Turns a plugin on or off |
| Alignment | Left, Center, or Right plugin panel |
| SortOrder | Display order within its panel |
| Padding | Horizontal offset for the plugin widget |
| Width | Fixed widget width, or auto when unset |

Plugins may also expose their own settings through `IPluginWithSettings`.

| Plugin | Custom Settings |
|--------|-----------------|
| Custom Text | Text Content, Font Size, Vertical Offset, Text Color, Single/Double Click Action & Value, Hover Action, Hover Color, Hover Tooltip |
| System Monitor (Sample) | CPU Color, RAM Color, Single/Double Click Action & Value, Hover Action, Hover Color, Hover Tooltip |

---

## Project Status

| Phase | Status |
|-------|--------|
| 1 — Taskbar hooking, Win32 interop, dynamic spacing | ✅ |
| 2 — Audio capture (FFT), media metadata, dominant color | ✅ |
| 3 — Visualizer rendering (Bar/Line/Dot/Circle), tray icon | ✅ |
| 4 — Settings dashboard, Acrylic/Mica, dark/light theme | ✅ |
| 5 — Plugin marketplace, scripting support | 🔜 |

---

## Changelog

### v0.2.3 (2026-05-27)
- **Power Button**: Shutdown button (⏻) in Settings title bar with red accent color, centered vertically
- **Settings UX Polish**: Title bar buttons aligned and styled consistently
- **Widget Right-Click**: Removed custom popup menu, right-click passes through to system

### v0.2.2 (2026-05-25)
- **Widget Click/Hover Events**: Single/double/right-click actions (open URL, run program, settings) and hover events with tooltip support
- **Plugin Click/Hover**: `OnMouseClick`, `OnMouseDoubleClick`, `OnMouseHover` callbacks with configurable actions, color changes, and tooltips
- **Album Art Settings**: Configurable size (16–48px), corner radius, and opacity
- **Visualizer Height**: Adjustable height slider (10–48px), window auto-sizes to content
- **Separate Media Controls**: Independent toggles for "Show Song Title" and "Show Album Art"
- **Marquee Scrolling**: Long song titles now scroll left-right with pause at each end
- **Plugin improvements**: Better lifecycle, non-nullable refactoring, async initialization
- **Bug fixes**: Mode switching, audio freeze workaround, adaptive theme timing, double-click debounce

### v0.2.1
- Restart tray option, audio freeze fix

### v0.2.0
- Initial public release

---

## License

MIT License — see [LICENSE](LICENSE) for details.
