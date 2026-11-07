# Windows Island

**English** | [Português (Brasil)](README.pt-BR.md) | [Español](README.es.md) | [Русский](README.ru.md)

A Dynamic Island for Windows. Music, Claude Code, its usage limits and your notifications live in a small pill at the top of your screen, with iPhone-style looks and spring animations.

<p align="center">
<img src="docs/images/musicexpanded-en.png" alt="The island expanded with the song playing" width="460">
</p>

> Unofficial community project. It is not affiliated with, endorsed by, or supported by Apple or Anthropic. "Claude" and the Claude logo belong to Anthropic; the MIT license does not cover them.

> **Early preview.** There are no published releases yet. [Build it from source](#build-from-source) to try it.

## Features

- **Music** from any player in the Windows media controls, such as Spotify or a browser: artwork, an equalizer tinted by the artwork, a progress bar you can click to seek, and play, pause, next and previous
- **Claude Code live activity:** the Claude spark thinks while Claude works, with the time so far, and breathes when Claude is waiting for you
- **Claude usage:** rings for the **5-hour** and **weekly** limits, with reset times
- **Alerts** when Claude finishes a task or needs you
- **Windows notifications** in the island, if you turn them on
- **Two activities at once:** the second one splits off into a bubble beside the island
- **Fluid motion:** shape and content morph with springs, never with fixed-time fades
- **Discreet:** it never takes focus, clicks around it reach the window below, it shrinks to a thin line when a maximized window is in front and it hides in fullscreen
- **Light:** about 40 MB of memory and under 1% CPU while music plays
- Installs for your user only, with Start menu and Desktop shortcuts and start with Windows
- English and Brazilian Portuguese
- **Never reads your Claude login or token**, and makes no network connections

## States

| State | Look |
|---|---|
| Idle | <img src="docs/images/idle-en.png" alt="Idle pill" width="260"> |
| Music playing, with Claude working beside it | <img src="docs/images/musiccompact-en.png" alt="Compact music with the Claude bubble" width="380"> |
| Claude working, with the time so far | <img src="docs/images/claudecompact-en.png" alt="Compact Claude activity" width="320"> |
| Claude finished or needs you | <img src="docs/images/banner-en.png" alt="Claude alert" width="420"> |
| A Windows notification | <img src="docs/images/notification-en.png" alt="Notification banner" width="420"> |
| Claude expanded, with the limits | <img src="docs/images/claudeexpanded-en.png" alt="Claude expanded with usage rings" width="460"> |

## Install

1. [Build](#build-from-source) `WindowsIsland.exe`, or download it from a release once there is one.
2. **Open it** and choose **Yes**. It installs for your Windows user only, with no administrator rights, adds Start menu and Desktop shortcuts and starts with Windows.
3. The island appears at the top of the screen, and its icon in the tray.

To try it without installing, run `WindowsIsland.exe --portable`.

## Use

1. **Hover** over the island to expand it, or **click** it. Move the mouse away and it collapses.
2. **Click** the progress bar to jump to that point of the song.
3. **Click** an alert or notification to dismiss it.
4. **Right-click** the tray icon for the options and **Quit**.

| Option | What it does |
|---|---|
| Show music | Shows what is playing |
| Show Claude | Shows Claude Code activity and limits |
| Claude alerts | Expands the island when Claude finishes or needs you |
| Show Windows notifications | Shows new notifications in the island. Off by default |
| Show pill when idle | Keeps the small pill on screen when nothing is happening |
| Expand on hover | Expands when the mouse rests on the island. When off, click it |
| Hide in fullscreen | Hides the island during games, videos and presentations |
| Position | Top of the screen, or just above the taskbar |
| Start with Windows | Opens the app when you sign in to Windows |
| Open settings file | Opens `settings.json`, where `OffsetY` sets the distance from the edge |

## How the data is read

- **Claude activity:** Claude Code keeps one small file per running session in `~/.claude/sessions`. The island reads only its kind, process ID, status, name and status time. It works with the terminal and with the desktop app.
- **Claude limits:** every 2 minutes, and when you open the Claude panel, the island runs `claude -p /usage` in the background, the same numbers you see in Claude Code. It makes no model call and spends no tokens. If [Claude Usage Taskbar](https://github.com/leotrvs/claude-usage-taskbar) is installed and has fresher numbers, the island uses those.
- **Notifications:** the official Windows API for reading notifications needs a signed package, so the island reads, read-only, the local database where Windows keeps them. It shows only notifications that arrive while it runs and stores nothing. Windows still shows its own banner too: turn off **Show notification banners** for an app in Windows Settings if you only want the island.

Details are in [SECURITY.md](SECURITY.md).

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

Planned next: automatic updates from releases, auto-hide and how long the island stays open, an empty island mode, size, position and monitor settings, and Codex usage. The full list is in [docs/ROADMAP.md](docs/ROADMAP.md).

## License

[MIT](LICENSE). The Claude name and logo are not covered.
