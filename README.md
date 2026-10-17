# 🛡️ WallSafe

> Windows anime wallpaper manager with an instant panic hotkey — plus an Android companion app.

WallSafe lets you browse, download and set high-resolution wallpapers from booru boards, and keeps you safe with a global hotkey that instantly switches your desktop to a safe wallpaper.

Version 4.0 rebuilt the desktop app with a modern HTML/CSS/JS interface hosted in WebView2.

## ✨ Features
- **Explore** Konachan (SFW), Konachan.com, yande.re and custom Moebooru boards, with tag autocomplete (Ctrl + K), sorting, resolution and desktop-fit filters
- **SFW / 16+ / 18+** rating switch in the title bar; discretion blur hides sensitive thumbnails until hovered
- **Panic hotkey** (default `Ctrl + Shift + W`, works in full-screen games): safe wallpaper, hide app, minimize windows, mute — press again to restore
- **Auto-safe triggers**: switch to the safe wallpaper on chosen Wi-Fi networks, near a location, or before sleep/shutdown
- **Favorites & collections** with drag & drop, **downloads**, **history** with one-click re-apply
- **Slideshow** from favorites, a collection or downloads; per-monitor targeting
- Windows 11 Mica/Acrylic window, light/dark/system theme, accent colors, tray mode, start with Windows, in-app updates

## 📦 Download
Get the latest from **[Releases](https://github.com/Kwan-desu/wallsafe/releases/latest)**:

| File | Use |
|---|---|
| `WallSafe-v4.x.x-Setup.exe` | Installer — also upgrades existing 3.x installs and keeps your data |
| `WallSafe-v4.x.x-win-x64-portable.zip` | Portable — extract and run `WallSafe.exe` |

Requires Windows 10 1809+ and the Microsoft Edge WebView2 Runtime (built into Windows 11).

## ⌨️ Shortcuts
| Shortcut | Action |
|---|---|
| `Ctrl + Shift + W` | Panic (customizable in Settings) |
| `Ctrl + K` | Search |
| `Ctrl + 1…6` | Home / Explore / Favorites / Downloads / History / Settings |
| `F5` | Refresh |
| `← →` `Esc` `I` | In the viewer: previous/next, close, toggle details |

## 🛠️ Repository
| Folder | Contents |
|---|---|
| `WallSafeWeb/` | Desktop app (.NET 8 WinForms + WebView2, UI in `wwwroot/`) — see [`WallSafeWeb/README.md`](WallSafeWeb/README.md) |
| `wallsafe-android/` | Android app (Kotlin, Jetpack Compose) |
| `installer/` | Inno Setup script (`WallSafeWeb.iss`) |
| `IconGen/` | Icon generation tool |

Architecture notes and dev workflow: [`CODEBASE_README.md`](CODEBASE_README.md). Versions up to 3.1.8 (WinUI 3) and 2.x (WPF) are in the git history and on the Releases page.

## 📄 License
MIT
