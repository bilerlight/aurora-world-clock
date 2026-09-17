# Aurora World Clock · 极光世界时钟

A **cel-shaded (赛璐璐), highly transparent desktop box for Windows 10/11**, written in C# / .NET 8
(WPF). One right-hand dock holds your world clocks, quick launchers, CC Switch usage, todos and
sticky notes — with flat ink outlines, clean highlights and a real frosted backdrop.

> The dock sticks to **any of the four screen edges**, can **auto-hide after a delay you choose** and
> **pops back when the mouse bumps the edge**. Launcher tiles start programs, folders, or a
> `cmd` / PowerShell / Windows Terminal session already sitting in the folder you pick — built for
> kicking off AI coding tools in the right project.

---

## ✨ Features

### The dock box 右侧盒子

| | |
|---|---|
| 🎨 **Cel-shaded & highly transparent** | Flat fills, 2 px ink outlines, hard "sticker" shadows. Panel transparency, corner radius, outline and shadow are all yours to tune, with six ready palettes (雪青 / 薄荷 / 奶油 / 樱粉 / 天蓝 / 墨夜) and eight accent colours. |
| 📐 **Docks to any edge** | Right · Left · Top · Bottom — drag the header and it snaps to the nearest one. Top / bottom docking re-flows the cards sideways. |
| 🫥 **Auto-hide + edge bump** | Wait N seconds (0.5 – 15 s) after the pointer leaves and the box slides away **completely** — the bump watcher brings it back when the mouse hits the screen edge, so no sliver has to stay visible (or leave 1 – 24 px on screen if you prefer a visible tab). It never hides while your pointer is on it or you are typing in it. |
| ↔️ **Resizable** | Drag the inner edge to change the box's thickness; drag the header along the edge to slide it. |
| 🧩 **Section layout** | Clocks, launchers, usage, todos and notes — each can be shown or hidden and reordered. |
| 📌 **Collapse** | Shrink it to a small tab when you want the screen back. |

### Quick launcher 快速启动

| | |
|---|---|
| 🚀 **Programs & shortcuts** | Any `.exe`, `.lnk`, `.bat`, `.cmd`, `.ps1` — icons are pulled from the real shell, so tiles look like your desktop. |
| 💻 **Command + working folder** | Give it a command and a folder: it opens `cmd` (or PowerShell 7 / Windows Terminal) already in that directory and runs the line. Perfect for `claude`, `codex`, `opencode`, `gemini`, `aider`… |
| 🧠 **AI presets** | One click fills in a ready-made entry for Claude Code, Codex CLI, OpenCode, Gemini CLI, Aider, `npm run dev`, `git pull && git status`. |
| ⚙️ **Per-tile options** | Arguments, icon override, accent colour, keep-the-window-open, run as administrator, reorder, edit, duplicate. |

### Clocks & desktop

| | |
|---|---|
| 🫧 **Real frosted glass** | The desktop behind the box is captured and blurred in-process — desaturated, lifted, grained. |
| 🌍 **Unlimited world clocks** | Every clock has its own city and time zone; the dock shows a hero digital readout plus a small analog dial each. |
| ⏱️ **Analog + digital** | Smooth sweeping second hand, seconds bar, date and UTC offset, 大周 / 小周 label. |
| 🪟 **Optional floating widgets** | The classic free-floating circle / rounded-square widgets are still there — off by default, one toggle away (tray → *浮动时钟组件*). Circular dials, square digital cards, per-clock size, shape and custom dial images. |
| 🖱️ **Pointer click-through** | Let every click fall through to the app behind — for the floating widgets. |
| ⌨️ **Global hotkeys** | Show/hide, box, click-through, always-on-top and settings — from any application. |
| 🔔 **Tray control center** | Always reachable, even when everything is hidden or click-through. |
| 🚀 **Start with Windows** | One toggle (registry `Run` key, no admin rights). |
| 💾 **Portable settings** | Everything lives in `%AppData%\AuroraClock\config.json`. |

### Productivity extras

| | |
|---|---|
| 💥 **Comic-book reminders** | Schedule a reminder and a hand-drawn "POW!" card pops up — halftone paper, thick ink outline, starburst badge. Click anywhere to dismiss. |
| 📅 **Smart repeat rules** | Once · every day · **workdays only** · **大小周 (alternating 5/6-day weeks)** · every N minutes. |
| 📝 **Sticky notes** | Drop a note on the desktop; it shows a live countdown and removes itself when the time is up (5 min → permanent). |
| ☑️ **Todo list** | Quick checklist in the box, completed items struck through. |
| 📊 **CC Switch usage** | Live cost, request count, token split and per-app bars read from `~/.cc-switch/cc-switch.db`, refreshed every 10 s. |
| 🎛 **Aurora Center** | One window that merges todos, reminders, notes and usage (tray → *Aurora 中心*). |

---

## ⌨️ Hotkeys

| Shortcut | Action |
|---|---|
| `Ctrl` + `Alt` + `B` | Show / hide the dock box |
| `Ctrl` + `Alt` + `D` | Show / hide everything |
| `Ctrl` + `Alt` + `P` | Toggle pointer click-through |
| `Ctrl` + `Alt` + `T` | Toggle always-on-top |
| `Ctrl` + `Alt` + `S` | Open settings |

> If a shortcut is already taken by another app, Aurora Clock automatically falls back to the next
> available key (`H`, `V`, `O`, `U`, `Y`, `J`, `N`, …). The settings window always shows the keys
> that actually bound on your machine.

**Mouse shortcuts** — drag the header to move / re-dock · drag the inner edge to resize · right-click
for the full menu (edge, hide delay, bump reveal, collapse).


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
├─ App.xaml(.cs)                 app bootstrap + the cel-shaded control styles
├─ DockBoxWindow.xaml(.cs)       the right-hand global box (clocks, launchers, usage, todos, notes)
├─ LauncherEditWindow.xaml(.cs)  launcher editor: program / folder / cmd + working dir + AI presets
├─ ClockWidget.xaml(.cs)         optional floating widget: glass layers, drag, morph, menu
├─ ReminderPopup.xaml(.cs)       comic-book reminder card
├─ NoteWindow.xaml(.cs)          desktop sticky note with countdown
├─ UsageWidget.xaml(.cs)         CC Switch usage widget (standalone)
├─ DashboardWindow.xaml(.cs)     Aurora Center: todos / reminders / notes / usage
├─ SettingsWindow.xaml(.cs)      theme, dock, clocks and global options
├─ CityPickerWindow.xaml(.cs)    searchable city / time-zone picker
├─ Controls/AnalogClock.cs       fully code-rendered analog face (ticks, hands, labels)
├─ Models/                       ClockItem, Reminder, NoteItem, TodoItem, LauncherItem, ThemePalette
├─ Services/
│   ├─ AppController.cs          central hub: config, dock, widgets, hotkeys
│   ├─ ThemeService.cs           the cel palette -> application resources
│   ├─ LaunchService.cs          starts apps, folders and terminal commands
│   ├─ BackdropService.cs        capture + blur behind a panel (true shaped frost)
│   ├─ ReminderService.cs        reminder scheduler (day rules, intervals)
│   ├─ CcSwitchUsage.cs          live usage reader for cc-switch.db
│   ├─ IconFactory.cs            app icon + real shell icon extraction for tiles
│   ├─ WindowFx.cs               click-through / no-activate extended styles
│   ├─ HotkeyManager.cs          RegisterHotKey sink
│   ├─ AutostartService.cs       HKCU Run entry
│   ├─ TrayIcon.cs               tray menu
│   ├─ TimeZoneCatalog.cs        curated world city table
│   └─ ConfigService.cs          JSON persistence
├─ installer/                    install.ps1, uninstall.ps1, setup.iss
└─ tools/make-icon.ps1           multi-size .ico generator
```

> The usage card reads `~/.cc-switch/cc-switch.db` read-only via `Microsoft.Data.Sqlite`
> (the only NuGet dependency). If CC Switch is absent the card reports that it is not running.

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
