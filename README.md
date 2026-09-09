# 🛡️ WallSafe

> **Next-Generation Windows Desktop Anime Wallpaper Manager with Instant Panic Hotkey Protection**

WallSafe is a sleek, modern Windows 11 desktop application crafted in WPF / .NET that lets you explore, download, and set high-resolution anime wallpapers from top booru boards—all while keeping your privacy safe with a hardware-level instant panic concealment system.

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
| `Ctrl + Shift + W` | **Global Panic Hotkey** (Instantly switch to safe wallpaper) |
| `Click Tray Icon` | Show / Hide WallSafe window |
| `Double Click Card` | Open High-Resolution Progressive Preview |

---

## 📄 License

Distributed under the MIT License.
