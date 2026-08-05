"use strict";

/**
 * One click, start to finish: the popup asks PostyFox what can be connected, reads the matching
 * website cookies, and posts them straight back. There is no URL to type and no token to copy —
 * the extension presents the user's own PostyFox session, so the server already knows who is asking.
 * Everything site-specific (which cookies, which login page) comes from the server, so supporting a
 * new site needs no extension release.
 */

const extensionApi = globalThis.browser ?? globalThis.chrome;

// The only two deployments this talks to. Both are declared in host_permissions, so flipping the Dev
// switch never triggers a permission prompt.
const ENVIRONMENTS = { live: "https://cp.postyfox.com", dev: "https://dev.postyfox.com" };

const els = {
  devMode: document.querySelector("#dev-mode"),
  status: document.querySelector("#status"),
  targetRow: document.querySelector("#target-row"),
  target: document.querySelector("#target"),
  action: document.querySelector("#action"),
  token: document.querySelector("#pairing-token"),
  manualConnect: document.querySelector("#manual-connect"),
  cookieSummary: document.querySelector("#cookie-summary"),
  originSummary: document.querySelector("#origin-summary"),
};

/** Raised when PostyFox says (or implies) that there is no signed-in session. */
class SignedOutError extends Error {}

/** Detection results: the sites we could connect, plus whether PostyFox knows who we are. */
let sites = [];
let signedIn = false;
/** What the primary button does right now; set by render(), invoked by the click handler. */
let action = null;

document.addEventListener("DOMContentLoaded", () =>
  run(async () => {
    const saved = await extensionApi.storage.local.get("devMode");
    els.devMode.checked = Boolean(saved.devMode);
    await detect();
  }),
);

els.devMode.addEventListener("change", () =>
  run(async () => {
    await extensionApi.storage.local.set({ devMode: els.devMode.checked });
    await detect();
  }),
);

els.target.addEventListener("change", render);

// NB: no await before action(), so the user gesture is still live if the handler needs to request an
// optional permission — Chrome rejects permissions.request() once the gesture has been consumed.
els.action.addEventListener("click", () => {
  if (action) run(action);
});

els.manualConnect.addEventListener("click", () => run(connectWithToken));

function origin() {
  return els.devMode.checked ? ENVIRONMENTS.dev : ENVIRONMENTS.live;
}

// ----- detection -------------------------------------------------------------------------------

async function detect() {
  action = null;
  els.action.hidden = true;
  els.targetRow.hidden = true;
  setStatus("Checking…");

  let targets;
  try {
    // Authenticated: resolves the user's connectors as well as the cookie details.
    targets = (await api("GET", "/api/connectors/cookie-pairing/targets")) ?? [];
    signedIn = true;
  } catch (error) {
    if (!(error instanceof SignedOutError)) {
      setStatus(message(error), "error");
      return;
    }
    // Signed out of PostyFox: fall back to the public site list so we can still report on the
    // website session and drive the pairing-token route below.
    signedIn = false;
    try {
      targets = (await api("GET", "/api/connectors/cookie-pairing/sites")) ?? [];
    } catch (siteError) {
      setStatus(message(siteError), "error");
      return;
    }
  }

  const activeHost = await activeTabHost();
  sites = [];
  for (const target of targets) {
    const granted = await hasSiteAccess(target.siteUrl);
    const cookies = granted ? await readCookies(target) : {};
    sites.push({
      ...target,
      granted,
      hasSession: hasAllCookies(target, cookies),
      onActiveTab: sameSite(host(target.siteUrl), activeHost),
    });
  }

  // Whatever the user is most likely to have meant: the site they are looking at, then anything
  // already logged in, then the first on offer.
  const preferred =
    sites.find((s) => s.onActiveTab && s.hasSession) ??
    sites.find((s) => s.hasSession) ??
    sites.find((s) => s.onActiveTab) ??
    sites[0];

  els.target.replaceChildren(
    ...sites.map((site, index) => {
      const option = document.createElement("option");
      option.value = String(index);
      option.textContent =
        signedIn && !site.connectorId ? `${site.displayName} (new)` : site.displayName;
      return option;
    }),
  );
  if (preferred) els.target.value = String(sites.indexOf(preferred));
  els.targetRow.hidden = sites.length < 2;

  render();
}

function render() {
  const site = selected();
  els.originSummary.textContent = host(origin());
  els.cookieSummary.textContent = site
    ? site.cookieNames.map((name) => `“${name}”`).join(" and ")
    : "a site requires";

  if (!site) {
    setStatus("PostyFox has no cookie-connected sites to set up.");
    els.action.hidden = true;
    action = null;
    return;
  }

  const siteHost = host(site.siteUrl);

  if (!site.granted) {
    // A site PostyFox supports but this extension build has no static permission for.
    return offer(
      `Allow access to ${siteHost}`,
      `PostyFox Connect needs permission to read your ${site.displayName} session.`,
      () => grantSiteAccess(site),
    );
  }

  // Both logins are needed, and the user may be missing either. Name them all, and point the button
  // at the first — a second pass through the popup will ask for the other if it is still missing.
  const missing = [];
  if (!signedIn)
    missing.push({ what: "PostyFox", label: "Sign in to PostyFox", url: `${origin()}/` });
  if (!site.hasSession)
    missing.push({ what: siteHost, label: `Log in to ${siteHost}`, url: site.loginUrl });
  if (missing.length > 0) {
    const [first] = missing;
    return offer(
      first.label,
      `Log in to ${missing.map((m) => m.what).join(" and ")} first, then come back here.`,
      () => openTab(first.url),
    );
  }

  offer(
    `Connect ${site.displayName}`,
    site.connectorId
      ? `Ready to connect ${site.displayName}.`
      : `Ready to add ${site.displayName} to PostyFox and connect it.`,
    () => connect(site),
  );
}

function offer(label, status, handler) {
  setStatus(status);
  els.action.textContent = label;
  els.action.hidden = false;
  action = handler;
}

function selected() {
  return sites[Number(els.target.value)] ?? sites[0];
}

// ----- the one click ---------------------------------------------------------------------------

async function connect(site) {
  // Re-read rather than trusting the detection snapshot: the user may have logged in (or out) of the
  // site in another tab while the popup was open.
  const cookies = await readCookies(site);
  if (!hasAllCookies(site, cookies))
    throw new Error(`Log in to ${host(site.siteUrl)} and try again.`);

  const result = await api("POST", "/api/connectors/cookie-pairing/pair", {
    platform: site.platform,
    connectorId: site.connectorId,
    cookies,
  });
  succeed(`${result?.displayName ?? site.displayName} is connected to PostyFox.`);
}

/**
 * Fallback for a browser that cannot present a PostyFox session — a different profile, or a Safari
 * build where the session cookie does not reach the extension. The one-use token is the authorization
 * in place of the session, so this call is deliberately unauthenticated.
 */
async function connectWithToken() {
  const pairingToken = els.token.value.trim();
  if (!pairingToken) throw new Error("Paste the one-time pairing token from PostyFox.");
  const site = selected();
  if (!site) throw new Error("There is no site to connect.");

  const cookies = await readCookies(site);
  if (!hasAllCookies(site, cookies))
    throw new Error(`Log in to ${host(site.siteUrl)} and try again.`);

  const response = await fetch(`${origin()}/api/connectors/cookie-pairing/complete`, {
    method: "POST",
    credentials: "omit",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ pairingToken, cookies }),
  });
  if (!response.ok) throw new Error(await problem(response));

  els.token.value = "";
  succeed(`${site.displayName} is connected to PostyFox.`);
}

async function grantSiteAccess(site) {
  const granted = await extensionApi.permissions.request({
    origins: [`${originOf(site.siteUrl)}/*`],
  });
  if (!granted) throw new Error(`Access to ${host(site.siteUrl)} was not granted.`);
  await detect();
}

// ----- PostyFox ---------------------------------------------------------------------------------

async function api(method, path, body) {
  const headers = { accept: "application/json" };
  if (body !== undefined) headers["content-type"] = "application/json";

  let response;
  try {
    response = await fetch(`${origin()}${path}`, {
      method,
      // The extension's own session-bearing call. Chrome treats extension-initiated requests as
      // same-site, so the PostyFox session cookie rides along.
      credentials: "include",
      // A login redirect must not be followed — it would arrive as a perfectly valid HTML page.
      redirect: "manual",
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new Error(`Could not reach ${host(origin())}.`);
  }

  if (response.type === "opaqueredirect" || [0, 401, 403].includes(response.status))
    throw new SignedOutError();
  if (!response.ok) throw new Error(await problem(response));
  return response.status === 204 ? null : await response.json().catch(() => null);
}

async function problem(response) {
  const body = await response.json().catch(() => null);
  return body?.error ?? `PostyFox returned HTTP ${response.status}.`;
}

// ----- cookies + tabs ---------------------------------------------------------------------------

/** The site's required cookies, by name. Anything else the site has set is left behind. */
async function readCookies(site) {
  let found = await getAllCookies({ url: site.siteUrl });

  // Safari has shipped versions where URL-only queries return no rows unless a storeId is supplied.
  if (found.length === 0 && extensionApi.cookies.getAllCookieStores) {
    const stores = await extensionApi.cookies.getAllCookieStores().catch(() => []);
    const perStore = await Promise.all(
      stores.map((store) => getAllCookies({ url: site.siteUrl, storeId: store.id })),
    );
    found = perStore.flat();
  }

  const values = {};
  for (const cookie of found)
    if (site.cookieNames.includes(cookie.name) && !(cookie.name in values))
      values[cookie.name] = cookie.value;
  return values;
}

/** Never throws: a site the extension has no access to simply has no cookies as far as we know. */
async function getAllCookies(query) {
  try {
    return (await extensionApi.cookies.getAll(query)) ?? [];
  } catch {
    return [];
  }
}

function hasAllCookies(site, cookies) {
  return site.cookieNames.length > 0 && site.cookieNames.every((name) => Boolean(cookies[name]));
}

function hasSiteAccess(siteUrl) {
  return extensionApi.permissions
    .contains({ origins: [`${originOf(siteUrl)}/*`] })
    .catch(() => false);
}

async function activeTabHost() {
  // tab.url is only populated for tabs this extension has host access to — exactly the tabs whose
  // host could match a pairable site, so no "tabs" permission is needed.
  const [tab] = await extensionApi.tabs.query({ active: true, currentWindow: true }).catch(() => []);
  return tab?.url ? host(tab.url) : null;
}

async function openTab(url) {
  await extensionApi.tabs.create({ url });
  window.close();
}

function host(url) {
  try {
    return new URL(url).host;
  } catch {
    return url;
  }
}

function originOf(url) {
  return new URL(url).origin;
}

/** Treats www.example.com and example.com as the same site, which is all this comparison needs. */
function sameSite(a, b) {
  if (!a || !b) return false;
  const bare = (value) => value.replace(/^www\./, "");
  return bare(a) === bare(b);
}

// ----- plumbing ---------------------------------------------------------------------------------

async function run(operation) {
  setBusy(true);
  try {
    await operation();
  } catch (error) {
    setStatus(message(error), "error");
  } finally {
    setBusy(false);
  }
}

function succeed(text) {
  setStatus(text, "success");
  els.action.hidden = true;
  action = null;
}

function message(error) {
  if (error instanceof SignedOutError) return `Sign in to ${host(origin())} and try again.`;
  return error instanceof Error ? error.message : String(error);
}

function setBusy(busy) {
  els.action.disabled = busy;
  els.manualConnect.disabled = busy;
  els.devMode.disabled = busy;
}

function setStatus(text, kind = "") {
  els.status.textContent = text;
  els.status.className = kind;
}
