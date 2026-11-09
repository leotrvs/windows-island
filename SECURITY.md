# Security

**English** | [Português (Brasil)](docs/SECURITY.pt-BR.md)

## What the app touches

| Item | Access | Why |
|---|---|---|
| Windows media controls | Reads the title, artist, artwork, position and playback state of the current player. Sends play, pause, next, previous and seek when you click them | Shows and controls music |
| `~/.claude/sessions` | Reads the kind, process ID, status, name and status time of each session, every second | Shows whether Claude Code is working or waiting for you. Nothing is stored. |
| `~/.claude/projects/*/<session>.jsonl` (Claude Desktop chats) | While a chat is busy, reads the end of its transcript once per second, only when the file changed | Finds a tool call that has no result yet, to show "waiting for you" when Claude Desktop asks for a permission. It takes only tool names and times, never what was said. Nothing is stored |
| `crash.log` and `restarts.txt` in the settings folder | The app writes the details of its own failures (and when it reopened itself) | Lets Settings copy a report for a GitHub issue. Both files stay on this PC and nothing is sent; the report hides the user's name and leaves out app names, keys and the team |
| Chrome's and Edge's `Notification Resources` folder | Reads the one icon file a website notification points to, only for Chrome or Edge notifications and only inside the browser's own profile folders (no links, PNG, JPEG or ICO, at most 512 KB) | Shows the site's icon on the notification. Nothing is stored |
| Claude Code's `/usage` command | Runs it every 2 minutes and when you open the Claude panel | Keeps only the limit percentages and reset times, in memory. It makes no model call and spends no tokens. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar\usage.json` | Reads it when it changes, if it exists | Uses Claude Usage Taskbar's limits when they are fresher |
| `%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db` | Reads it, read-only, every 1.5 seconds, only when **Show Windows notifications** is on (the default; the welcome asks, and you can turn it off) | Shows the app name, title and text of notifications that arrive while the island runs. Nothing is stored. |
| `~/.codex/sessions` | Reads the end of the newest session file every 2 seconds, while **Show Codex** is on | Takes only whether the last task started or finished and the rate-limit percentages and reset times. Nothing is stored. |
| `HKCU\...\CapabilityAccessManager\ConsentStore` | Reads two timestamps per app (last start and stop of microphone and camera use) every 2 seconds, while **Microphone and camera dots** is on | Shows the dots. It never records or listens to audio or video, and does not read which app it is |
| The Windows clipboard, for screenshots | Is told when the clipboard changes (no polling), while **Screenshot alert** is on, and checks only that an image arrived and that Windows Explorer or the Snipping Tool put it there | Shows a small note. It never reads the picture or any clipboard text, and it does not watch the keyboard |
| `HKCU\...\CapabilityAccessManager\ConsentStore\graphicsCapture*` | Reads two timestamps per app (last start and stop of screen capture) every 2 seconds, while **Microphone and camera dots** and **Screen sharing** are on | Shows the blue dot while an app shares or records the screen. It never captures or reads the screen itself |
| Discord's local RPC pipe (`\\.\pipe\discord-ipc-N`) | Only while **Show when I am muted in Discord** is on: connects to the pipe, checks that the program serving it runs in your session and lives in your own Discord folder under `%LOCALAPPDATA%`, and reads whether you are muted or deafened | Shows a crossed microphone or headphones. It reads nothing else, and the setup is described in [docs/DISCORD.md](docs/DISCORD.md). The client secret and Discord's tokens are kept encrypted for your Windows user in `%APPDATA%\WindowsIsland\discord.dat` |
| The disk-write counter of the `steam` process (`GetProcessIoCounters`) | While **Steam downloads** is on and a download runs, every 3 seconds: how many bytes Steam has written | Moves the percent of a download between two writes of its manifest. Nothing else about the process is read |
| Steam's `steamappsppmanifest_*.acf` files (in each Steam library folder, found from the registry and `libraryfolders.vdf`) | While **Steam downloads** is on (the default): reads the game's name and how many bytes are downloaded, every 3 seconds (a file only when it changed) | Shows the progress of a Steam download or update. Read only; nothing is sent and Steam is not touched |
| Whether the keyboard's media next and previous keys are down (`GetAsyncKeyState`, only those two keys) | Always, 8 times a second | To tell whether a song change was "next" or "previous" for the music notice. No other key is looked at and nothing is recorded |
| The newest video in `Videos\Captures` (the Windows Game Bar's recordings) | While **Screen recording** is on, every 2 seconds: its name and date, and whether another program can still open it (while the Game Bar records, it cannot) | Shows the Game Bar recording and for how long. The video itself is never opened for reading |
| OBS's own log (`%APPDATA%\obs-studio\logs`) | While **OBS recording** is on and OBS runs: the lines that say recording or streaming started or stopped | Shows OBS recording or streaming and for how long. Read only |
| The `TiWorker` process and the `RebootRequired` registry key of Windows Update | While **Windows Update** is on: whether Windows servicing is busy and whether a restart is pending | Shows "Installing updates" and "Restart to finish". Nothing else about the system is read |
| ESPN's public scoreboard (`site.api.espn.com`, https only) | Only if you turn on **Football score**, every 3 minutes (30 seconds during a match of your team): a request for each league code you typed | Shows the live score. Your team's name is used on this PC to pick the match and is never sent |
| Browser downloads (the extension's `downloads` permission) | The file name and percent of each download, sent only to the island on `127.0.0.1` | Shows download progress. Nothing goes anywhere else |
| Windows audio sessions (Core Audio) | Asks every 2 seconds whether any microphone has an active recording session, while **Microphone and camera dots** is on | Shows the dot. It reads only the active/inactive state and the process number: no audio is opened or heard, and no app name is kept |
| `%APPDATA%\WindowsIsland\settings.json` | Reads and writes | Stores preferences |
| `%LOCALAPPDATA%\Programs\WindowsIsland` | Reads and writes | Holds the installed app |
| Windows Run key, Apps list, shortcuts | Reads and writes | Start with Windows, uninstall entry, and Start menu and Desktop shortcuts |

## What the app never does

- Read `~/.claude/.credentials.json`, `~/.codex/auth.json` or any other Claude or Codex login, token or cookie
- Call Anthropic's APIs
- Store prompts, responses, session names or project paths
- Read your notifications unless you turn them on, or read the content of other windows
- Send analytics or telemetry
- Require administrator rights

## Answering Claude Code's permissions (off by default)

**Answer permissions on the island** in Settings adds one entry to Claude Code's user settings (`~/.claude/settings.json`): a `PermissionRequest` hook that runs `WindowsIsland.exe permission-hook`. Claude Code runs it only when it is about to ask you for a permission. The hook sends the tool name and a short summary of what it wants to do (up to 280 characters of the command, file, address or query) to the island's local API on this computer, with the API token, and waits for your click.

- **Only a click decides.** Allow and Deny are answered only by clicking the buttons. If you do not click within 60 seconds, the island is not running, the API is off or anything fails, the hook prints nothing and exits normally, so Claude Code shows its own prompt exactly as before.
- **It shows a summary, not everything.** The summary is the command, file, address or query itself (never a description the model wrote), on one line with line breaks shown as ⏎, and with the project folder. When it is longer than 280 characters, or the tool has no such field, **Allow is not offered**: you can Deny or open Claude to decide there.
- **A click must be deliberate.** The buttons wake up about half a second after the prompt appears, and a click only counts when it is pressed and released on the same button, so a click meant for the window underneath (or a double click) cannot answer it.
- **One entry, backed up.** Only the island's own entry is added or removed. A copy of your settings file is saved beside it as `settings.json.island-backup` the first time (and `settings.json.island-backup-last` before every change), the file is not rewritten when the entry is already right, and nothing is changed if the file cannot be read. Comments inside the file are lost when it is rewritten; the backup keeps them. Turning the option off, or uninstalling the island, removes the entry.
- **The summary stays in memory** and is never stored. The island only sends the token to its own process (it checks who owns the port first).
- **Any program that has the token could show a prompt**, but a prompt can only be answered by your click and only returns to the program that asked.

## Network access

The island itself makes one kind of connection (the second one below is optional): about 30 seconds after it opens and then every hour, it downloads release files from this repository on GitHub, unless you turn off **Automatic updates**. It does the same when you click **Check for updates**. GitHub can see your IP address, as with any download. No Claude or personal data is sent. An update is installed only when its RSA signature matches the public key built into the app and the file's SHA-256 matches the signed manifest.

With the Discord option on, the island also contacts `https://discord.com/api/oauth2/token` to trade Discord's approval for a token, and again to renew it. Nothing else is sent to Discord from the internet side; the voice state comes from the Discord app on this computer.

When the browser extension is installed and a tab is playing sound, the island also downloads that site's public `/apple-touch-icon.png` (HTTPS only, no redirects, PNG up to 256 KB, once per site) to show a sharp icon. The site sees an ordinary request from your IP address and nothing else is sent.

Privacy note: this download goes out directly from your computer, not through the browser's proxy or VPN, and also happens for tabs in private windows if the extension is allowed there. The island refuses addresses that point to this computer or the local network and gives each download four seconds and 256 KB.

The `/usage` command is part of Claude Code, which connects to Anthropic with your own login. The island never sees that login.

## Report a vulnerability

Use **Report a vulnerability** in the Security tab of this repository. Please do not open a public issue.
