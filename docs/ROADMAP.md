# Roadmap

Ideas planned for upcoming releases. Nothing here is promised for a specific version.

## Behavior

- **Settings window:** an iOS-style window for every option, since the tray menu is getting long
- **Size and position:** set the size of the collapsed and the expanded island, and nudge its position, separately for each state

## Monitors

- Choose where it appears: the primary monitor, a specific monitor, all monitors, or the active monitor (the one with the mouse)

## Activities

- **Codex usage**, next to Claude. To investigate: the Codex CLI writes session logs under `~/.codex/sessions` that appear to include rate-limit percentages; check whether that is stable enough to read, the same read-only way Claude's session files are read
- Timers, battery, and the microphone and camera in use
- A way for scripts and other apps to show their own live activities
- App icons on notification banners
- The real tab icon (favicon) for web audio without artwork. Windows does not report the tab or site, so this needs a small browser extension that tells the island which tab is playing
- Notifications through the official Windows listener, once the app ships as a signed package

## Install and updates

- A smaller download: today the self-contained exe is about 78 MB. Dropping Windows Forms for the tray icon and trimming the Windows API projections should cut it down

## Done

- Configurable alert and paused-music durations, and auto-hide (1.2.0)
- Empty island mode (1.1.0)
- Signed automatic updates from GitHub releases (1.0.0)
- Start menu and Desktop shortcuts, created by the installer (0.2.0)
- Reading Claude's limits without Claude Usage Taskbar (0.2.0)
- Windows notifications in the island (0.2.0)
- Installer and uninstaller (0.2.0)
- Lower memory use (0.2.0)
