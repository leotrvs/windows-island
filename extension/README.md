# Windows Island tab icon (browser extension)

A small Chrome and Edge extension. While a tab plays sound, it tells Windows Island the tab's title, site name and small icon, so web audio without artwork (a WhatsApp voice message, for example) shows that site's icon instead of a letter.

It only talks to the island on this same computer (`127.0.0.1`), only while a tab is playing, and sends nothing anywhere else.

## Set up

1. In Windows Island, Settings, System: turn on **Local API**, then click **Chrome** or **Edge** next to *Install browser extension*. The app unpacks the extension (with its token, so nothing to paste) and opens the browser's extensions page. The folder path is already on your clipboard.
2. In the browser, turn on Developer mode, choose **Load unpacked** and paste the folder (Ctrl+V).
3. Play something in a tab. The island's music view shows that site's icon.

By hand instead: load this `extension` folder, run `island token` (see [docs/API.md](../docs/API.md)) and paste the token in the extension's options. The default port is 47613.

Permissions: `tabs` (to read the playing tab's title and address), `favicon` (the browser's own copy of the site icon), `storage` (the token and port), and access to `127.0.0.1` only.
