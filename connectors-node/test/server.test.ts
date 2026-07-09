import { test } from "node:test";
import assert from "node:assert/strict";
import { buildServer } from "../src/server.js";
import type { ConnectorRegistry } from "../src/connectors/index.js";
import type { Connector } from "../src/types.js";

const stubConnector: Connector = {
  async isAuthenticated() {
    return { isAuthenticated: true };
  },
  async listTargets() {
    return { targets: [{ id: "a", name: "A" }] };
  },
  async deliver() {
    return { success: true, externalId: "1", externalUrl: "http://x" };
  },
};

function registryWith(platform: string): ConnectorRegistry {
  const r: ConnectorRegistry = new Map();
  r.set(platform, stubConnector);
  return r;
}

test("GET /health is open and returns ok", async () => {
  const app = buildServer({ internalToken: "secret", registry: registryWith("bluesky") });
  const res = await app.inject({ method: "GET", url: "/health" });
  assert.equal(res.statusCode, 200);
  assert.deepEqual(res.json(), { status: "ok" });
  await app.close();
});

test("401 when token missing", async () => {
  const app = buildServer({ internalToken: "secret", registry: registryWith("bluesky") });
  const res = await app.inject({
    method: "POST",
    url: "/connectors/bluesky/is-authenticated",
    payload: { connectorId: "c", userId: "u", configJson: "{}", secretJson: null, targetId: null },
  });
  assert.equal(res.statusCode, 401);
  await app.close();
});

test("401 when token wrong", async () => {
  const app = buildServer({ internalToken: "secret", registry: registryWith("bluesky") });
  const res = await app.inject({
    method: "POST",
    url: "/connectors/bluesky/is-authenticated",
    headers: { "x-internal-token": "nope" },
    payload: { connectorId: "c", userId: "u", configJson: "{}", secretJson: null, targetId: null },
  });
  assert.equal(res.statusCode, 401);
  await app.close();
});

test("passes when token correct", async () => {
  const app = buildServer({ internalToken: "secret", registry: registryWith("bluesky") });
  const res = await app.inject({
    method: "POST",
    url: "/connectors/bluesky/is-authenticated",
    headers: { "x-internal-token": "secret" },
    payload: { connectorId: "c", userId: "u", configJson: "{}", secretJson: null, targetId: null },
  });
  assert.equal(res.statusCode, 200);
  assert.deepEqual(res.json(), { isAuthenticated: true });
  await app.close();
});

test("auth disabled when no token configured (dev)", async () => {
  const app = buildServer({ registry: registryWith("bluesky") });
  const res = await app.inject({
    method: "POST",
    url: "/connectors/bluesky/list-targets",
    payload: { connectorId: "c", userId: "u", configJson: "{}", secretJson: null, targetId: null },
  });
  assert.equal(res.statusCode, 200);
  await app.close();
});

test("unknown platform returns 404", async () => {
  const app = buildServer({ internalToken: "secret", registry: registryWith("bluesky") });
  const res = await app.inject({
    method: "POST",
    url: "/connectors/myspace/is-authenticated",
    headers: { "x-internal-token": "secret" },
    payload: { connectorId: "c", userId: "u", configJson: "{}", secretJson: null, targetId: null },
  });
  assert.equal(res.statusCode, 404);
  assert.deepEqual(res.json(), { error: "unknown platform" });
  await app.close();
});
