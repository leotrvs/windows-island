# Changelog

## 1.0.0

First release.

- Signed automatic updates from GitHub releases: the island checks every hour, verifies the signature and the file's hash, and restarts into the new version when you are not using it. **Automatic updates** and **Check for updates** are in the tray menu
- A paused song leaves the island after 5 seconds instead of 30
- Everything from the previews: music, Claude Code activity and limits, alerts, Windows notifications, the installer and spring animations

## 0.2.1

- Music follows whichever player is actually playing. Before, a paused player could stay on the island for several seconds after another one started
- Web pages that play audio without artwork, such as a WhatsApp voice message, show a tile with the page's initial instead of the browser logo

## 0.2.0

- Reads Claude's limits by itself through Claude Code's `/usage` command, so Claude Usage Taskbar is no longer needed. Its numbers are still used when they are fresher
- Windows notifications in the island, off by default. The Claude app's own toasts are skipped while the island's Claude alerts are on
- The Claude spark plays a smooth thinking animation while Claude works
- Installer: Start menu and Desktop shortcuts, start with Windows, an entry in Windows' app list and an uninstaller. `--portable` runs without installing
- The exe runs without .NET installed
- About 40 MB of memory instead of 166 MB, by drawing the island on the CPU
- Roadmap in `docs/ROADMAP.md`

## 0.1.0

First preview.

- An island at the top of the screen that morphs between idle, compact and expanded states with spring animations
- Music from any player in the Windows media controls: artwork, an equalizer tinted by the artwork, a progress bar you can click to seek, and play, pause, next and previous
- Claude Code live activity: how long Claude has been working, when it waits for you, and idle sessions
- Claude usage rings for the 5-hour and weekly limits, read from Claude Usage Taskbar
- Alerts when Claude finishes a task or needs you
- A second activity splits off into a bubble beside the island
- Shrinks to a thin line when a maximized window is in front and hides in fullscreen
- Tray menu with top or bottom position, start with Windows and what to show
- English and Brazilian Portuguese
