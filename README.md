# Aurora World Clock · 极光世界时钟

A **glassmorphic, always-on-top world clock widget for Windows 10/11**, written in C# / .NET 8 (WPF).
Real Windows acrylic blur, clipped to the widget silhouette, with Apple-style animations and a
tray-first control model.

> Circular and rounded-square widgets float above your desktop, blurring whatever is behind them.
> Each clock keeps its own city, time zone, size and position — and clicks can pass straight through.

---

## ✨ Features

| | |
|---|---|
| 🫧 **Real frosted glass** | Windows acrylic blur masked to a circle / rounded square, plus a tiled micro-grain, specular bloom and hairline rim. |
| ⭕ **Circle & Square** | Toggle the silhouette per clock with a squash‑and‑pop morph animation. |
| 🌍 **Unlimited world clocks** | Every widget has its own city, time zone, size, shape and screen position. |
| 🕐 **Analog + digital** | Smooth sweeping second hand, live digital readout, date and UTC offset. |
| 🖱️ **Pointer click‑through** | Let every click fall through to the app behind — perfect for a desktop overlay. |
| ⌨️ **Global hotkeys** | Show/hide, click‑through, always‑on‑top and settings — from any application. |
| 🧲 **Desktop-native feel** | Drag anywhere, edge snapping, wheel to resize, `Shift`+wheel opacity, arrow‑key nudging, lock position. |
| 🔔 **Tray control center** | The notification‑area icon stays reachable even when widgets are hidden or click‑through. |
| 🚀 **Start with Windows** | One toggle (registry `Run` key, no admin rights required). |
| 💾 **Portable settings** | Everything is stored in `%AppData%\AuroraClock\config.json`. |

---

## ⌨️ Hotkeys

| Shortcut | Action |
|---|---|
| `Ctrl` + `Alt` + `D` | Show / hide all clocks |
| `Ctrl` + `Alt` + `P` | Toggle pointer click-through |
| `Ctrl` + `Alt` + `T` | Toggle always-on-top |
| `Ctrl` + `Alt` + `S` | Open settings |

> If a shortcut is already taken by another app, Aurora Clock automatically falls back to the next
> available key (`H`, `V`, `O`, `U`, `Y`, …). The settings window always shows the keys that
> actually bound on your machine.

**Mouse shortcuts** — drag to move · wheel to resize · `Shift`+wheel for opacity · right-click for the
full menu · arrow keys to nudge by 1 px.

---

## 📦 Install

### A. Portable (no installer)

1. Download `AuroraClock-portable-win-x64.zip` from [Releases](../../releases) and unzip.
2. Run `AuroraClock.exe`.
   It's a self-contained build — **no .NET runtime required**.

### B. Setup.exe (Inno Setup)

Download and run `AuroraClock-Setup-x64.exe`. It creates Start-Menu/desktop shortcuts, registers an
uninstaller in *Apps & features*, and can enable autostart.

### C. Script installer (per-user, no admin)

From the unzipped folder:

```powershell
pwsh -File install.ps1 -Desktop -Startup -Run
```

Uninstall from *Apps & features*, or:

```powershell
pwsh -File uninstall.ps1
```

---

## 🛠 Build from source

Requirements: **.NET 8 SDK** (Windows).

```powershell
# debug run
dotnet run --project AuroraClock.csproj

# self-contained portable build (+ zip, + setup.exe if Inno Setup 6 is installed)
pwsh -File build.ps1
```

`build.ps1` regenerates `Assets/app.ico` from `tools/make-icon.ps1`, publishes a single-file
`win-x64` executable into `dist/app`, zips it, and compiles `installer/setup.iss` when
[Inno Setup](https://jrsoftware.org/isdl.php) is available.

### Project layout

```
AuroraClock/
├─ App.xaml(.cs)                 app bootstrap, theme, styles
├─ ClockWidget.xaml(.cs)         the widget window: glass layers, drag, morph, menu
├─ SettingsWindow.xaml(.cs)      clock list + global options
├─ CityPickerWindow.xaml(.cs)    searchable city / time-zone picker
├─ Controls/AnalogClock.cs       fully code-rendered analog face (ticks, hands, labels)
├─ Models/ClockItem.cs           per-clock model
├─ Services/
│   ├─ AppController.cs          central hub: config, widgets, hotkeys
│   ├─ AcrylicHelper.cs          SetWindowCompositionAttribute + shaped window region
│   ├─ WindowFx.cs               click-through / no-activate extended styles
│   ├─ HotkeyManager.cs          RegisterHotKey sink
│   ├─ AutostartService.cs       HKCU Run entry
│   ├─ TrayIcon.cs               dark tray menu
│   ├─ TimeZoneCatalog.cs        curated world city table
│   └─ ConfigService.cs          JSON persistence
├─ installer/                    install.ps1, uninstall.ps1, setup.iss
└─ tools/make-icon.ps1           multi-size .ico generator
```

---

## 🔧 Notes & constraints

- Windows acrylic fills the whole window rectangle, so the widget clips its native window region to
  the silhouette — that's what keeps the blur inside the circle.
- Acrylic requires desktop composition (Windows 10/11). On unsupported systems the widget falls back
  to its translucent tint layer.
- With click-through enabled the widget no longer receives mouse or keyboard input by design: use the
  tray icon or a hotkey to regain control.

## 📄 License

MIT — see [LICENSE](LICENSE).
