import { readFile } from "node:fs/promises";

const manifestPath = new URL("../browser-extension/manifest.json", import.meta.url);
const manifest = JSON.parse(await readFile(manifestPath, "utf8"));

if (manifest.manifest_version !== 3) throw new Error("manifest_version must be 3");
if (!manifest.permissions?.includes("cookies")) throw new Error("cookies permission is required");
if (!manifest.host_permissions?.some((entry) => entry.includes("furaffinity.net")))
  throw new Error("FurAffinity host permission is required");

console.log("PostyFox Connect manifest is valid");
