// Tells Windows Island which tab is playing sound. It sends only the tab's title, its site name and its small
// icon, to the island on this same computer (127.0.0.1), and only while a tab is audible and a token is set.
// Nothing is sent to any other address and nothing is stored besides the island's port and token.

const DEFAULT_PORT = 47613;
let lastSent = "";

// config.json is written next to the extension by Windows Island's Settings and is the first choice, so no
// token has to be pasted. What was typed into the options page is the fallback.
async function settings() {
  try {
    const config = await (await fetch(chrome.runtime.getURL("config.json"))).json();
    if (config && config.token) return { port: Number(config.port) || DEFAULT_PORT, token: String(config.token).trim() };
  } catch (e) { /* no config file: use the stored values */ }
  const { port, token } = await chrome.storage.local.get(["port", "token"]);
  return { port: Number(port) || DEFAULT_PORT, token: (token || "").trim() };
}

// The browser's own cached copy of the site's icon, as a small PNG in base64.
async function faviconOf(pageUrl) {
  try {
    const url = new URL(chrome.runtime.getURL("/_favicon/"));
    url.searchParams.set("pageUrl", pageUrl);
    url.searchParams.set("size", "64");
    const blob = await (await fetch(url.toString())).blob();
    if (blob.size === 0 || blob.size > 24 * 1024) return null;
    const bytes = new Uint8Array(await blob.arrayBuffer());
    let binary = "";
    for (const b of bytes) binary += String.fromCharCode(b);
    return btoa(binary);
  } catch (e) {
    return null;
  }
}

async function report(tab) {
  const { port, token } = await settings();
  if (!token || !tab || !tab.audible || !tab.url || !/^https?:/.test(tab.url)) return;
  const host = new URL(tab.url).hostname;
  const key = tab.id + "|" + tab.title;
  if (key === lastSent) return;
  lastSent = key;
  const favicon = await faviconOf(tab.url);
  const body = { title: (tab.title || "").slice(0, 60), host: host.slice(0, 60) };
  if (favicon) body.favicon = favicon;
  try {
    await fetch("http://127.0.0.1:" + port + "/v1/media-hint", {
      method: "POST",
      headers: { "Content-Type": "application/json", Authorization: "Bearer " + token },
      body: JSON.stringify(body),
    });
  } catch (e) {
    lastSent = ""; // The island is not running or the API is off: try again on the next change
  }
}

chrome.tabs.onUpdated.addListener((id, change, tab) => {
  if (change.audible === true || (tab.audible && (change.title || change.url))) report(tab);
});

chrome.tabs.onActivated.addListener(async ({ tabId }) => {
  try { report(await chrome.tabs.get(tabId)); } catch (e) { /* the tab closed */ }
});
