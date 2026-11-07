# Security

**English** | [Português (Brasil)](docs/SECURITY.pt-BR.md)

## What the app touches

| Item | Access | Why |
|---|---|---|
| Windows media controls | Reads the title, artist, artwork, position and playback state of the current player. Sends play, pause, next, previous and seek when you click them | Shows and controls music |
| `~/.claude/sessions` | Reads the kind, process ID, status, name and status time of each session, every second | Shows whether Claude Code is working or waiting for you. Nothing is stored. |
| Claude Code's `/usage` command | Runs it every 2 minutes and when you open the Claude panel | Keeps only the limit percentages and reset times, in memory. It makes no model call and spends no tokens. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar\usage.json` | Reads it when it changes, if it exists | Uses Claude Usage Taskbar's limits when they are fresher |
| `%LOCALAPPDATA%\Microsoft\Windows\Notifications\wpndatabase.db` | Reads it, read-only, every 1.5 seconds, only when **Show Windows notifications** is on | Shows the app name, title and text of notifications that arrive while the island runs. Nothing is stored. |
| `~/.codex/sessions` | Reads the end of the newest session file every 2 seconds, while **Show Codex** is on | Takes only whether the last task started or finished and the rate-limit percentages and reset times. Nothing is stored. |
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

The island itself makes one kind of connection: about 30 seconds after it opens and then every hour, it downloads release files from this repository on GitHub, unless you turn off **Automatic updates**. It does the same when you click **Check for updates**. GitHub can see your IP address, as with any download. No Claude or personal data is sent. An update is installed only when its RSA signature matches the public key built into the app and the file's SHA-256 matches the signed manifest.

The `/usage` command is part of Claude Code, which connects to Anthropic with your own login. The island never sees that login.

## Report a vulnerability

Use **Report a vulnerability** in the Security tab of this repository. Please do not open a public issue.
