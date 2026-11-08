# Changelog

## 2.4.0

- **Limit alerts:** a note when the Claude 5-hour or weekly limit reaches 75%, 90% and 100% and when it renews (after it had reached 75% or more). The 100% note says "Limit reached" with the window below. On by default; turn it off in Settings, Activities, Limit alerts. The first reading when the island starts is never announced
- **New defaults** (for new installs; an existing settings file keeps its values): alert duration 3 s, auto-hide when idle after 5 s, paused music hidden at once


## 2.3.5

- **Claude Desktop questions:** a chat that ends its turn with a question for you (the yellow dot in Claude Desktop, such as "Publish?") now shows as waiting for you in the island, and the alert says Claude needs you instead of only "finished"
- README images: the notification and battery images are correct again, and new ones show Claude asking for you, several chats working and one of two waiting


## 2.3.4

- Settings footer shows the GitHub mark and `@leotrvs/windows-island` (a link to the project)
- **Security review:** the site-icon download refuses addresses on this computer or the local network and has a 4 second, 256 KB limit for the whole transfer; website notification icons are read only from Chrome's and Edge's own folders, never through links, and only if they are PNG, JPEG or ICO; the extension's `config.json` is readable only by your Windows user; the extension button types the address in one batch; the API token is kept out of the Win+V clipboard history; the Windows volume popup is given back at the next start even after a crash
- **Performance:** the volume is read every 250 ms and only every 40 ms for two seconds after a change; the transcript of a chat that cannot be found is not searched for every second
- README images and GIFs regenerated, READMEs and security notes updated for the new features


## 2.3.3

- Settings footer: the version (Windows Island v2.3.3) on the left and a link to the project on GitHub on the right


## 2.3.2

- Resting the mouse on the small second activity beside the island (for example the Claude spark while music plays) opens the island directly on that activity's page


## 2.3.1

- Compact island with several chats: when some are waiting it shows how many of them (1/2 · Waiting) instead of the total, which looked like all were waiting


## 2.3.0

- **Claude Desktop permissions:** the island now shows "waiting for you" when a chat in Claude Desktop is asking for a permission or putting a question (Desktop reports those chats as busy). It reads only tool names and times from the chat transcript
- **Several chats at once:** with more than one chat working, the compact island shows a count before the time (for example 2 · 0:58) and the Claude panel lists up to three chats, each with its own state and time


## 2.2.3

- Settings window keeps the edge nearest the taskbar in place when it grows or shrinks: near the bottom it stays anchored there (and goes back down when a group is folded); in the upper half it keeps its top


## 2.2.2

- Settings window now also shrinks when a group is folded or a shorter tab is opened (it only grew before)


## 2.2.1

- **Settings window** fits its content: it opens, switches tabs and unfolds groups at the height it needs, so nothing has to be scrolled (as far as the screen allows); it grows upwards when it reaches the bottom edge
- Cards stacked in a tab (Behavior and Pages) now have a clear gap between them


## 2.2.0

- **Tray icon:** a second left click now closes the Settings window
- **Tray menu:** redrawn to match the Settings window (dark, rounded, with a soft entrance and exit); the Timer choices fold open under their row; Esc or a click elsewhere closes it


## 2.1.9

- **YouTube and other video sites:** the island no longer stays on the browser logo. The video preview is read again when it arrives late, and a preview already shown is kept instead of flipping back to the logo


## 2.1.8

- **Volume bar:** reads the volume every 40 ms (was 250 ms) and glides to each level, so holding a key moves smoothly instead of in jumps
- **Hide Windows' volume popup:** its window is made fully transparent ahead of time, so the instant before it is hidden no longer flashes; it is restored when the option is turned off or the island quits


## 2.1.7

- Website audio: the island now uses the site's own touch icon (apple-touch-icon.png) when it has one, so WhatsApp shows its logo instead of a blurry tab icon with the unread badge. Downloaded once per site, HTTPS only (see SECURITY.md)


## 2.1.6

- **Tidier Settings:** related options fold under arrows (Privacy, Battery, System alerts, Volume and brightness, Times, Size and position; and the API token, guide and browser extension). The system monitor style only shows while the monitor is on


## 2.1.5

- Notification icons (apps and websites) no longer sit on a gray tile: they show directly on the island with rounded corners, so transparent icons blend in


## 2.1.4

- Small website icons in the music view are enlarged with smooth interpolation, so they look soft instead of blocky


## 2.1.3

- **Website audio:** the equalizer takes the color of the site icon (WhatsApp is green, not purple); icons are drawn smoother and the extension sends a larger one (reload the extension to get it)
- **Website notifications** from Chrome/Edge now show the site icon and name instead of the browser logo
- **Microphone/camera dots** no longer hide or slide when the island is tucked away or auto-hidden; they stay in place


## 2.1.2

- **Browser extension button:** opens the extensions page itself (browsers ignore chrome:// addresses from the command line and show a blank tab, so the address is typed into that tab, only when a blank new tab is in front)
- Copying the folder path is retried and checked; if it still fails, the folder opens in Explorer instead of saying it was copied. The steps also name "Modo de programador" and "Carregar descompactada" for Portuguese (Portugal) browsers

## 2.1.1

- **Tuck away:** the island now comes back after 2.5 seconds, and also when the mouse rests on the top edge of the screen in the middle (where it sits), so it does not have to be on its exact spot

## 2.1.0

- **System monitor:** an option in Settings, Appearance, keeps the processor and memory load on the island when nothing else is happening. Rings (the default), bars or numbers; off by default
- **Tuck away:** drag the island up (down when it sits at the bottom) to put it away so you can use what is behind it, such as browser tabs. It comes back with a new activity, an alert, a microphone or camera in use, or when the mouse rests on its spot for a few seconds

## 2.0.1

- **Settings window:** remembers its size and place, and no longer jumps when it opens
- **Browser extension button:** opens the extensions page in the profile you used last (no more "who is using Chrome?" page), with short numbered steps
- **Local API settings:** now say what the token is for (only for your own scripts; the island command and the extension need none) and link to the guide
- **App name on the dots:** shows when you hover the dots themselves, not when you open the island
- **Hide Windows' volume popup (experimental):** now reacts the instant the popup appears, so it no longer flashes

## 2.0.0

- **App names on the dots:** hover the island to see which app uses the microphone or camera, beside the dot. Settings can also show the name by itself for 1, 2 or 3 seconds when an app starts (off by default)
- **Battery as an activity:** while the charger is plugged in and the battery is not full, the island shows the charge level like any other activity
- **Choose your pages:** Settings, Behavior, lets you turn each page of the open island on or off
- **App icons on notifications:** Windows notifications show the real icon of the app that sent them (loaded from the shell, only for plain app ids)
- **Volume and brightness:** a small bar while you change the volume or the laptop screen brightness
- **Hide Windows' volume popup (experimental, off by default):** hides Windows' own volume and brightness popup so only the island's bar shows. It relies on an undocumented shell window, so a Windows update may stop it working
- **Devices and network:** a short note when a Bluetooth or USB device connects or disconnects, and when the internet goes away or comes back
- **Do not disturb:** a small moon on the island while Do not disturb or a focus session is on
- **Clock and system page:** the time and date, with processor and memory load
- **Keyboard shortcut:** opens and closes the island from anywhere. Off by default and configurable (Ctrl+Alt+I to begin with); it uses Windows' own hotkey call, so no keyboard hook is installed
- **Browser extension:** the `extension/` folder has a small Chrome and Edge extension that tells the island which tab is playing, so web audio shows that site's icon instead of a letter. Settings has an install button that unpacks it with its token and opens the browser's extensions page
- **Local API:** scripts and apps can show their own live activities and alerts, and a browser extension can say which tab is playing. Off by default; it listens on 127.0.0.1 only and needs a secret token. The `island.cmd` command line beside the app does the same from a terminal. See [docs/API.md](docs/API.md)

## 1.13.0

- **Sharp on every monitor:** the island now follows each monitor's own display scale (per-monitor DPI), so it is no longer stretched and blurry on a screen with a different scale than the main one
- `--dev` no longer needs `--portable`

## 1.12.0

- **Screenshot alert:** a very small note, "Screenshot taken", when you take a screenshot with Print Screen or Win+Shift+S. It does not queue up or repeat for a burst of screenshots, and it can be turned off in Settings, Activities. It only notices that an image reached the clipboard from Windows' own screenshot tools: it never reads the picture and does not watch the keyboard
- **Tabs and options slide:** every tab bar and choice in Settings now has a pill that slides to the one you pick
- The language choice is now just **Auto**, English, Português, Español and Русский
- Sharper text in Settings (ClearType instead of grayscale smoothing)
- Fixed the spinning update icon turning around a point outside itself
- The Claude and Codex rows in Settings use their own marks
- `--dev` runs a build beside the installed copy, for testing

## 1.11.0

- **Check for updates** now shows what it is doing: a spinning icon and "Checking for updates…", then a download icon and "Download update 1.x" when one is found. Nothing is downloaded until you click. Automatic updates still install by themselves
- **Languages:** English, Portuguese, Spanish and Russian, picked automatically from Windows. Change it in Settings, System, Language
- **Inter** is now the font, bundled in the app (SIL Open Font License)
- **Smaller alerts:** notifications and alerts are about 20% smaller, with a Small or Normal choice in Settings, Appearance
- **Battery alerts** have their own icon (charging, low, full). Previews are in the README
- **Icons** on every row of the Settings window, and a really thin scroll bar
- **Defaults:** collapsed size 90%, expanded size 95%, distance from the edge 5 px (new installs; your own choices are kept)
- `--preview=Settings` opens the Settings window with sample data

## 1.10.1

- The Settings window now behaves like a tray flyout: it takes the focus (which closes the hidden-icons panel), slides up into the corner by the taskbar, stays above other windows until you click elsewhere, and Esc closes it
- Animated GIF at the top of the README, recorded from sample data (`scripts/render-gif.ps1`)

## 1.10.0

- **Codex icon:** the Codex logo replaces the old prompt symbol in the island
- **Usage ring color:** blue by default, like in Claude and Codex themselves, with Green and Purple in Settings, under Appearance. The rings still turn amber and red as the limit gets close
- **Settings window:** a black header with a close button instead of the gray Windows title bar (no minimize or maximize), and it opens higher, clear of the hidden-icons flyout
- The collapsed music view is narrower
- New README picture showing the collapsed and expanded states, and a blue Download button that stands out on the dark GitHub theme

## 1.9.0

- **Pages in the island:** when it is expanded, scroll the mouse wheel over it or click the dots to move between music, Claude (with Codex) and the timer. You can see Claude's limits and sessions without pausing the music
- **Timer page:** choose a length with - and +, start a countdown or a stopwatch, pause, resume and stop, all inside the island
- The expanded music view is shorter, with less empty space
- The compact Claude and Codex views are a little narrower

## 1.8.0

- **App icon** for the exe, the shortcuts and the Settings window, instead of the default Windows one
- **Settings window:** opens in the corner by the taskbar (and can be moved), with tabs on top (Activities, Behavior, Appearance, System) instead of one long page. Thinner scroll bar and smaller switches
- **Microphone dots:** smaller and softer, and the microphone is now also detected through Windows' audio sessions, which catches apps the previous method missed, such as dictation in some desktop apps

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
