# WallSafe — Codebase Guide

## Projects
| Project | Stack |
|---|---|
| `WallSafeWeb/` | Desktop app. .NET 8 (`net8.0-windows10.0.19041.0`) WinForms host + WebView2; UI is vanilla HTML/CSS/JS ES modules, no build step |
| `wallsafe-android/` | Android app, Kotlin + Jetpack Compose, multi-module |
| `installer/WallSafeWeb.iss` | Inno Setup 6 installer |
| `IconGen/` | Small tool that builds `WallSafe.ico` |

## Desktop architecture
```
MainForm (WinForms, borderless + Mica)  ──hosts──▶  WebView2  https://app.wallsafe/index.html  (wwwroot/)
        │                                                   │
   RpcBridge  ◀── postMessage {id, method, params} ─────────┤  js/bridge.js  rpc() / on()
        │      ─── {id, result|error} / {event, data} ──────▶│
   Services/ (ApiClient, WallpaperManager, Favorites/Downloads/History, Settings, NativeHost, …)
   ImageServer: https://img.wallsafe/r?u=<url> (disk-cached) and /l?p=<local file>
```
- `App.cs`: entry point, single-instance mutex, tray icon + global hotkey (`NativeHost`), Wi-Fi / location / shutdown watchers. Closing the window hides it to the tray.
- `Bridge/RpcBridge.cs`: every RPC method and host→UI event. Settings writes are whitelisted by key.
- `Bridge/PostRegistry.cs`: maps `source:id` keys to `PostItem`s and builds the JSON DTOs.
- Data lives in `%LOCALAPPDATA%\WallSafe` (`settings.json`, `favorites.json`, …, `WebView2/`, `crash-web.log`).

### Frontend (`WallSafeWeb/wwwroot`)
- `js/app.js`: boot, hash router, title bar (rating, slideshow, blur, panic, window buttons), global keys.
- `js/pages/*.js`: each exports `create()` returning `{ el, show(params), refresh(reason, visible) }`. Pages are created once and cached. `broadcast(reason)` notifies them of changes (`rating`, `favorites`, `downloads`, `history`, `settings`, `discretion`, `manual`, …).
- `js/state.js`: shared state and post actions. `js/cards.js`: wallpaper cards. `js/preview.js`: viewer. `js/dnd.js`: drag & drop. `js/ui.js`: toast/menu/modal helpers. `js/icons.js`: SVG icons.
- `js/mock.js`: fake backend used automatically when `index.html` is opened outside the host, so the UI can be developed in a normal browser.

## Build & deploy (from WSL)
`dotnet` in WSL can't build Windows targets; use the `winbuild` bridge:
```bash
$HOME/.local/bin/winbuild WallSafeWeb/WallSafeWeb.csproj --configuration Release --target Publish
# output: WallSafeWeb/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/

# installer → dist/WallSafe-v<ver>-Setup.exe
powershell.exe -NoProfile -Command "& 'C:\Users\ASUS\AppData\Local\Programs\Inno Setup 6\ISCC.exe' 'C:\Users\ASUS\Documents\radom-ai-coded-shit\WallSafe\installer\WallSafeWeb.iss'"
```
The local install is `D:\WallSafe` (installed by the setup). Set `WALLSAFE_DEVTOOLS=1` before launching to enable DevTools.

UI-only work: `cd WallSafeWeb/wwwroot && python3 -m http.server 8765`, then open `http://127.0.0.1:8765/`.

## Releasing
1. Bump `<Version>` in `WallSafeWeb/WallSafeWeb.csproj` and `MyAppVersion` in `installer/WallSafeWeb.iss`. `UpdateService` reads the assembly version.
2. Publish, build the installer, and zip the publish folder as `WallSafe-v<ver>-win-x64-portable.zip`.
3. Tag `v<ver>` and create the GitHub release with both files. It **must be marked Latest**, because the desktop updater reads `/releases/latest` and downloads the asset ending in `-Setup.exe`. Android releases (`android-v*`) are found separately by their `.apk` asset.

## Android
Modules: `:core:model`, `:core:network`, `:core:database` (Room), `:core:datastore`, `:core:wallpaper`, `:core:panic`, `:feature:*` (Compose screens), `:widget`.
```bash
cd wallsafe-android && ./gradlew assembleDebug
```

## Rules for changes
- Never hardcode tokens or secrets. The updater works unauthenticated; `WALLSAFE_GITHUB_TOKEN` is optional.
- New setting: add a property to `Services/Settings.cs`, whitelist the key in `RpcBridge` `settings.set`, then bind it in `js/pages/settings.js`.
- New page: add `js/pages/<name>.js`, register it in `PAGES` in `js/app.js`, and add a sidebar link in `index.html`.
- Marshal service callbacks to the UI thread via `UiDispatch.Post` before touching WinForms or WebView2.
