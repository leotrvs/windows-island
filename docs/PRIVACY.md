# Privacy policy

Windows Island is a free, open source app for Windows that runs on your computer. This page says what it does with your data. The code is public, so you can check every statement here: <https://github.com/leotrvs/windows-island>.

**In short: the app has no server, no account and no analytics. It does not collect, send or sell personal data.**

## What stays on your computer

- Your settings, in `%APPDATA%\WindowsIsland\settings.json`.
- If you use the Discord option: your Discord application's client secret and the access and refresh tokens Discord gives the app, encrypted for your Windows user in `%APPDATA%\WindowsIsland\discord.dat`. The "Forget the Discord data" button in Settings deletes them.
- Nothing else is stored. The app does not keep what it reads (music, chats, notifications, calls).

## What it reads, and why

The app reads local information from your computer only to show it on screen: what music is playing, whether Claude Code or Codex is working, the microphone, camera and screen sharing indicators Windows keeps, battery and volume, and, if you turn it on, whether you are muted or deafened in Discord. The full list, with where each piece is read from, is in [SECURITY.md](../SECURITY.md).

**Discord.** With the Discord option on, the app asks the Discord app on your computer, through its local RPC, only whether you are muted or deafened. It does not read messages, voice, servers, friends or any other Discord data. The permission you approve in Discord (`rpc` and `rpc.voice.read`) is broader than that, which is why the token is kept encrypted and can be deleted.

## What it sends over the internet

- To GitHub, to check for and download updates (the file is only installed if its digital signature is valid). GitHub can see your IP address, as in any download.
- To `discord.com/api/oauth2/token`, only with the Discord option on, to trade your approval for a token and to renew it. Your client secret goes to Discord and nowhere else.
- With the browser extension, to the site that is playing sound, to download its public `apple-touch-icon.png` (once per site).

Nothing is sent to the author of the app or to any analytics service.

## Your control

Every feature that reads something sensitive is off by default or can be turned off in Settings. Uninstalling the app removes its files and settings. To remove the Discord authorization, use "Forget the Discord data" and remove the application in Discord, under Settings, Authorized Apps.

## Contact

Questions or problems: open an issue at <https://github.com/leotrvs/windows-island/issues>.
