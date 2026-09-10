# 🛡️ WallSafe

> **Next-Generation Windows Desktop Anime Wallpaper Manager with Instant Panic Hotkey Protection — now with a built-in Taskbar Transparency engine (merged from TranslucentTB).**

WallSafe is a sleek, modern Windows 11 desktop application crafted in WPF / .NET that lets you explore, download, and set high-resolution anime wallpapers from top booru boards—all while keeping your privacy safe with a hardware-level instant panic concealment system.

**As of this build, WallSafe also natively controls the appearance of your Windows taskbar** — making it translucent, blurred, or acrylic with a custom tint color. This capability is a native C# reimplementation of [TranslucentTB](https://github.com/TranslucentTB/TranslucentTB)'s core engine, integrated directly into WallSafe's settings, tray menu, startup lifecycle and panic hotkey. No second process, no extra install.

---

## ✨ Key Features

- **🌐 Multi-Source Booru Aggregation**
  - Stream thousands of wallpapers from **konachan.net** (SFW), **konachan.com**, and **yande.re**.
  - Add and manage custom Danbooru / Moebooru boards with SFW classification.
  - Quick top-bar SFW / NSFW toggle to switch between safe browsing and full catalog.

- **⚡ Instant In-Card Actions**
  - Always-visible action bar on every wallpaper card:
    - ❤️ **Favorite**: Instantly save to your local offline favorites collection.
    - ⚡ **Apply Wallpaper**: Set desktop wallpaper immediately with one click.
    - 📥 **Download**: Download original high-resolution art to your pictures folder.
    - ↗️ **Source**: Open the original booru post in your browser.

- **✨ Dynamic Source-Aware Universe & Series Discovery**
  - **Live Board Ranking**: Popular franchises automatically rank based on real-time booru popularity and post counts on the currently active image board (e.g. Blue Archive #1 with 47k+ on Yande.re, Vocaloid #1 on Konachan).
  - **Live Artwork Counts & Badges**: Each card displays live post counts (e.g. `47.6k Artworks`), genre badges (*Anime*, *Gaming*, *Custom*), and representative backdrop art directly from the active board.
  - **Visual 16:9 Banner Cards**: Rounded cards (`CornerRadius="8"`) with dark gradient title overlays and subtle hover elevation.
  - **Custom Series Manager**: Add your own custom series tags with auto-fetched representative booru thumbnails.

- **♾️ Pixiv-Style Infinite Scroll & In-Memory Caching**
  - Smooth, non-blocking infinite scrolling with subtle bottom-loader indicators.
  - Multi-tier cache architecture: LRU in-memory bitmap cache for 0ms card re-renders + content-addressable disk cache.

- **🔍 Progressive High-Res Preview Modal**
  - Double-click any wallpaper for full-size inspection.
  - Instant progressive render: instantly displays the cached thumbnail while fetching and decoding crisp 1080p/4K high-res samples in the background.

- **🚨 Global Panic Hotkey & Stealth Suite**
  - **Instant Panic Switch**: Press the global shortcut (default `Ctrl + Shift + W`, fully customizable up to 3 keys) anywhere—even inside full-screen games.
  - **Bimodal Restore**: First press switches desktop to default Windows 11 safe wallpaper and hides WallSafe; second press restores your previous anime wallpaper.
  - **Concealment Actions**: Optional auto-minimize all windows (`Win+D` simulation) and master desktop mute.

- **🔄 Automated Slideshow Engine**
  - Automatically cycle through your favorite or downloaded wallpapers.
  - Configurable rotation intervals (5m, 15m, 30m, 1h, 4h) with multi-monitor targeting support.

- **🚀 Windows Startup & Background Mode**
  - Option to auto-launch WallSafe on Windows boot via registry integration.
  - **Start in Background / System Tray**: Silently loads into the system tray and aggressively compacts process memory (`AppSuspensionManager`) without popping up windows.

- **🪟 Built-in Taskbar Transparency (merged from TranslucentTB)**
  - Make the Windows taskbar **Normal / Opaque / Clear / Blur / Acrylic** — the same states TranslucentTB offers.
  - **Custom tint color** via `#AARRGGBB` hex, with quick presets (Dark, Light, Indigo, Clear) and a live swatch.
  - **Live apply**: changes take effect instantly as you adjust them in *Settings → Taskbar Appearance*.
  - **Multi-monitor aware**: applies to the primary taskbar and every secondary (per-display) taskbar.
  - **Self-healing refresh loop** re-applies the effect so it survives Explorer restarts and display changes.
  - **Tray toggle**: enable/disable taskbar transparency straight from the WallSafe tray icon.
  - **Panic-integrated**: optionally revert the taskbar to its default look while WallSafe is concealed, then restore it on the second (restore) press — kept in lock-step with the bimodal safe-wallpaper toggle.
  - Implemented natively in C# via the undocumented `SetWindowCompositionAttribute` + `ACCENT_POLICY` API — **no extra process and no separate install**; the taskbar is automatically restored to normal when WallSafe exits.

- **🎨 Windows 11 Fluent Light & Dark Themes**
  - Seamlessly switch between dark mode and clean Windows 11 light mode.
  - Dynamic theme-reactive brushes, vector icon geometry, and subtle drop shadows.

- **🛡️ Content Discretion & Tag Blacklisting**
  - Discretion Mode: blurs sensitive thumbnails until hovered.
  - Global tag blacklist to filter out unwanted tags and content.

---

## 🚀 Getting Started

### Download Pre-built Binary
1. Download the latest standalone **`WallSafe.exe`** from the [Releases](https://github.com/Kwan-desu/wallsafe/releases).
2. Run `WallSafe.exe` directly—no installation or external .NET runtime required!

### Building from Source

**Prerequisites**:
- Windows 10/11
- [.NET 10 SDK](https://dotnet.microsoft.com/)

```bash
# Clone repository
git clone https://github.com/Kwan-desu/wallsafe.git
cd wallsafe

# Build in Release mode
dotnet build WallSafe/WallSafe.csproj -c Release

# Publish self-contained single-file executable
dotnet publish WallSafe/WallSafe.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/WallSafe-Standalone-win-x64
```

---

## ⌨️ Default Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + Shift + W` | **Global Panic Hotkey** (Instantly switch to safe wallpaper + revert taskbar) |
| `Click Tray Icon` | Show / Hide WallSafe window |
| `Double Click Card` | Open High-Resolution Progressive Preview |
| `Tray → Taskbar Transparency` | Toggle the built-in taskbar transparency effect |

To configure the taskbar effect, open **Settings → Taskbar Appearance**, pick a state (Normal / Opaque / Clear / Blur / Acrylic), set a tint color, and enable it. Changes apply live.

---

## 🧩 About This Build (WallSafe + TranslucentTB Merge)

This build merges two originally-separate applications into one:

- **WallSafe** — the C# / .NET WPF anime wallpaper manager (the host app).
- **TranslucentTB** — a C++ utility that makes the Windows taskbar translucent.

Because the two projects use incompatible tech stacks (C# / .NET vs. C++ / WinRT), TranslucentTB was not bundled as a binary. Instead, its **core taskbar-appearance mechanism was reimplemented natively in C#** (`WallSafe/TaskbarManager.cs`) and wired directly into WallSafe's settings, system-tray menu, startup/exit lifecycle, and panic hotkey. The result is a single self-contained executable with no additional processes or installers.

The engine calls the same undocumented Windows API TranslucentTB uses — `user32!SetWindowCompositionAttribute` with a `WCA_ACCENT_POLICY` payload — against the taskbar windows (`Shell_TrayWnd` and every `Shell_SecondaryTrayWnd`).

### Building from Source

```bash
dotnet build WallSafe/WallSafe.csproj -c Release
```

Requires the **.NET 10 SDK** on Windows 10/11.

---

## 📄 License

WallSafe is distributed under the MIT License.

The taskbar-transparency engine is an independent reimplementation inspired by
[TranslucentTB](https://github.com/TranslucentTB/TranslucentTB) (GPLv3). Credit for the
original taskbar-transparency concept and API research goes to the TranslucentTB team.
