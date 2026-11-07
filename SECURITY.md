# Security

**English** | [Português (Brasil)](docs/SECURITY.pt-BR.md)

## What the app touches

| Item | Access | Why |
|---|---|---|
| Windows media controls | Reads the title, artist, artwork, position and playback state of the current player. Sends play, pause, next, previous and seek when you click them | Shows and controls music |
| `~/.claude/sessions` | Reads the kind, process ID, status, name and status time of each session, every second | Shows whether Claude Code is working or waiting for you. Nothing is stored. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar\usage.json` | Reads it when it changes | Shows the 5-hour and weekly limits that Claude Usage Taskbar saved |
| `%APPDATA%\WindowsIsland\settings.json` | Reads and writes | Stores preferences |
| Windows Run key | Writes only when you turn on **Start with Windows** | Opens the app when you sign in |

## What the app never does

- Read `~/.claude/.credentials.json` or any other Claude login, token or cookie
- Call Anthropic's APIs or connect to the internet at all
- Store prompts, responses, session names or project paths
- Read the content of your notifications or other windows
- Send analytics or telemetry
- Require administrator rights

## Report a vulnerability

Use **Report a vulnerability** in the Security tab of this repository. Please do not open a public issue.
