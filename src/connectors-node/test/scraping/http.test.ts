import { test } from "node:test";
import assert from "node:assert/strict";
import { CookieScraperSession, type FetchLike } from "../../src/scraping/http.js";

test("cookie scraper sends imported cookies and retains response cookies", async () => {
  const seenCookies: string[] = [];
  const fakeFetch: FetchLike = async (_input, init) => {
    const headers = new Headers(init?.headers);
    seenCookies.push(headers.get("cookie") ?? "");
    return new Response("ok", {
      status: 200,
      headers: { "set-cookie": "session=refreshed; Path=/; Secure" },
    });
  };

  const session = await CookieScraperSession.create(
    "https://www.furaffinity.net",
    "a=one; b=two",
    { fetch: fakeFetch },
  );

  await session.request("/controls/submissions");
  await session.request("/submit/");

  assert.match(seenCookies[0], /a=one/);
  assert.match(seenCookies[0], /b=two/);
  assert.match(seenCookies[1], /session=refreshed/);
});

test("cookie scraper rejects cross-origin URLs", async () => {
  const session = await CookieScraperSession.create(
    "https://www.furaffinity.net",
    "a=one",
    { fetch: async () => new Response("not reached") },
  );

  await assert.rejects(
    session.request("https://example.com/private"),
    /configured origin/,
  );
});

test("cookie scraper refuses cross-origin redirects", async () => {
  const session = await CookieScraperSession.create(
    "https://www.furaffinity.net",
    "a=one",
    {
      fetch: async () =>
        new Response(null, {
          status: 302,
          headers: { location: "http://127.0.0.1/internal" },
        }),
    },
  );

  await assert.rejects(
    session.request("/submit/"),
    /redirect must remain/,
  );
});

test("cookie scraper rejects cookie header injection", async () => {
  await assert.rejects(
    CookieScraperSession.create(
      "https://www.furaffinity.net",
      "a=one\r\nx-injected: yes",
    ),
    /line breaks/,
  );
});
