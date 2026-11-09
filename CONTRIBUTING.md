# Contributing

## Setup

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/leotrvs/windows-island.git
cd windows-island
git config core.hooksPath .githooks
pwsh -File scripts/build.ps1
.\bin\WindowsIsland.exe --portable
```

Run the tests with `dotnet test tests`. They cover the logic that has no window (the chat transcript reader, the limit alert rules and the Claude settings editor), so a change there should come with a test.

`--portable` runs it without the install prompt. The hooks stop commits and pushes that contain secrets or personal data. To add your own terms, such as your name, list them one per line in `.git\info\leak-patterns.txt`. That file is never committed.

## Rules

- **Never read Claude credentials or call Anthropic's private endpoints.** Claude data comes only from the session status files Claude Code keeps, Claude Code's own `/usage` command and the limits file Claude Usage Taskbar saves. Pull requests that change this will be declined.
- **No network access beyond updates.** The only connections are the signed update check against this repository's releases (it can be turned off) and, with the browser extension, one small icon download per site, described in SECURITY.md.
- **Privacy-sensitive features are opt-in.** Reading notifications stays off until the user turns it on.
- **Add every user-visible text in both English and Portuguese** in `src\Strings.cs`.
- **Keep it light.** The island is always on screen: the render loop must stop when nothing moves, and nothing should allocate per frame without need.
- **Do not use real screenshots.** Generate documentation images from sample data:

  ```powershell
  pwsh -File scripts/render-images.ps1
  ```

  The animated GIF at the top of the README is made the same way, with `pwsh -File scripts/render-gif.ps1` (it needs Python 3 with Pillow).
  The small animation of each state in the README tables is made with `pwsh -File scripts/render-clips.ps1` (`WindowsIsland.exe --clips <folder>` writes the frames, `scripts/assemble-clips.py` joins them; same Python and Pillow).

  To look at one state with sample data, run `.\bin\WindowsIsland.exe --preview=MusicExpanded`. The states are `Idle`, `Minimal`, `MusicCompact`, `ClaudeCompact`, `MusicExpanded`, `ClaudeExpanded` and `Banner`. Add `--lang=en` or `--lang=pt` to force a language.

## Project layout

| Path | Purpose |
|---|---|
| `src\IslandWindow.xaml` | The island: shape, layout of every state and the glyphs |
| `src\IslandWindow.xaml.cs` | Which state to show, the spring animations and the live parts |
| `src\Spring.cs` | The damped spring behind every animation |
| `src\MediaService.cs` | What is playing, from the Windows media controls |
| `src\ClaudeService.cs` | Claude Code session status, alerts and when to read the limits |
| `src\UsageProbe.cs` | Reads the limits from Claude Code's `/usage` command |
| `src\CodexService.cs` | Codex activity and limits, from the end of its newest session file |
| `src\Displays.cs` | The monitors and their bounds, for placing each island |
| `src\SettingsWindow.xaml` | The settings window |
| `src\ClaudeSpark.cs` | The Claude spark and its thinking animation, from `src\Assets\thinking.png` |
| `src\NotificationService.cs` | New Windows notifications, read-only, through the SQLite that ships with Windows |
| `src\Installer.cs` | Install, shortcuts and uninstall |
| `src\Updater.cs` | Signed updates from GitHub releases |
| `src\UpdateKey.cs` | The public half of the update signing key |
| `src\Native.cs` | Overlay window style, fullscreen and maximized detection |
| `src\Extras.cs` | The timer, the microphone and camera dots (registry and audio sessions), and battery alerts |
| `src\Tray.cs` | The tray icon and its menu |
| `src\Settings.cs` | Preferences, saved as JSON |
| `src\Strings.cs` | English and Portuguese texts |
| `tests\` | Unit tests (xUnit) for the pure logic, compiled from `src` |
| `scripts\` | Build, README images and leak checks |

Planned work is in [docs/ROADMAP.md](docs/ROADMAP.md). Maintainers should see [docs/RELEASING.md](docs/RELEASING.md).
