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
| 🫧 **Real frosted glass** | The desktop behind each widget is captured and blurred in-process, then clipped to the exact silhouette — a true circular (or rounded-square) frosted pane, with desaturation, grain, specular sheen and a lit bezel. |
| ⭕ **Circle & Square** | Circle = analog dial with a deep bezel; Square = full-width digital card whose time auto-fits the card. Switch with a squash-and-pop morph. |
| 🌍 **Unlimited world clocks** | Every widget has its own city, time zone, size, shape and screen position. |
| 🏙️ **Per-clock city / time zone** | Change any clock's city or time zone on the spot — right-click the widget → *Change city / time zone*, or use the button in the settings list. |
| 🖼️ **Custom dial image** | Use any image (PNG / JPG / BMP / GIF / WEBP) as a dial face; bezel, ticks, hands and text stay on top. Clear it any time. |
| 🕐 **Analog + digital** | Smooth sweeping second hand, live digital readout, date and UTC offset. |
| 🖱️ **Pointer click‑through** | Let every click fall through to the app behind — perfect for a desktop overlay. |
| ⌨️ **Global hotkeys** | Show/hide, click‑through, always‑on‑top and settings — from any application. |
| 🧲 **Desktop-native feel** | Drag anywhere, edge snapping, wheel to resize, `Shift`+wheel opacity, arrow‑key nudging, lock position. |
| 🔔 **Tray control center** | The notification‑area icon stays reachable even when widgets are hidden or click‑through. |
| 🚀 **Start with Windows** | One toggle (registry `Run` key, no admin rights required). |
| 💾 **Portable settings** | Everything is stored in `%AppData%\AuroraClock\config.json`. |

### Productivity extras

| | |
|---|---|
| 💥 **Comic-book reminders** | Schedule a reminder and a hand-drawn "POW!" card pops up — halftone paper, thick ink outline, starburst badge. Click anywhere to dismiss. |
| 📅 **Smart repeat rules** | Once · every day · **workdays only** · **大小周 (alternating 5/6-day weeks)** · every N minutes. |
| 📝 **Sticky notes** | Drop a note on the desktop; it shows a live countdown and removes itself when the time is up (5 min → permanent). |
| ☑️ **Todo list** | Quick checklist, completed items struck through. |
| 📊 **CC Switch usage** | A small glass widget mirrors the usage CC Switch records (`~/.cc-switch/cc-switch.db`): today's cost, tokens and per-app breakdown, refreshed every 10 s. It hides itself whenever CC Switch is not running. |
| 🎛 **Aurora Center** | One window that merges todos, reminders, notes and usage (tray → *Aurora 中心*). |

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
├─ ClockWidget.xaml(.cs)         the clock widget: glass layers, drag, morph, menu
├─ ReminderPopup.xaml(.cs)       comic-book reminder card
├─ NoteWindow.xaml(.cs)          desktop sticky note with countdown
├─ UsageWidget.xaml(.cs)         CC Switch usage widget
├─ DashboardWindow.xaml(.cs)     Aurora Center: todos / reminders / notes / usage
├─ SettingsWindow.xaml(.cs)      clock list + global options
├─ CityPickerWindow.xaml(.cs)    searchable city / time-zone picker
├─ Controls/AnalogClock.cs       fully code-rendered analog face (ticks, hands, labels)
├─ Models/                       ClockItem, Reminder, NoteItem, TodoItem
├─ Services/
│   ├─ AppController.cs          central hub: config, widgets, hotkeys
│   ├─ BackdropService.cs        capture + blur behind a widget (true shaped frost)
│   ├─ ReminderService.cs        reminder scheduler (day rules, intervals)
│   ├─ CcSwitchUsage.cs          live usage reader for cc-switch.db
│   ├─ AcrylicHelper.cs          glass tint for the dialog windows
│   ├─ WindowFx.cs               click-through / no-activate extended styles
│   ├─ HotkeyManager.cs          RegisterHotKey sink
│   ├─ AutostartService.cs       HKCU Run entry
│   ├─ TrayIcon.cs               dark tray menu
│   ├─ TimeZoneCatalog.cs        curated world city table
│   └─ ConfigService.cs          JSON persistence
├─ installer/                    install.ps1, uninstall.ps1, setup.iss
└─ tools/make-icon.ps1           multi-size .ico generator
```

> The CC Switch usage widget reads `~/.cc-switch/cc-switch.db` read-only via
> `Microsoft.Data.Sqlite` (the only NuGet dependency). If CC Switch is absent the widget simply
> stays hidden.

---

## 🔧 Notes & constraints

- **Why the glass is self-rendered:** Windows' built-in acrylic backdrop always fills the whole
  window rectangle and ignores window regions (`SetWindowRgn`) and `DwmEnableBlurBehindWindow`'s blur
  region, so a circular widget would always show a square of blur behind it. Aurora Clock therefore
  captures the screen area behind the widget and blurs it itself (`BackdropService`), which is what
  lets the frost follow a circle exactly — and lets the blur be stronger than the system default.
- The widget never captures itself (it is marked `WDA_EXCLUDEFROMCAPTURE`). Untick
  *Show in screenshots* in Settings if you want it to appear in your own screenshots/recordings
  too — the blur then has a faint self-capture halo.
- Turning *Live frosted backdrop* off falls back to a flat translucent tint (useful on very slow
  machines).
- With click-through enabled the widget no longer receives mouse or keyboard input by design: use the
  tray icon or a hotkey to regain control.

## 📄 License

MIT — see [LICENSE](LICENSE).
