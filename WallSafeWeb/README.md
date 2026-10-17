# WallSafe — WebView2 edition

A WinForms + WebView2 host with a plain HTML/CSS/JS UI (no framework, no build step). Replaced the WinUI 3 app (≤ 3.1.8, still in git history).

## Build & run
```bash
# from repo root (WSL)
$HOME/.local/bin/winbuild WallSafeWeb/WallSafeWeb.csproj --configuration Release --target Publish
cmd.exe /c "taskkill /IM WallSafe.exe /F"
powershell.exe -NoProfile -Command "Copy-Item 'C:\Users\ASUS\Documents\radom-ai-coded-shit\WallSafe\WallSafeWeb\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*' 'D:\WallSafe\' -Recurse -Force; Start-Process 'D:\WallSafe\WallSafe.exe'"
```
- DevTools: set `WALLSAFE_DEVTOOLS=1` before launching.
- UI only: serve `wwwroot/` over HTTP (e.g. `python3 -m http.server`). Outside the host, `js/bridge.js` falls back to the fake backend in `js/mock.js`.
- Crash log: `%LOCALAPPDATA%\WallSafe\crash-web.log`.
- Optional: `WALLSAFE_GITHUB_TOKEN` raises the GitHub API rate limit for update checks.

## Layout
| Path | Role |
|---|---|
| `App.cs` | Entry point, single instance, tray, panic hotkey, Wi-Fi/location/shutdown watchers |
| `MainForm.cs` | Borderless window (Mica/Acrylic, rounded corners), WebView2 setup, `https://app.wallsafe` → `wwwroot` |
| `Bridge/RpcBridge.cs` | JSON RPC (`{id, method, params}`) plus pushed events |
| `Bridge/ImageServer.cs` | `https://img.wallsafe/r?u=` (cached remote image) and `/l?p=` (local file) |
| `Bridge/PostRegistry.cs` | Post keys and the DTOs sent to JS |
| `Services/` | Booru API, wallpaper, favorites/downloads/history, settings, tray & hotkey services |
| `wwwroot/js/app.js` | Shell, router, title-bar controls |
| `wwwroot/js/dnd.js` | Drag cards onto collection tiles, sidebar Favorites/Downloads, or the floating drop dock |
| `wwwroot/js/pages/*` | Home, Explore, Favorites, Downloads, History, Settings |

Each page module exports `create()`, which returns `{ el, show(params), refresh(reason, visible) }`.

