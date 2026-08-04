"use strict";

const extensionApi = globalThis.browser ?? globalThis.chrome;
const requiredCookieNames = ["a", "b"];
const furAffinityUrl = "https://www.furaffinity.net/";

const apiBaseInput = document.querySelector("#api-base");
const pairingTokenInput = document.querySelector("#pairing-token");
const inspectButton = document.querySelector("#inspect");
const connectButton = document.querySelector("#connect");
const status = document.querySelector("#status");
const diagnostics = document.querySelector("#diagnostics");
const cookieNames = document.querySelector("#cookie-names");
const cookieFlags = document.querySelector("#cookie-flags");

document.addEventListener("DOMContentLoaded", async () => {
  const saved = await extensionApi.storage.local.get("apiBase");
  apiBaseInput.value = saved.apiBase ?? "";
});

inspectButton.addEventListener("click", async () => {
  await run(async () => {
    const cookies = await readFurAffinityCookies();
    showDiagnostics(cookies);
    setStatus(
      hasRequiredCookies(cookies)
        ? "FurAffinity session cookies are available."
        : "The required FurAffinity cookies were not found. Log in and try again.",
      hasRequiredCookies(cookies) ? "success" : "error",
    );
  });
});

connectButton.addEventListener("click", async () => {
  await run(async () => {
    const apiBase = normalizeApiBase(apiBaseInput.value);
    const pairingToken = pairingTokenInput.value.trim();
    if (!pairingToken) throw new Error("Enter the one-time pairing token from PostyFox.");

    // Chrome requires optional permission prompts to remain directly tied to the button gesture.
    await ensurePostyFoxPermission(apiBase);
    const cookies = await readFurAffinityCookies();
    showDiagnostics(cookies);
    if (!hasRequiredCookies(cookies))
      throw new Error("The required FurAffinity cookies were not found. Log in and try again.");

    const values = Object.fromEntries(
      cookies
        .filter((cookie) => requiredCookieNames.includes(cookie.name))
        .map((cookie) => [cookie.name, cookie.value]),
    );

    const response = await fetch(`${apiBase}/api/connectors/cookie-pairing/complete`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ pairingToken, cookies: values }),
    });
    if (!response.ok) {
      const problem = await response.json().catch(() => null);
      throw new Error(problem?.error ?? `PostyFox rejected the pairing (HTTP ${response.status}).`);
    }

    await extensionApi.storage.local.set({ apiBase });
    pairingTokenInput.value = "";
    setStatus("FurAffinity is connected to PostyFox.", "success");
  });
});

async function readFurAffinityCookies() {
  let cookies = await extensionApi.cookies.getAll({ url: furAffinityUrl });

  // Safari has shipped versions where URL-only queries return no rows unless a storeId is supplied.
  if (cookies.length === 0 && extensionApi.cookies.getAllCookieStores) {
    const stores = await extensionApi.cookies.getAllCookieStores();
    const perStore = await Promise.all(
      stores.map((store) =>
        extensionApi.cookies.getAll({ url: furAffinityUrl, storeId: store.id }),
      ),
    );
    cookies = perStore.flat();
  }

  const unique = new Map();
  for (const cookie of cookies)
    if (requiredCookieNames.includes(cookie.name) && !unique.has(cookie.name))
      unique.set(cookie.name, cookie);
  return [...unique.values()];
}

async function ensurePostyFoxPermission(apiBase) {
  const url = new URL(apiBase);
  const originPattern = `${url.origin}/*`;
  const granted = await extensionApi.permissions.request({ origins: [originPattern] });
  if (!granted) throw new Error("PostyFox site access was not granted.");
}

function normalizeApiBase(value) {
  const url = new URL(value.trim());
  if (url.protocol !== "https:" && !isLocalDevelopment(url))
    throw new Error("PostyFox must use HTTPS (localhost is allowed for development).");
  return url.origin;
}

function isLocalDevelopment(url) {
  return url.protocol === "http:" && ["localhost", "127.0.0.1"].includes(url.hostname);
}

function hasRequiredCookies(cookies) {
  return requiredCookieNames.every((name) => cookies.some((cookie) => cookie.name === name));
}

function showDiagnostics(cookies) {
  diagnostics.hidden = false;
  cookieNames.textContent = cookies.map((cookie) => cookie.name).join(", ") || "none";
  cookieFlags.textContent =
    cookies.map((cookie) => `${cookie.name}: HttpOnly=${Boolean(cookie.httpOnly)}`).join(", ") || "none";
}

async function run(operation) {
  setBusy(true);
  setStatus("Working…");
  try {
    await operation();
  } catch (error) {
    setStatus(error instanceof Error ? error.message : String(error), "error");
  } finally {
    setBusy(false);
  }
}

function setBusy(busy) {
  inspectButton.disabled = busy;
  connectButton.disabled = busy;
}

function setStatus(message, kind = "") {
  status.textContent = message;
  status.className = kind;
}
