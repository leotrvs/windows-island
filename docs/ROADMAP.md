# Roadmap

Ideas planned for upcoming releases, in the order they are expected to land. Nothing here is promised for a specific version.

## Next: lighter app (1.6)

- A smaller download: today the self-contained exe is about 78 MB. Replacing Windows Forms (used for the tray icon and the monitor list) with direct Windows calls should cut it down
- Measure and trim memory use when one island runs on every monitor

## Then: more live activities (1.7)

- Timer and stopwatch
- Battery and charging, on laptops
- The microphone and camera in use, like the green and orange dots on iPhone

## Later

- A way for scripts and other apps to show their own live activities
- The real tab icon (favicon) for web audio without artwork. Windows does not report the tab or site, so this needs a small browser extension that tells the island which tab is playing
- App icons on notification banners
- Sharp islands on monitors with different display scales (per-monitor DPI)
- Notifications through the official Windows listener, once the app ships as a signed package

## Done

- Codex activity, limits and alerts (1.5.0)
- Choosing the monitor: primary, a specific one, all, or the one with the mouse (1.4.0)
- Settings window, and the size and position of the island (1.3.0)
- Configurable alert and paused-music durations, and auto-hide (1.2.0)
- Empty island mode (1.1.0)
- Signed automatic updates from GitHub releases (1.0.0)
- Start menu and Desktop shortcuts, created by the installer (0.2.0)
- Reading Claude's limits without Claude Usage Taskbar (0.2.0)
- Windows notifications in the island (0.2.0)
- Installer and uninstaller (0.2.0)
- Lower memory use (0.2.0)
