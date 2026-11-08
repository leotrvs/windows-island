# Local API

Scripts and apps on your PC can show their own live activities and alerts in the island, and a browser extension can tell it which tab is playing. The API is **off by default**: turn it on in Settings.

- Address: `http://127.0.0.1:47613` (loopback only, never the network). The port is `ApiPort` in `settings.json` (1024-65535).
- Every request needs `Authorization: Bearer <token>`.
- Requests with a body need `Content-Type: application/json` (optionally `; charset=utf-8`) and a `Content-Length`. The body is UTF-8 JSON, at most 8 KB (48 KB for `/v1/media-hint`).
- One request per connection. Answers are JSON: `{"ok":true}` or `{"ok":false,"error":"<code>"}`.

## The token

The token is created the first time the API is turned on, and stored in `%APPDATA%\WindowsIsland\api-token.txt`, readable only by your Windows user. To see it:

```
"%LOCALAPPDATA%\Programs\WindowsIsland\island.cmd" token
```

Regenerating it in Settings refuses the old one at once. Treat it like a password: anyone with it can put text on your island.

## Endpoints

### `GET /v1/ping`

`{"ok":true,"app":"WindowsIsland","version":"<app version>"}`. Also needs the token.

### `POST /v1/activity`

Creates or updates a live activity.

| Field | Type | Notes |
| --- | --- | --- |
| `id` | string, required | 1-64 characters: `A-Z a-z 0-9 . _ -` |
| `title` | string | Required when creating. Up to 60 characters |
| `text` | string | Up to 120 characters |
| `progress` | number | 0-100. Leave it out for no progress bar |
| `icon` | string | A name from the list below; anything else shows as `info` |
| `state` | string | `running` (default), `done` or `error` |
| `app` | string | Up to 24 characters. Only used when the island cannot tell which program sent the request |

Fields left out keep their previous value. At most 8 live activities: a 9th new one is refused with `409`. An activity ends 10 minutes after its last update, or about 6 seconds after it is marked `done` or `error`.

### `POST /v1/activity/end`

`{"id":"..."}`. Ends it now. Ending one that is already gone is not an error.

### `POST /v1/alert`

| Field | Type | Notes |
| --- | --- | --- |
| `title` | string, required | Up to 60 characters |
| `text` | string | Up to 120 characters |
| `icon` | string | As above |
| `seconds` | number | 2-15, default 5 |
| `app` | string | As above |

Alerts are limited to a few per second.

### `POST /v1/media-hint`

For a browser extension: which tab is playing.

| Field | Type | Notes |
| --- | --- | --- |
| `title` | string, required | The tab's title, up to 60 characters |
| `host` | string | Such as `www.youtube.com` |
| `favicon` | string | Base64 PNG, JPEG or ICO (a `data:` URL also works), at most 32 KB and 128 x 128 pixels |

### `POST /v1/permission`

Used by the island's own hook for Claude Code (see **Answer permissions on the island** in Settings). It shows Allow and Deny on the island and answers on the same connection once you click, the time runs out, or the caller hangs up.

| Field | Type | Notes |
| --- | --- | --- |
| `tool` | string, required | The tool's name, up to 100 characters |
| `summary` | string | What it wants to do (the command, the file, the query), up to 300 characters |
| `session` | string | Up to 64 characters |
| `wait` | number | 5-110 seconds to wait, default 60 |

The answer is `{"decision":"allow"}`, `{"decision":"deny"}` or `{"decision":"none"}`. `none` means nothing was decided, including when the option is off, four requests are already waiting, or nobody clicked in time.

### Icons

`info`, `check`, `error`, `warning`, `build`, `download`, `upload`, `sync`, `mail`, `music`, `code`, `terminal`, `bell`, `heart`, `star`, `clock`, `calendar`, `lock`, `shield`, `wifi`, `bluetooth`, `battery`, `camera`, `mic`, `cloud`, `globe`, `chat`, `play`.

### Errors

| Status | When |
| --- | --- |
| 400 | Bad request, wrong `Host`, JSON that does not parse, a field of the wrong type (`invalid_field`), a missing title (`title_required`), a bad favicon (`invalid_favicon`) |
| 401 | Missing or wrong token |
| 403 | An `Origin` header from a web page |
| 404 / 405 | Unknown path, or the wrong method (`OPTIONS` is never accepted) |
| 409 | Already 8 live activities |
| 411 / 413 / 415 | No `Content-Length`, body too large, not `application/json` |
| 429 | More than about 20 requests a second, or too many alerts |

## Who sent it

The island shows which program sent each activity and alert. It finds the program that owns the connection, so the `app` field cannot pretend to be another program; it is only used when that lookup fails. Requests from `island.cmd` are shown under the program that ran it, such as PowerShell.

## Command line

The installer puts `island.cmd` beside the app, in `%LOCALAPPDATA%\Programs\WindowsIsland`. It is not added to `PATH`; call it by its full path, or add that folder yourself.

```
island show <id> [--title T] [--text T] [--progress 0-100] [--icon NAME] [--state running|done|error]
island done <id> [--text T]
island end <id>
island alert <title> [--text T] [--icon NAME] [--seconds 2-15]
island token
island help
```

It exits with 0 when it worked and 1 otherwise, and prints errors to stderr. It only sends the token to the island itself: if another program holds the port, it stops.

```bat
set ISLAND="%LOCALAPPDATA%\Programs\WindowsIsland\island.cmd"
%ISLAND% show build --title "Building" --progress 0 --icon build
%ISLAND% show build --progress 60 --text "Linking"
%ISLAND% done build --text "Finished in 42 s"
%ISLAND% alert "Backup finished" --icon check
```

## Examples

curl (Windows 10 and later include `curl.exe`):

```bat
set /p ISLAND_TOKEN=<"%APPDATA%\WindowsIsland\api-token.txt"
curl -H "Authorization: Bearer %ISLAND_TOKEN%" -H "Content-Type: application/json" ^
     -d "{\"id\":\"build\",\"title\":\"Building\",\"progress\":40,\"icon\":\"build\"}" ^
     http://127.0.0.1:47613/v1/activity
```

PowerShell:

```powershell
$token = (Get-Content "$env:APPDATA\WindowsIsland\api-token.txt" -Raw).Trim()
$headers = @{ Authorization = "Bearer $token" }
$body = @{ id = 'build'; title = 'Building'; progress = 40; icon = 'build' } | ConvertTo-Json
Invoke-RestMethod http://127.0.0.1:47613/v1/activity -Method Post -Headers $headers `
    -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
```

Windows PowerShell 5.1 sends a text body as Latin-1, so send bytes as above, or accented letters are refused as invalid JSON.

## Security notes

- The server listens on `127.0.0.1` only, with an exclusive bind, so no other program can share the port while it runs.
- Any web page can try to reach `127.0.0.1`. Pages are refused because the API needs a token they do not have, a JSON content type (which makes the browser ask first, and that `OPTIONS` request is refused), a `Host` of `127.0.0.1:<port>` or `localhost:<port>` (against DNS rebinding), and no `Origin` other than `chrome-extension://` or `moz-extension://`. No CORS headers are ever sent.
- The token is compared in constant time; wrong tokens are answered slowly and counted. Nothing is logged.
- Requests are small and strict: 8 KB of headers, a short read timeout, 16 connections at a time, no chunked bodies, JSON nested at most 4 levels deep, no repeated fields.
- Text is cleaned (control, formatting, direction-changing and private-use characters removed, lengths capped) and shown as plain text. Icons come only from the list above. Favicons are checked for type and size before they are decoded.
- Programs running as your Windows user can read the token file, as they can read the rest of your files. The token keeps out web pages and other users, not software you already run.
