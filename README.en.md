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

DeskShift brings wallpaper colors to your Windows desktop. Extract a palette from an image, then update the wallpaper and Windows colors with one click. Export color-only themes for Chrome and Edge.

> **Minimal new tab**: original wallpaper + search + most-visited sites. No clock, date or news. Chrome / Edge sync the new tab automatically; tab-strip themes still load manually.

Website shortcuts use the browser’s original cached favicons and sit directly below the search box. Browser title-bar themes must be loaded separately through the theme guide; new-tab syncing does not automatically update an installed theme.

## Preview

![DeskShift desktop app — current UI is Simplified Chinese](docs/media/app-preview.png)

A compact default window (about 940 × 660) separates wallpapers, switching settings and browser integration through a sidebar. Grouped settings use rounded cards and switches; smaller screens get a constrained window with scrolling.

![DeskShift switching settings](docs/media/settings-preview.png)

## Features

| Feature | What it does |
| :--- | :--- |
| 🎨 Automatic palettes | Import JPG, PNG or BMP; extract five colors and a light/dark scheme; adjust the accent manually |
| 🖥️ Desktop sync | Apply the wallpaper, Windows accent and light/dark mode |
| 🌐 Minimal new tab | Original wallpaper, local colors, search and up to eight real most-visited sites |
| ✨ Fullscreen motion | Five cover/reveal effects and six geometric waiting designs, with nonrepeating random selection |
| ⚡ Warm startup | Prewarm hidden windows, start the clock after first paint, reuse windows and cache artwork |
| 🧭 Tray switching | Right-click → select a wallpaper; the active wallpaper is checked |
| 🫧 Floating orb | Optional draggable desktop shortcut with a thumbnail picker; remembers its position |
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

Enable the floating orb in the switching settings under desktop shortcuts. Click it to pick a wallpaper, drag to reposition it, and click outside the picker or press Esc to dismiss. Its right-click menu opens the app or hides the orb. It is off by default; visibility and position are saved.

### Minimal new tab

Shortcuts can be selected automatically from the browser's most-visited sites or added manually with **Add website**. Manual links appear first, with automatic suggestions filling the remaining slots. Hover a tile and choose **⋯** to edit, delete or hide it. Turn off automatic suggestions in the dialog for a manual-only list. Hidden suggestions can be restored. Preferences are stored locally in the current browser and survive restarts.


Load `browser/chrome` or `browser/edge` through Developer mode at `chrome://extensions` or `edge://extensions`. Keep the updated DeskShift running and apply a wallpaper. This replaces the new-tab page; startup URLs, home-button URLs and the browser's default search setting are unchanged.

The page shows only wallpaper, search and up to eight most-visited sites from `topSites`, with host deduplication. It does not request full `history` access or send site lists anywhere. Chrome searches Google; Edge searches Bing. Browser-ranked sites can differ from Edge's pinned shortcuts. An empty history shows a short hint.

Desktop originals are preserved; browser backgrounds use separate JPEG copies (up to 2560 pixels on the longest edge, quality 82) and only the latest image is cached for offline use. Hidden tabs stop polling. Exported tab-strip themes still require manual loading. Optional checks: `node tests/browser-sync.js`; isolated visual preview: `node tests/newtab-preview.js`.

![Minimal new tab; sample sites for preview](docs/media/newtab-preview.png)

## Tech stack

| Layer | Technology | Role |
| :--- | :--- | :--- |
| Desktop | C# · .NET Framework 4.x · WinForms | Wallpaper library, controls, preview and tray |
| Graphics | System.Drawing · GDI+ | Sampling, RGB color buckets, shaped overlays and geometry |
| Windows | Win32 P/Invoke · Registry · `.theme` files | Full-resolution wallpaper, accent, light/dark mode and undo |
| Animation resources | Background tasks · First-paint barrier · Window pool · LRU cache | Preparation, reuse and shared waiting frames across displays |
| Browser extension | JavaScript · HTML · CSS · Manifest V3 · topSites | Minimal new tab, most-visited sites and wallpaper cache |
| Local bridge | C# · TCP / HTTP · JSON | Read-only wallpaper and palette API with host/origin/revision checks |
| Build & validation | PowerShell · Framework C# compiler · Built-in self-tests | Build without package dependencies; hidden-window checks |

### Layout

```text
src/                    Desktop app, Windows integration, animation and themes
browser/chrome/         Chrome minimal new-tab extension and guide
browser/edge/           Edge minimal new-tab extension and guide
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
- The extension requests only storage, top sites and local connection access. The app does not edit browser profiles. Node is not required to build or run it.
- [Detailed Chinese guide](docs/GUIDE.zh-CN.md).

## References and credits

[Chrome themes](https://developer.chrome.com/docs/extensions/develop/ui/themes) · [Windows wallpaper API](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-systemparametersinfow) · [DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush)

Original geometric animations, with design references from [MetaLab](https://dribbble.com/shots/4037784-Geometric-shapes-loading-animation), [Infinity Loader](https://dribbble.com/shots/2465356-Infinity-Loader) and [IBM Design](https://www.ibm.com/design/language/animation/tips-and-techniques/). Bundled wallpapers were supplied by the user; project authorship does not imply ownership of the wallpaper artwork.

## Visitors

<div align="center">
<a href="https://github.com/journey-ad/Moe-Counter">
<img src="https://moecount.lolihouse.top/get/@jax367-deskshift?theme=moebooru" alt="DeskShift Moe Counter — Moebooru theme" />
</a>

Based on [Moe Counter](https://github.com/journey-ad/Moe-Counter), hosted by [Lolihouse](https://moecount.lolihouse.top/). GitHub image caching affects request counts; this is not a unique-visitor metric.

**Lar** · [turelar367@gmail.com](mailto:turelar367@gmail.com)

</div>

Version backups and downloads: [stable and experimental builds](docs/BACKUP-VERSIONS.md).
