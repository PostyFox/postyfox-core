import { test } from "node:test";
import assert from "node:assert/strict";
import { DEFAULT_USER_AGENT, fetchWithUserAgent } from "../src/user-agent.js";

function captureFetch(t: { mock: { method: typeof test.mock.method } }): Headers[] {
  const seen: Headers[] = [];
  t.mock.method(globalThis, "fetch", async (_input: unknown, init?: RequestInit) => {
    seen.push(new Headers(init?.headers));
    return new Response("{}");
  });
  return seen;
}

test("fetchWithUserAgent adds the default User-Agent and keeps other headers", async (t) => {
  const seen = captureFetch(t);
  await fetchWithUserAgent("https://example.com", { headers: { accept: "application/json" } });
  assert.equal(seen[0].get("user-agent"), DEFAULT_USER_AGENT);
  assert.equal(seen[0].get("accept"), "application/json");
});

test("fetchWithUserAgent keeps a caller-supplied User-Agent", async (t) => {
  const seen = captureFetch(t);
  await fetchWithUserAgent("https://example.com", { headers: { "User-Agent": "Custom/1.0" } });
  assert.equal(seen[0].get("user-agent"), "Custom/1.0");
});

test("fetchWithUserAgent carries a Request's own headers", async (t) => {
  const seen = captureFetch(t);
  await fetchWithUserAgent(new Request("https://example.com", { headers: { authorization: "Bearer x" } }));
  assert.equal(seen[0].get("authorization"), "Bearer x");
  assert.equal(seen[0].get("user-agent"), DEFAULT_USER_AGENT);
});
