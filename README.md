# Windows Island

**English** | [Português (Brasil)](README.pt-BR.md) | [Español](README.es.md) | [Русский](README.ru.md)

A Dynamic Island for Windows. Music, Claude Code and its usage limits live in a small pill at the top of your screen, with iPhone-style looks and spring animations.

<p align="center">
<img src="docs/images/musicexpanded-en.png" alt="The island expanded with the song playing" width="460">
</p>

> Unofficial community project. It is not affiliated with, endorsed by, or supported by Apple or Anthropic. "Claude" is a trademark of Anthropic.

> **Early preview.** There is no installer yet. [Build it from source](#build-from-source) to try it.

## Features

- **Music** from any player in the Windows media controls, such as Spotify or a browser: artwork, an equalizer tinted by the artwork, a progress bar you can click to seek, and play, pause, next and previous
- **Claude Code live activity:** how long Claude has been working, and when it is waiting for you
- **Claude usage:** rings for the **5-hour** and **weekly** limits, with reset times
- **Alerts** when Claude finishes a task or needs you
- **Two activities at once:** the second one splits off into a bubble beside the island
- **Fluid motion:** shape and content morph with springs, never with fixed-time fades
- **Discreet:** it never takes focus, clicks around it reach the window below, it shrinks to a thin line when a maximized window is in front and it hides in fullscreen
- Top or bottom position, start with Windows, English and Brazilian Portuguese
- **Never reads your Claude login or token**, and makes no network connections

## States

| State | Look |
|---|---|
| Idle | <img src="docs/images/idle-en.png" alt="Idle pill" width="260"> |
| Music playing, with Claude working beside it | <img src="docs/images/musiccompact-en.png" alt="Compact music with the Claude bubble" width="380"> |
| Claude working, with the time so far | <img src="docs/images/claudecompact-en.png" alt="Compact Claude activity" width="320"> |
| Claude finished or needs you | <img src="docs/images/banner-en.png" alt="Claude alert" width="420"> |
| Claude expanded, with the limits | <img src="docs/images/claudeexpanded-en.png" alt="Claude expanded with usage rings" width="460"> |

## Use

1. **Hover** over the island to expand it, or **click** it. Move the mouse away and it collapses.
2. **Click** the progress bar to jump to that point of the song.
3. **Right-click** the tray icon for the options and **Quit**.

| Option | What it does |
|---|---|
| Show music | Shows what is playing |
| Show Claude | Shows Claude Code activity and limits |
| Claude alerts | Expands the island when Claude finishes or needs you |
| Show pill when idle | Keeps the small pill on screen when nothing is happening |
| Expand on hover | Expands when the mouse rests on the island. When off, click it |
| Hide in fullscreen | Hides the island during games, videos and presentations |
| Position | Top of the screen, or just above the taskbar |
| Start with Windows | Opens the app when you sign in to Windows |
| Open settings file | Opens `settings.json`, where `OffsetY` sets the distance from the edge |

## How Claude data is read

- **Activity:** Claude Code keeps one small file per running session in `~/.claude/sessions`. The island reads only its kind, process ID, status, name and status time. It works with the terminal and with the desktop app.
- **Limits:** the island reads the percentages and reset times that [Claude Usage Taskbar](https://github.com/leotrvs/claude-usage-taskbar) saves. Without it, the limits area shows a hint and everything else keeps working.

Details are in [SECURITY.md](SECURITY.md).

## Build from source

You need Windows 10 version 2004 or later, or Windows 11, and the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/leotrvs/windows-island.git
cd windows-island
pwsh -File scripts/build.ps1
.\bin\WindowsIsland.exe
```

Contributors should read [CONTRIBUTING.md](CONTRIBUTING.md).

## Roadmap

- Installer and signed automatic updates
- Reading the limits by itself, without Claude Usage Taskbar
- Windows notifications in the island
- Timers, battery, and the microphone and camera in use
- A way for scripts and other apps to show their own live activities
- A settings window, themes and multiple monitors
- Lower memory use

## License

[MIT](LICENSE)
