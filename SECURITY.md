# Security

**English** | [Português (Brasil)](docs/SECURITY.pt-BR.md)

## What the app touches

| Item | Access | Why |
|---|---|---|
| Windows media controls | Reads the title, artist, artwork, position and playback state of the current player. Sends play, pause, next, previous and seek when you click them | Shows and controls music |
| `~/.claude/sessions` | Reads the kind, process ID, status, name and status time of each session, every second | Shows whether Claude Code is working or waiting for you. Nothing is stored. |
| `~/.claude/projects/*/<session>.jsonl` (Claude Desktop chats) | While a chat is busy, reads the end of its transcript once per second, only when the file changed | Finds a tool call that has no result yet, to show "waiting for you" when Claude Desktop asks for a permission. It takes only tool names and times, never what was said. Nothing is stored |
| Chrome's and Edge's `Notification Resources` folder | Reads the one icon file a website notification points to, only for Chrome or Edge notifications and only inside the browser's own profile folders (no links, PNG, JPEG or ICO, at most 512 KB) | Shows the site's icon on the notification. Nothing is stored |
| Claude Code's `/usage` command | Runs it every 2 minutes and when you open the Claude panel | Keeps only the limit percentages and reset times, in memory. It makes no model call and spends no tokens. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar\usage.json` | Reads it when it changes, if it exists | Uses Claude Usage Taskbar's limits when they are fresher |
| `%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db` | Reads it, read-only, every 1.5 seconds, only when **Show Windows notifications** is on | Shows the app name, title and text of notifications that arrive while the island runs. Nothing is stored. |
| `~/.codex/sessions` | Reads the end of the newest session file every 2 seconds, while **Show Codex** is on | Takes only whether the last task started or finished and the rate-limit percentages and reset times. Nothing is stored. |
| `HKCU\...\CapabilityAccessManager\ConsentStore` | Reads two timestamps per app (last start and stop of microphone and camera use) every 2 seconds, while **Microphone and camera dots** is on | Shows the dots. It never records or listens to audio or video, and does not read which app it is |
| The Windows clipboard, for screenshots | Is told when the clipboard changes (no polling), while **Screenshot alert** is on, and checks only that an image arrived and that Windows Explorer or the Snipping Tool put it there | Shows a small note. It never reads the picture or any clipboard text, and it does not watch the keyboard |
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

## Network access

The island itself makes one kind of connection (the second one below is optional): about 30 seconds after it opens and then every hour, it downloads release files from this repository on GitHub, unless you turn off **Automatic updates**. It does the same when you click **Check for updates**. GitHub can see your IP address, as with any download. No Claude or personal data is sent. An update is installed only when its RSA signature matches the public key built into the app and the file's SHA-256 matches the signed manifest.

When the browser extension is installed and a tab is playing sound, the island also downloads that site's public `/apple-touch-icon.png` (HTTPS only, no redirects, PNG up to 256 KB, once per site) to show a sharp icon. The site sees an ordinary request from your IP address and nothing else is sent.

Privacy note: this download goes out directly from your computer, not through the browser's proxy or VPN, and also happens for tabs in private windows if the extension is allowed there. The island refuses addresses that point to this computer or the local network and gives each download four seconds and 256 KB.

The `/usage` command is part of Claude Code, which connects to Anthropic with your own login. The island never sees that login.

## Report a vulnerability

Use **Report a vulnerability** in the Security tab of this repository. Please do not open a public issue.
