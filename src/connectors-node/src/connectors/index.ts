import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import type { Connector } from "../types.js";
import { BlueskyConnector } from "./bluesky.js";
import { MegalodonConnector } from "./megalodon.js";
import { TumblrConnector } from "./tumblr.js";

/** Registry of connectors keyed by lower-cased platform name. */
export type ConnectorRegistry = Map<string, Connector>;

export function createDefaultRegistry(
  mediaStore: MediaStore = mediaStoreFromEnv(),
): ConnectorRegistry {
  const registry: ConnectorRegistry = new Map();
  registry.set("bluesky", new BlueskyConnector(undefined, mediaStore));
  registry.set("tumblr", new TumblrConnector(undefined, mediaStore));

  // Fediverse platforms served by megalodon. The SNS is auto-detected from the instance's nodeinfo
  // at connect time; the value below is only the fallback (and the driver a few forks share, e.g.
  // Akkoma→pleroma, Hometown→mastodon, Iceshrimp→firefish).
  for (const [platform, sns] of [
    ["mastodon", "mastodon"],
    ["pleroma", "pleroma"],
    ["akkoma", "pleroma"],
    ["friendica", "friendica"],
    ["firefish", "firefish"],
    ["iceshrimp", "firefish"],
    ["gotosocial", "gotosocial"],
    ["hometown", "mastodon"],
    ["pixelfed", "pixelfed"],
  ] as const) {
    registry.set(platform, new MegalodonConnector(sns, mediaStore));
  }
  return registry;
}

/** Case-insensitive lookup. Returns undefined for unknown platforms. */
export function resolveConnector(
  registry: ConnectorRegistry,
  platform: string,
): Connector | undefined {
  return registry.get(platform.toLowerCase());
}
