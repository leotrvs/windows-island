# Changelog

## 1.7.3

- Fixed the island briefly showing Claude as working, with a timer, every time the limits were read. The short-lived Claude Code started for the `/usage` read no longer counts as a chat

## 1.7.2

- The Codex section of the Claude panel now shows only while Codex is working, instead of whenever its files exist on the PC

## 1.7.1

- Fixed the expanded Claude panel cutting off the Codex section at the bottom

## 1.7.0

- **Timer and stopwatch** from the tray menu (**Timer**): a live activity in the island, with an alert when the countdown ends
- **Microphone and camera dots:** a small orange or green dot beside the island while an app uses them. Off with **Microphone and camera dots** in Settings
- **Battery alerts** on laptops: charger connected or removed, low battery (20% and 10%) and fully charged

## 1.6.0

- **Lighter app:** the tray icon and the monitor list now use Windows directly instead of Windows Forms. The download is about 10 MB smaller (78 MB to 68 MB) and the behavior is the same
- Memory stays around 40 MB, also with one island on each of three monitors

## 1.5.0

- **Codex** joins the island: a live activity with a timer while Codex works, its 5-hour and weekly limits under Claude's in the expanded panel, and a **Codex finished** alert. Read from the session files Codex keeps, without touching its login
- **Show Codex** and **Codex alerts** in Settings

## 1.4.1

- Fixed the **With the mouse** monitor mode: after moving to another screen the island flashed and then stayed invisible on every screen
- Arriving on a new screen counts as activity, so auto-hide waits its full time there

## 1.4.0

- **Monitor** in Settings: show the island on the primary monitor, on a specific one, on all of them at once, or on the one with the mouse, where it hops over as the cursor moves
- Fullscreen and maximized windows are now detected on each island's own monitor

## 1.3.0

- **Settings window** in the style of iOS Settings, with every option. Click the tray icon, choose **Settings…**, or open the shortcut while the island runs
- **Size:** separate sizes for the collapsed and the expanded island, from 80% to 130%
- **Position:** distance from the edge and horizontal position
- The tray menu keeps only the quick actions

## 1.2.0

- **Auto-hide when idle:** the island hides after it has shown nothing for 5 seconds to 5 minutes, and comes back with new activity or when the mouse reaches its spot. Off by default
- **Alert duration:** choose how long Claude alerts, notifications and app messages stay on screen
- **Hide paused music after:** choose how long a paused song stays, from at once to a minute
- Values typed in `settings.json` are kept within sensible bounds

## 1.1.0

- **Empty island** in the tray menu: the collapsed island stays a plain pill and no longer grows while music plays or Claude works. Hover or click it to see the activities. Alerts and notifications keep following their own options

## 1.0.2

- Notification text is read more defensively: XML definitions (DTDs) are refused, and oversized notifications and texts are cut, since any app or website can send a notification

## 1.0.1

- Updates are signed with the same key as Claude Usage Taskbar. Each update file now names its app, and the island rejects any file meant for another app
- Copies of 1.0.0 cannot update to this version by themselves: download it once from this release

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
