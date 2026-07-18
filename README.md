# AetherBar

**Open-source Windows Taskbar Widget Engine** — Embed audio visualizers, media info, and live widgets directly into your Windows taskbar via native Win32 embedding.

![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![Platform](https://img.shields.io/badge/platform-Windows%2011-success)

---

## Demo

![AetherBar Demo](https://raw.githubusercontent.com/Lehoangf/AetherBar/master/AetherBar.UI/Assets/demo.gif)

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

**9 Color Themes:** Rainbow, Neon Blue, Matrix Green, Fire, Monochrome, Sunset, Ocean, Cyberpunk + Custom (up to 10 colors with individual R/G/B sliders).  
**Animated Gradient** — smooth color cycling across all themes with 3 direction modes (MoveLeft, MoveRight, Wave), adjustable speed (0.1–5.0), and seamless interpolation. Wave mode uses sin reflection for butter-smooth transitions.  
**Album Art Color** option — visualizer dynamically matches the dominant color extracted from the current album art (mutually exclusive with Animated Gradient — checking one disables the other).

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
| **Immersive Dark Mode** | Title bar + widget background |
| **Corner Radius** | Configurable 0–12 px (fixed — now properly applies to album art avatar) |
| **Widget Padding** | Configurable 0–20 px |

### ⚙️ Settings Dashboard
Fluent Design settings window with 4 tabs. Every change applies live — no Save button needed.

- **Visualizer tab:** Mode, Color Theme, Custom Color (up to 10 colors, each with R/G/B sliders), Animated Gradient (direction + speed), Opacity, Bar Count, Sensitivity, Threshold, Bar Start Offset, Show Peak, Visualizer Height
- **Taskbar tab:** Position, Widget Width, Horizontal Offset, Widget Padding, Show Song Title, Show Album Art, Album Art (Size/Corner Radius/Opacity), Text Color (Auto/White/Black/Red/Green/Blue/Cyan/Yellow/Custom + R/G/B), Auto-hide
- **Effects tab:** Corner Radius, Adaptive Theme
- **General tab:** Start with Windows (via Task Scheduler — works with admin elevation), Start Minimized (tray only), Dark Mode, Reset to Defaults

### 🔌 Plugin System
Extensible via collectible `AssemblyLoadContext`:
- `IPlugin` interface with `InitializeAsync` / `ShutdownAsync`
- `IPluginContext` provides `TaskbarHwnd` and `CreateWidget()`
- `PluginManager` loads/unloads assemblies at runtime
- `IPluginWithSettings` lets plugins expose custom controls in the Settings window
- `PluginWidget` supports text content, font size, vertical offset, text color, separate top/bottom line colors, opacity, text wrapping, max width, text alignment, and SVG icons (via string names → host-created WPF Path/Rectangle on UI thread to avoid cross-thread exceptions)
- Icon styling: `SetIconColor()`, `SetIconSize()`, `SetIconSpacing()` with immediate re-render
- Plugin projects can import `AetherBar.Plugins/AetherBar.Plugin.targets` to auto-reference the plugin API and copy outputs to the app `plugins` folder
- Included plugins:
  - `Custom Text`: editable taskbar text with font size, vertical offset, and text color settings
  - `System Monitor (Sample)`: live CPU/RAM widget with separate CPU and RAM color settings
  - `Media Player`: media playback controls with play/pause/next/previous buttons, playing/idle colors, and icon customization
  - **`Lyrics`**: real-time synced lyrics display from LRCLIB with optional Spicetify WebSocket support for sub-second position accuracy
- See `PLUGIN_DEVELOPMENT.md` for the plugin project template, lifecycle rules, settings API, and sample plugin patterns.

### 🎤 Lyrics Plugin
Displays synced lyrics for the currently playing song, line by line, with real-time position tracking.

**How it works:**
- Fetches synced lyrics (LRC format) from the free [LRCLIB](https://lrclib.net/) API by title + artist
- Falls back to unsynced (plain) lyrics when synced version is unavailable
- Shows "♪ Instrumental ♪" for instrumental sections
- Current line highlighted in gold (`#FFD700`), other lines in white
- Caches lyrics per track (max 20 entries)

**Settings:**
| Setting | Default | Description |
|---------|---------|-------------|
| Font Size | 10 | Text size (8–20) |
| Text Color | #FFFFFF | Default text color |
| Synced Line Color | #FFD700 | Color for current playing line |
| Offset (ms) | 0 | Timing offset (+ earlier, − later) |
| Text Alignment | center | left / center / right when text wraps |

**Position source:**
By default, position is read from Windows SMTC (System Media Transport Controls) which works with any audio source (Spotify, YouTube, Chrome, etc.) but has ~200–500ms latency. For sub-second accuracy with Spotify, install the Spicetify WebSocket extension (see below).

#### Spicetify WebSocket Setup (optional, Spotify only)

The Lyrics plugin includes a built-in WebSocket server that connects to a Spicetify extension for real-time Spotify playback position (< 100ms latency).

**1. Install Spicetify**

If you haven't already, install Spicetify via PowerShell:

```powershell
iwr -useb https://raw.githubusercontent.com/spicetify/cli/master/install.ps1 | iex
spicetify apply
```

**2. Install the WebSocket extension**

Download [`spicetify-websocket-client.js`](https://github.com/19EB/spicetify-websocket-client) and place it in:

```
%APPDATA%\spicetify\Extensions\spicetify-websocket-client.js
```

Then copy it to the Spotify apps extensions folder and apply:

```powershell
Copy-Item "$env:APPDATA\spicetify\Extensions\spicetify-websocket-client.js" "$env:APPDATA\Spotify\Apps\xpui\extensions\" -Force
spicetify apply
```

**3. Restart Spotify**

The extension connects as a WebSocket client to `ws://127.0.0.1:9090`. AetherBar's Lyrics plugin hosts the server — no extra software needed.

**How it works:**
- On startup, the Lyrics plugin starts a WebSocket server on port `9090`
- The Spicetify extension connects to it automatically when Spotify launches
- Spotify track changes and position updates are pushed in real time
- When Spicetify is connected, lyrics sync uses its position; otherwise falls back to SMTC
- If you switch away from Spotify (e.g. to YouTube), lyrics automatically switch to SMTC data

### 🖥 System Tray Icon
H.NotifyIcon.Wpf `TaskbarIcon` with context menu (Show/Hide, Settings, Exit). Icon loaded from multi-resolution `.ico` (16×16 – 256×256).

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
├── AetherBar.Plugins.Lyrics
│                           — Synced lyrics plugin (LRCLIB + Spicetify WebSocket)
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

WinRT Media Session → MediaManager (100ms poll) → MainWindow UI update
                                                       ↓
                                               DominantColorExtractor
                                                       ↓
                                               Adaptive background tint

LRCLIB API → LyricsPlugin → Synced LRC lines → 100ms sync timer → Taskbar text
         ↕                                                      ↕
Spicetify WebSocket (port 9090) ←→ Real-time position ←→ Line highlight

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
| ColorTheme | "Rainbow" | 9 themes + Custom |
| AnimatedGradient | false | bool |
| GradientDirection | "MoveRight" | MoveLeft / MoveRight / Wave |
| GradientSpeed | 1.0 | 0.1–5.0 |
| CustomGradientColors | [] | List of hex colors (2–10) |
| Opacity | 0.5 | 0.1–1.0 |
| ShowPeak | true | bool |
| AlbumArtColor | false | bool |
| AlbumArtMinLightness | 0.3 | 0.0–1.0 |
| AlbumArtMaxLightness | 0.85 | 0.0–1.0 |
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
| CornerRadius | 4 | 0–12 |
| AdaptiveTheme | true | bool |
| EnableDarkMode | true | bool |

### General

| Setting | Default |
|---------|---------|
| StartWithWindows | false (via Task Scheduler) |
| StartMinimized | true |

### Plugins

Plugins have shared layout controls in the Settings window:

| Setting | Description |
|---------|-------------|
| Enabled | Turns a plugin on or off |
| Alignment | Left, Center, or Right plugin panel |
| SortOrder | Display order within its panel |
| Padding | Horizontal offset for the plugin widget |
| Width | Fixed widget width, or auto when unset |
| Opacity | Per-plugin transparency (0.0–1.0) |
| VerticalOffset | Per-plugin vertical position shift |

Plugins may also expose their own settings through `IPluginWithSettings`.

| Plugin | Custom Settings |
|--------|-----------------|
| Custom Text | Text Content, Font Size, Vertical Offset, Text Color, Single/Double Click Action & Value, Hover Action, Hover Color, Hover Tooltip |
| System Monitor (Sample) | CPU Color, RAM Color, Single/Double Click Action & Value, Hover Action, Hover Color, Hover Tooltip |
| Media Player | Playing Color, Idle Color, Hover Color, Icon Size, Icon Spacing, Hide When Idle |
| **Lyrics** | Font Size, Text Color, Synced Line Color, Offset (ms), Text Alignment |

---

## Project Status

| Phase | Status |
|-------|--------|
| 1 — Taskbar hooking, Win32 interop, dynamic spacing | ✅ |
| 2 — Audio capture (FFT), media metadata, dominant color | ✅ |
| 3 — Visualizer rendering (Bar/Line/Dot/Circle), tray icon | ✅ |
| 4 — Settings dashboard, Acrylic/Mica/Game Mode removed, dark/light theme | ✅ |
| 4.1 — Lyrics plugin (LRCLIB + Spicetify WebSocket) | ✅ |
| 5 — Plugin marketplace, scripting support | 🔜 |

---

## License

MIT License — see [LICENSE](LICENSE) for details.
