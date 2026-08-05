import { readFile } from "node:fs/promises";

const manifestPath = new URL("../browser-extension/manifest.json", import.meta.url);
const manifest = JSON.parse(await readFile(manifestPath, "utf8"));

if (manifest.manifest_version !== 3) throw new Error("manifest_version must be 3");
if (!manifest.permissions?.includes("cookies")) throw new Error("cookies permission is required");
if (!manifest.host_permissions?.some((entry) => entry.includes("furaffinity.net")))
  throw new Error("FurAffinity host permission is required");

// The popup has no URL field: both PostyFox environments must be granted up front so the one-click
// flow (and the Dev switch) never stops to ask for permission.
for (const origin of ["https://cp.postyfox.com/*", "https://dev.postyfox.com/*"])
  if (!manifest.host_permissions?.includes(origin))
    throw new Error(`${origin} must be declared in host_permissions`);

// popup.js reads the environment origins from its own table; keep the two in step.
const popup = await readFile(new URL("../browser-extension/popup.js", import.meta.url), "utf8");
for (const origin of ["https://cp.postyfox.com", "https://dev.postyfox.com"])
  if (!popup.includes(origin)) throw new Error(`popup.js must target ${origin}`);

console.log("PostyFox Connect manifest is valid");
