const tokenInput = document.getElementById("token");
const portInput = document.getElementById("port");
const status = document.getElementById("status");

chrome.storage.local.get(["port", "token"]).then(async ({ port, token }) => {
  if (port) portInput.value = port;
  if (token) tokenInput.value = token;
  try {
    const config = await (await fetch(chrome.runtime.getURL("config.json"))).json();
    if (config && config.token) {
      portInput.value = config.port || 47613;
      tokenInput.value = config.token;
      status.textContent = "Set up by Windows Island. Nothing to paste.";
    }
  } catch (e) { /* no config file: the fields above are the manual way */ }
});

document.getElementById("save").addEventListener("click", async () => {
  const token = tokenInput.value.trim();
  const port = Number(portInput.value) || 47613;
  await chrome.storage.local.set({ token, port });
  status.textContent = "Checking…";
  try {
    const response = await fetch("http://127.0.0.1:" + port + "/v1/ping", { headers: { Authorization: "Bearer " + token } });
    status.textContent = response.ok ? "Connected to Windows Island." : "The island answered, but did not accept the token.";
  } catch (e) {
    status.textContent = "Saved. The island is not reachable: is it running with the local API on?";
  }
});
