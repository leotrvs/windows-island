# Contributing

## Setup

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
git clone https://github.com/leotrvs/windows-island.git
cd windows-island
git config core.hooksPath .githooks
pwsh -File scripts/build.ps1
.\bin\WindowsIsland.exe
```

The hooks stop commits and pushes that contain secrets or personal data. To add your own terms, such as your name, list them one per line in `.git\info\leak-patterns.txt`. That file is never committed.

## Rules

- **Never read Claude credentials or call Anthropic's private endpoints.** Claude data comes only from the session status files Claude Code keeps and from the limits file Claude Usage Taskbar saves. Pull requests that change this will be declined.
- **No network access.** The island works entirely on your computer.
- **Add every user-visible text in both English and Portuguese** in `src\Strings.cs`.
- **Keep it light.** The island is always on screen: the render loop must stop when nothing moves, and nothing should allocate per frame without need.
- **Do not use real screenshots.** Generate documentation images from sample data:

  ```powershell
  pwsh -File scripts/render-images.ps1
  ```

  To look at one state with sample data, run `.\bin\WindowsIsland.exe --preview=MusicExpanded`. The states are `Idle`, `Minimal`, `MusicCompact`, `ClaudeCompact`, `MusicExpanded`, `ClaudeExpanded` and `Banner`. Add `--lang=en` or `--lang=pt` to force a language.

## Project layout

| Path | Purpose |
|---|---|
| `src\IslandWindow.xaml` | The island: shape, layout of every state and the glyphs |
| `src\IslandWindow.xaml.cs` | Which state to show, the spring animations and the live parts |
| `src\Spring.cs` | The damped spring behind every animation |
| `src\MediaService.cs` | What is playing, from the Windows media controls |
| `src\ClaudeService.cs` | Claude Code session status and usage limits |
| `src\Native.cs` | Overlay window style, fullscreen and maximized detection |
| `src\Tray.cs` | The tray icon and its menu |
| `src\Settings.cs` | Preferences, saved as JSON |
| `src\Strings.cs` | English and Portuguese texts |
| `scripts\` | Build, README images and leak checks |
