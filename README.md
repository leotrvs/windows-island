# Windows Island

**English** | [Português (Brasil)](README.pt-BR.md) | [Español](README.es.md) | [Русский](README.ru.md)

A Dynamic Island for Windows. Music, Claude Code, its usage limits and your notifications live in a small pill at the top of your screen, with iPhone-style looks and spring animations.

<p align="center">
<img src="docs/images/demo-en.gif" alt="Windows Island animated: collapsed, expanding, music, Claude, timer and an alert" width="575">
</p>

<p align="center">
<a href="https://github.com/leotrvs/windows-island/releases/latest/download/WindowsIsland.exe"><img alt="Download for Windows" src="https://img.shields.io/badge/Download_for_Windows-0A84FF?style=for-the-badge&logo=windows&logoColor=white"></a>
</p>

> Unofficial community project. It is not affiliated with, endorsed by, or supported by Apple, Anthropic or OpenAI. "Claude" and the Claude logo belong to Anthropic, and "Codex" to OpenAI; the MIT license does not cover them.

## Features

- **Music** from any player in the Windows media controls, such as Spotify or a browser: artwork, an equalizer tinted by the artwork, a progress bar you can click to seek, and play, pause, next and previous
- **Claude Code live activity:** the Claude spark thinks while Claude works, with the time so far, and breathes when Claude is waiting for you
- **Claude usage:** rings for the **5-hour** and **weekly** limits, with reset times
- **Codex:** a live activity while Codex works, its 5-hour and weekly limits, and an alert when it finishes
- **Pages:** when the island is expanded, scroll over it or click the dots to switch between music, Claude and the timer, which also has its own buttons there
- **Timer and stopwatch:** start one from the tray menu; it shows as a live activity and alerts you when the time is up
- **Microphone and camera dots:** orange while the microphone is in use, green for the camera, like on iPhone
- **Battery** on laptops: shown as an activity while charging, plus alerts for charger connected or removed, low battery and fully charged
- **App names on the dots:** see which app is using the microphone or camera
- **Volume and brightness** bar, **Bluetooth, USB and network** notes, and a moon for **Do not disturb**
- **Clock and system page** with the time, date, processor and memory load
- **System monitor** (off by default): keeps the processor and memory load on the island when nothing else is happening, as rings, bars or numbers
- **Tuck away:** drag the island up to put it away and use what is behind it; it comes back with new activity or when the mouse rests on its spot
- **Local API and command line** (off by default) so your scripts and apps can show their own activities and alerts, see [docs/API.md](docs/API.md), plus a small [browser extension](extension/README.md) that gives web audio the site's icon
- **Keyboard shortcut** to open and close the island (off by default, configurable)
- **Alerts** when Claude finishes a task or needs you
- **Windows notifications** in the island, with the sending app's icon, if you turn them on
- **Two activities at once:** the second one splits off into a bubble beside the island
- **Fluid motion:** shape and content morph with springs, never with fixed-time fades
- **Discreet:** it never takes focus, clicks around it reach the window below, it shrinks to a thin line when a maximized window is in front and it hides in fullscreen
- **Light:** about 40 MB of memory and under 1% CPU while music plays
- Installs for your user only, with Start menu and Desktop shortcuts and start with Windows
- English and Brazilian Portuguese
- Signed automatic updates
- **Never reads your Claude login or token**. Its only connection is checking this repository for updates

## States

| State | Look |
|---|---|
| Idle | <img src="docs/images/idle-en.png" alt="Idle pill" width="260"> |
| Music playing, with Claude working beside it | <img src="docs/images/musiccompact-en.png" alt="Compact music with the Claude bubble" width="380"> |
| Claude working, with the time so far | <img src="docs/images/claudecompact-en.png" alt="Compact Claude activity" width="320"> |
| Codex working | <img src="docs/images/codexcompact-en.png" alt="Compact Codex activity" width="320"> |
| Claude finished or needs you | <img src="docs/images/banner-en.png" alt="Claude alert" width="420"> |
| A Windows notification | <img src="docs/images/notification-en.png" alt="Notification banner" width="420"> |
| Battery charging | <img src="docs/images/battery-charging-en.png" alt="Charging alert" width="420"> |
| Battery low | <img src="docs/images/battery-low-en.png" alt="Low battery alert" width="420"> |
| Claude expanded, with the limits | <img src="docs/images/claudeexpanded-en.png" alt="Claude expanded with usage rings" width="460"> |

## Install

1. **Download** `WindowsIsland.exe` from the [latest release](https://github.com/leotrvs/windows-island/releases/latest). Windows may show a SmartScreen warning because the app has no paid code-signing certificate: choose **More info**, then **Run anyway**.
2. **Open it** and choose **Yes**. It installs for your Windows user only, with no administrator rights, adds Start menu and Desktop shortcuts and starts with Windows.
3. The island appears at the top of the screen, and its icon in the tray.

To try it without installing, run `WindowsIsland.exe --portable`.

## Use

1. **Hover** over the island to expand it, or **click** it. Move the mouse away and it collapses.
2. **Click** the progress bar to jump to that point of the song.
3. **Click** an alert or notification to dismiss it.
4. **Click** the tray icon, or open the Windows Island shortcut while it runs, to open **Settings**. Right-click the icon for quick actions and **Quit**.

<img src="docs/images/settings-en.png" alt="The settings window" width="320">

| Option | What it does |
|---|---|
| Show music | Shows what is playing |
| Show Claude | Shows Claude Code activity and limits |
| Claude alerts | Expands the island when Claude finishes or needs you |
| Show Codex, Codex alerts | Codex activity and limits, and the alert when it finishes. Only appear while Codex is working |
| Microphone and camera dots | Shows a small dot beside the island while an app uses the microphone (orange) or the camera (green) |
| Battery alerts | Charger connected or removed, low battery and fully charged. Laptops only |
| Show Windows notifications | Shows new notifications in the island. Off by default |
| Alert size | Small or normal alerts and notifications |
| Screenshot alert | A very small note when a screenshot is taken with Print Screen or Win+Shift+S |
| Usage ring color | Blue, green or purple. The rings turn amber and red near the limit |
| Language | Follows Windows by default. English, Portuguese, Spanish and Russian |
| Empty island | Keeps the collapsed island a plain pill, with no live activities. Hover or click to see them |
| Show pill when idle | Keeps the small pill on screen when nothing is happening |
| Expand on hover | Expands when the mouse rests on the island. When off, click it |
| Hide in fullscreen | Hides the island during games, videos and presentations |
| Position | Top of the screen, or just above the taskbar |
| Monitor | The primary monitor, a specific one, all of them, or the one with the mouse |
| Collapsed size, expanded size | 80% to 130%, set apart for each state |
| Distance from the edge, horizontal position | Moves the island down from the edge or to the side |
| Auto-hide when idle | Hides the island after it has shown nothing for a while. It comes back with new activity, or when you move the mouse to its spot |
| Alert duration | How long Claude alerts and notifications stay on screen |
| Hide paused music after | How long a paused song stays on the island |
| Start with Windows | Opens the app when you sign in to Windows |
| Open settings file | Opens `settings.json`, where `OffsetY` sets the distance from the edge |

## How the data is read

- **Claude activity:** Claude Code keeps one small file per running session in `~/.claude/sessions`. The island reads only its kind, process ID, status, name and status time. It works with the terminal and with the desktop app.
- **Claude limits:** every 2 minutes, and when you open the Claude panel, the island runs `claude -p /usage` in the background, the same numbers you see in Claude Code. It makes no model call and spends no tokens. If [Claude Usage Taskbar](https://github.com/leotrvs/claude-usage-taskbar) is installed and has fresher numbers, the island uses those.
- **Codex:** Codex keeps each session as a file in `~/.codex/sessions`. Every 2 seconds the island reads the end of the newest one and takes only whether the last task started or finished and the limit percentages Codex recorded. They update whenever you use Codex. It never reads Codex's login.
- **Notifications:** the official Windows API for reading notifications needs a signed package, so the island reads, read-only, the local database where Windows keeps them. It shows only notifications that arrive while it runs and stores nothing. Windows still shows its own banner too: turn off **Show notification banners** for an app in Windows Settings if you only want the island.

Details are in [SECURITY.md](SECURITY.md).

## Updates

Every hour the island checks the latest GitHub release. It installs an update only if its digital signature matches the key built into the app and the file's hash matches the signed list, so a tampered file is rejected. It restarts into the new version when you are not using the island and shows a short notice. Turn this off with **Automatic updates** in the tray menu, where **Check for updates** runs a check on demand.

## Uninstall

Go to **Windows Settings > Apps > Installed apps > Windows Island > Uninstall**. This removes the app, the shortcuts and the settings.

## Build from source

You need Windows 10 version 2004 or later, or Windows 11, and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The exe it builds runs without .NET installed.

```powershell
git clone https://github.com/leotrvs/windows-island.git
cd windows-island
pwsh -File scripts/build.ps1
.\bin\WindowsIsland.exe
```

Contributors should read [CONTRIBUTING.md](CONTRIBUTING.md).

## Roadmap

The full list of what is planned is in [docs/ROADMAP.md](docs/ROADMAP.md).

## License

[MIT](LICENSE). The Claude name and logo are not covered.

The Inter font is bundled under the SIL Open Font License, see [src/Assets/fonts/NOTICE.md](src/Assets/fonts/NOTICE.md).
