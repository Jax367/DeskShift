<div align="center">

<img src="docs/media/hero.svg" width="100%" alt="DeskShift — One wallpaper. A whole new mood." />

### One wallpaper. A whole new mood.

[简体中文](README.md) · [English](README.en.md) · [日本語](README.ja.md)

<img src="https://img.shields.io/badge/Windows-10%20%2F%2011-526f5d?style=flat-square&amp;logo=windows" alt="Windows 10 / 11" />
<img src="https://img.shields.io/badge/C%23-.NET%20Framework-797593?style=flat-square" alt="C# / .NET Framework" />
<img src="https://img.shields.io/badge/UI-WinForms-84bdb2?style=flat-square" alt="WinForms" />
<img src="https://img.shields.io/badge/Chrome-Manifest%20V3-b4aa8d?style=flat-square&amp;logo=googlechrome&amp;logoColor=white" alt="Chrome Manifest V3" />
<img src="https://img.shields.io/github/stars/Jax367/DeskShift?style=flat-square&amp;color=797593" alt="GitHub stars" />

**[Get started](#get-started) · [Tech stack](#tech-stack) · [Report an issue](https://github.com/Jax367/DeskShift/issues)**

</div>

DeskShift brings wallpaper colors to your Windows desktop. Extract a palette from an image, then update the wallpaper, Windows colors and Chrome's new tab with one click.

> **Original clarity**: desktop wallpapers are cached losslessly at their original resolution. Chrome's new tab follows colors only. Full browser themes are exported and loaded manually.

## Preview

![DeskShift desktop app — current UI is Simplified Chinese](docs/media/app-preview.png)

## Features

| Feature | What it does |
| :--- | :--- |
| 🎨 Automatic palettes | Import JPG, PNG or BMP; extract five colors and a light/dark scheme; adjust the accent manually |
| 🖥️ Desktop sync | Apply the wallpaper, Windows accent and light/dark mode |
| 🌐 Chrome colors | Sync new-tab colors locally; export a complete static Chrome theme |
| ✨ Fullscreen motion | Five cover/reveal effects and six geometric waiting designs, with nonrepeating random selection |
| ⚡ Warm startup | Prewarm hidden windows, start the clock after first paint, reuse windows and cache artwork |
| 🧭 Tray switching | Right-click → select a wallpaper; the active wallpaper is checked |
| ↩️ One-step undo | Persist the previous wallpaper and registry values changed by the app across restarts |
| 🏠 Local processing | Extract palettes and process images on your computer |

<details>
<summary><b>Explore the motion designs</b></summary>

Diagonal sweep, split curtains, alternating bands, circular iris and staggered tiles.

![Static transition previews](docs/media/transition-preview.png)

Orbital rings, infinity loop, modular grid, rhythmic waves, breathing rosette and folded prism. These images are static previews.

![Static waiting-animation previews](docs/media/waiting-preview.png)

</details>

## Get started

**Requirements**: Windows 10 / 11 and .NET Framework 4.x. Build with the Windows framework compiler; no .NET SDK, NuGet, Node or Python is required.

```powershell
git clone https://github.com/Jax367/DeskShift.git
cd DeskShift
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\DeskShift.exe
```

1. Choose a bundled wallpaper or import an image with the add button or drag and drop.
2. Set the accent, light/dark mode, Windows sync and transition options.
3. Click the main apply button. Closing the window keeps the app in the tray; right-click to switch wallpapers.

This repository contains source code. The build script generates the executable. The app UI is currently **Simplified Chinese**; these READMEs are available in three languages.

### Chrome extension

Open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, and select `dist/browser/chrome` after building. Keep DeskShift running and apply a wallpaper at least once. The extension replaces your existing new-tab page.

| Scope | How it works |
| :--- | :--- |
| Wallpaper and Windows colors | One-click application |
| Chrome new-tab colors | Automatic polling every 2.5 seconds after extension installation |
| Complete Chrome tab/toolbar theme | Manual export and installation; no automatic switching |

## Tech stack

| Layer | Technology | Role |
| :--- | :--- | :--- |
| Desktop | C# · .NET Framework 4.x · WinForms | Wallpaper library, controls, preview and tray |
| Graphics | System.Drawing · GDI+ | Sampling, RGB color buckets, shaped overlays and geometry |
| Windows | Win32 P/Invoke · Registry · `.theme` files | Full-resolution wallpaper, accent, light/dark mode and undo |
| Animation resources | Background tasks · First-paint barrier · Window pool · LRU cache | Preparation, reuse and shared waiting frames across displays |
| Browser | JavaScript · HTML · CSS · Chrome Manifest V3 | New-tab color sync and transitions |
| Local bridge | C# HTTP bridge · JSON | Read-only endpoints on `127.0.0.1:47831` |
| Build & validation | PowerShell · Framework C# compiler · Built-in self-tests | Build without package dependencies; hidden-window checks |

### Layout

```text
src/                    Desktop app, Windows integration, animation and bridge
browser/chrome/         Chrome new-tab extension
assets/                 App icon and three original wallpapers
tests/                  Animation profiling
tools/                  Icon generation
docs/                   Guides and preview assets
build.ps1               Build entry point
```

## Validation and behavior

```powershell
$p = Start-Process .\dist\DeskShift.exe -ArgumentList '--self-test' -Wait -PassThru
$p.ExitCode
Start-Process .\dist\DeskShift.exe -ArgumentList '--smoke' -Wait
```

Self-tests and smoke previews do not change the real desktop. Outputs are saved in `test-output`. Actual Windows theme refresh and Chrome installation require checks on the target system.

- Windows light mode does not support Start/taskbar accent colors; some apps need reopening to refresh.
- Prewarming reduces initialization costs. `DwmFlush` only synchronizes this app, not every application on the desktop.
- Data lives in `%LOCALAPPDATA%\DeskShift`. The app does not register itself to launch at startup.
- The local bridge checks Host and Origin and exposes read-only endpoints. The extension requests only its own storage and local connection access.
- [Detailed Chinese guide](docs/GUIDE.zh-CN.md).

## References and credits

[Chrome themes](https://developer.chrome.com/docs/extensions/develop/ui/themes) · [Windows wallpaper API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow) · [DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush)

Original geometric animations, with design references from [MetaLab](https://dribbble.com/shots/4037784-Geometric-shapes-loading-animation), [Infinity Loader](https://dribbble.com/shots/2465356-Infinity-Loader) and [IBM Design](https://www.ibm.com/design/language/animation/tips-and-techniques/). Bundled wallpapers were supplied by the user; project authorship does not imply ownership of the wallpaper artwork.

## Visitors

<div align="center">
<a href="https://github.com/journey-ad/Moe-Counter">
<img src="https://count.getloli.com/@jax367-deskshift?name=jax367-deskshift&amp;theme=miku&amp;padding=7&amp;offset=0&amp;align=center&amp;scale=0.8&amp;pixelated=1&amp;darkmode=auto" alt="DeskShift Moe Counter — Miku theme" />
</a>

Powered by [Moe Counter](https://github.com/journey-ad/Moe-Counter). GitHub image caching affects request counts; this is not a unique-visitor metric.

**Lar** · [turelar367@gmail.com](mailto:turelar367@gmail.com)

</div>
