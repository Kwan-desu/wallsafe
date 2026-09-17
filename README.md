# 🛡️ WallSafe

> **Next-Generation Windows 11 Desktop Anime Wallpaper Manager with Instant Panic Hotkey Protection (WinUI 3 & Windows App SDK).**

WallSafe is a sleek, modern desktop application crafted in **WinUI 3 / Windows App SDK** and **.NET 8** that lets you explore, download, and set high-resolution anime wallpapers from top booru boards—all while keeping your privacy safe with a hardware-level instant panic concealment system.

Designed natively for Windows 11, WallSafe features a modern **Windows App** dashboard layout with horizontal carousels, responsive statistics, smooth pagination controls, Mica/Acrylic backdrops, and instant emergency protection.

---

## ✨ Key Features

### 🎨 Windows App Dashboard & Fluent Design (New in v3.0.0)
- **Windows App Layout**: Restructured around tidy horizontal scrolling shelves (Trending wallpapers, Popular Universes & Franchises, and Your Favorites).
- **Interactive Carousel Controls**: Smooth horizontal navigation with left/right chevrons (`<` and `>`) and centered live **`PipsPager`** dot indicators (`● ○`).
- **Mouse Wheel Horizontal Scrolling**: Scroll naturally over any carousel with your mouse wheel—no need to hold `Shift`.
- **Responsive Statistics Strip**: 5-column full-width overview cards displaying Favorites, Downloads, Applied wallpapers, Top pick franchise, and **Panic Triggers count**.
- **Dashboard Section Customization**: Toggle individual sections on/off under *Settings → Home dashboard* (including the overview statistics strip).
- **Mica & Acrylic Material Surfaces**: Deep Windows 11 integration with Mica/Acrylic window backdrops and Fluent theme adaptation (Light & Dark modes).

### 🌐 Multi-Source Booru Aggregation & Filter Memory
- Stream thousands of wallpapers from **konachan.net** (SFW), **konachan.com**, and **yande.re**, plus "All Sources" multi-engine aggregation.
- **3-Mode Segmented Rating Selector**: Instant switching between **SFW**, **Questionable**, and **Explicit** catalogs directly in the title bar.
- Add and manage custom Danbooru / Moebooru boards with SFW classification.
- Remembers your customized filter choices independently for each rating mode.

### ⚡ Instant In-Card Actions
- Clean action bar on every wallpaper card:
  - 👁️ **View / Double-click**: Instant progressive 1080p/4K inspection preview modal.
  - ❤️ **Favorite**: Instantly save to your local offline favorites collection.
  - ⚡ **Apply Wallpaper**: Set desktop wallpaper immediately with one click.
  - 📥 **Download**: Download original high-resolution art to your pictures folder.
  - ↗️ **Source**: Open the original booru post in your browser.

### ✨ Source-Aware Universe & Series Discovery
- **Live Board Ranking**: Popular franchises automatically rank based on real-time booru popularity and post counts on the currently active image board.
- **Live Artwork Counts & Badges**: Each card displays live post counts (e.g. `47.6k Artworks`), genre badges (*Anime*, *Gaming*, *Custom*), and representative backdrop art.
- **Clipped Rounded Banners**: 16:9 banner cards (`CornerRadius="12"`) with dark gradient title overlays and subtle hover elevation.

### 🚨 Global Panic Hotkey & Stealth Suite
- **Instant Panic Switch**: Press the global shortcut (default `Ctrl + Shift + W`, fully customizable) anywhere—even inside full-screen games.
- **Bimodal Restore**: First press switches desktop to default Windows 11 safe wallpaper and conceals WallSafe; second press restores your previous anime wallpaper.
- **Panic Trigger Tracker**: Tracks how many times emergency safe wallpaper has been invoked, displayed directly on your Home dashboard.
- **Concealment Actions**: Optional auto-minimize all windows (`Win+D` simulation) and master desktop mute.

### 🔄 Automated Slideshow Engine
- Automatically cycle through your favorite or downloaded wallpapers.
- Configurable rotation intervals (seconds, minutes, hours) with multi-monitor targeting support.

### 🚀 Windows Startup & Background Tray Mode
- Option to auto-launch WallSafe on Windows boot via registry integration.
- **Start in Background / System Tray**: Silently loads into the system tray and aggressively compacts process memory (`AppSuspensionManager`) without popping up windows.

---

## 📦 Installation & Downloads

Download the latest version from the **[Releases](https://github.com/Kwan-desu/wallsafe/releases)** page:

| Package | Description | Recommended For |
| :--- | :--- | :--- |
| **`WallSafe-v3.0.0-Setup.exe`** | **Standalone Windows Installer**: Installs to `%LOCALAPPDATA%\Programs\WallSafe`, creates Desktop & Start Menu shortcuts, and integrates with Windows Settings Apps. | Most users (one-click setup) |
| **`WallSafe-v3.0.0-win-x64-portable.zip`** | **Self-Contained Portable Archive**: Extract anywhere (or on a USB flash drive) and run `WallSafeWinUI.exe` directly without installation. | Portable / USB drive users |

---

## ⌨️ Default Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + Shift + W` | **Global Panic Hotkey** (Instantly switch to safe wallpaper and conceal WallSafe) |
| `Click Tray Icon` | Show / Hide WallSafe window |
| `Double Click Card` | Open High-Resolution Progressive Preview Dialog |

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 (version 1809+ / build 17763+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/)
- Visual Studio 2022 / 2026 or Windows App SDK Build Tools

### Building WallSafe (WinUI 3)
```powershell
# Clone repository
git clone https://github.com/Kwan-desu/wallsafe.git
cd wallsafe

# Build unpackaged self-contained Release
dotnet build WallSafeWinUI/WallSafeWinUI.csproj -c Release -p:Platform=x64

# Publish portable folder
dotnet publish WallSafeWinUI/WallSafeWinUI.csproj -c Release -r win-x64 -p:Platform=x64
```

### Building the Setup Installer
```powershell
# Build self-contained single-file installer
dotnet publish installer/WallSafeInstaller.csproj -c Release -r win-x64
```

---

## 📄 License

WallSafe is distributed under the MIT License.
