import { mediaStoreFromEnv, type MediaStore } from "../media-store.js";
import type { Connector } from "../types.js";
import { BlueskyConnector } from "./bluesky.js";
import { TumblrConnector } from "./tumblr.js";

/** Registry of connectors keyed by lower-cased platform name. */
export type ConnectorRegistry = Map<string, Connector>;

export function createDefaultRegistry(
  mediaStore: MediaStore = mediaStoreFromEnv(),
): ConnectorRegistry {
  const registry: ConnectorRegistry = new Map();
  registry.set("bluesky", new BlueskyConnector(undefined, mediaStore));
  registry.set("tumblr", new TumblrConnector(undefined, mediaStore));
  return registry;
}

/** Case-insensitive lookup. Returns undefined for unknown platforms. */
export function resolveConnector(
  registry: ConnectorRegistry,
  platform: string,
): Connector | undefined {
  return registry.get(platform.toLowerCase());
}
