import type { Connector } from "../types.js";
import { BlueskyConnector } from "./bluesky.js";
import { TumblrConnector } from "./tumblr.js";

/** Registry of connectors keyed by lower-cased platform name. */
export type ConnectorRegistry = Map<string, Connector>;

export function createDefaultRegistry(): ConnectorRegistry {
  const registry: ConnectorRegistry = new Map();
  registry.set("bluesky", new BlueskyConnector());
  registry.set("tumblr", new TumblrConnector());
  return registry;
}

/** Case-insensitive lookup. Returns undefined for unknown platforms. */
export function resolveConnector(
  registry: ConnectorRegistry,
  platform: string,
): Connector | undefined {
  return registry.get(platform.toLowerCase());
}
