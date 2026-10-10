import { test } from "node:test";
import assert from "node:assert/strict";
import { buildRichText, stripMarkdown } from "../src/connectors/bluesky-richtext.js";

/** Agent whose handle lookups resolve only the given handles. */
function resolvingAgent(known: Record<string, string> = {}) {
  return {
    com: {
      atproto: {
        identity: {
          async resolveHandle({ handle }: { handle: string }) {
            const did = known[handle];
            if (!did) throw new Error("Unable to resolve handle");
            return { data: { did } };
          },
        },
      },
    },
  };
}

/** Returns each facet's covered text and its single feature, for readable assertions. */
function spans(text: string, facets: Awaited<ReturnType<typeof buildRichText>>["facets"]) {
  const bytes = Buffer.from(text, "utf8");
  return (facets ?? []).map((f) => ({
    text: bytes.subarray(f.index.byteStart, f.index.byteEnd).toString("utf8"),
    feature: f.features[0],
  }));
}

test("markdown link becomes a link facet over its display text", async () => {
  const { text, facets } = await buildRichText(
    "See [my site](https://example.com/a) now",
    resolvingAgent(),
  );
  assert.equal(text, "See my site now");
  assert.deepEqual(spans(text, facets), [
    {
      text: "my site",
      feature: { $type: "app.bsky.richtext.facet#link", uri: "https://example.com/a" },
    },
  ]);
});

test("advertising strapline renders as one linked word", async () => {
  const { text, facets } = await buildRichText(
    "Body\n\nSent using [PostyFox](https://postyfox.com)",
    resolvingAgent(),
  );
  assert.equal(text, "Body\n\nSent using PostyFox");
  assert.deepEqual(spans(text, facets), [
    {
      text: "PostyFox",
      feature: { $type: "app.bsky.richtext.facet#link", uri: "https://postyfox.com" },
    },
  ]);
});

test("link url keeps balanced parens and bold markers are stripped", async () => {
  const { text, facets } = await buildRichText(
    "**Read** [this](https://en.wikipedia.org/wiki/Fox_(disambiguation))",
    resolvingAgent(),
  );
  assert.equal(text, "Read this");
  assert.equal(
    (spans(text, facets)[0].feature as { uri: string }).uri,
    "https://en.wikipedia.org/wiki/Fox_(disambiguation)",
  );
});

test("bare urls are linked after any character and lose trailing punctuation", async () => {
  const body =
    'a <https://a.example/x> b "https://b.example" c Link:https://c.example... d **https://d.example**';
  const { text, facets } = await buildRichText(body, resolvingAgent());
  assert.deepEqual(
    spans(text, facets).map((s) => [s.text, (s.feature as { uri: string }).uri]),
    [
      ["https://a.example/x", "https://a.example/x"],
      ["https://b.example", "https://b.example"],
      ["https://c.example", "https://c.example"],
      ["https://d.example", "https://d.example"],
    ],
  );
});

test("facet offsets are utf-8 bytes when the text holds emoji", async () => {
  const { text, facets } = await buildRichText("🦊🦊 [fox](https://fox.example) #art", resolvingAgent());
  assert.deepEqual(
    spans(text, facets).map((s) => s.text),
    ["fox", "#art"],
  );
});

test("resolved mentions are kept and unresolved ones dropped", async () => {
  const { text, facets } = await buildRichText(
    "hi @alice.bsky.social and @nobody.bsky.social https://x.example",
    resolvingAgent({ "alice.bsky.social": "did:plc:alice" }),
  );
  assert.deepEqual(
    spans(text, facets).map((s) => s.text),
    ["@alice.bsky.social", "https://x.example"],
  );
});

test("links survive when the agent cannot resolve mentions at all", async () => {
  const { text, facets } = await buildRichText("@alice.bsky.social [x](https://x.example)", {});
  assert.deepEqual(
    spans(text, facets).map((s) => s.text),
    ["x"],
  );
});

test("plain text has no facets", async () => {
  assert.deepEqual(await buildRichText("hello world", resolvingAgent()), {
    text: "hello world",
    facets: undefined,
  });
});

test("stripMarkdown leaves non-http link syntax alone", () => {
  assert.equal(stripMarkdown("[a](ftp://x) [b](relative)").text, "[a](ftp://x) [b](relative)");
});
