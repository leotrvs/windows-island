# Windows Island tab icon (browser extension)

A small Chrome and Edge extension. While a tab plays sound, it tells Windows Island the tab's title, site name and small icon, so web audio without artwork (a WhatsApp voice message, for example) shows that site's icon instead of a letter.

It only talks to the island on this same computer (`127.0.0.1`), only while a tab is playing, and sends nothing anywhere else.

## Set up

1. In Windows Island, Settings, System: turn on **Local API**.
2. In Chrome or Edge open `chrome://extensions` (or `edge://extensions`), turn on Developer mode, and choose **Load unpacked**, then pick this `extension` folder.
3. In a terminal run `island token` (see [docs/API.md](../docs/API.md)) and paste the token in the extension's options (click its icon). The default port is 47613.
4. Play something in a tab. The island's music view shows that site's icon.

Permissions: `tabs` (to read the playing tab's title and address), `favicon` (the browser's own copy of the site icon), `storage` (the token and port), and access to `127.0.0.1` only.
