import { AppBskyRichtextFacet, AtpAgent, RichText } from "@atproto/api";

type Facet = AppBskyRichtextFacet.Main;

interface LinkSpan {
  /** UTF-16 offsets into the stripped text. */
  start: number;
  end: number;
  uri: string;
}

// `[text](https://url)`, allowing one level of balanced parens in the url (Wikipedia-style), or
// `**bold**`. Only http(s) targets become links; anything else is left as literal text.
const MARKDOWN_REGEX =
  /\[(?<text>[^\]\n]+)\]\((?<url>https?:\/\/[^\s()]+(?:\([^\s()]*\)[^\s()]*)*)\)|\*\*(?<bold>[^*\n]+)\*\*/g;

// Explicit http(s) URLs anywhere in the text. atproto's own detection only fires after whitespace
// or "(", so it misses `<https://..>`, `"https://.."` and `Link:https://..`.
const URL_REGEX = /https?:\/\/[^\s<>"'`]+/g;

/**
 * Strips markdown links and bold from `markdown`, returning the visible text plus a link span for
 * each markdown link. Must stay in step with TemplateEngine.ContentLength in core, which budgets
 * Bluesky posts by this visible text.
 */
export function stripMarkdown(markdown: string): { text: string; links: LinkSpan[] } {
  let text = "";
  let last = 0;
  const links: LinkSpan[] = [];
  for (const m of markdown.matchAll(MARKDOWN_REGEX)) {
    text += markdown.slice(last, m.index);
    const groups = m.groups!;
    if (groups.text !== undefined) {
      links.push({ start: text.length, end: text.length + groups.text.length, uri: groups.url });
      text += groups.text;
    } else {
      text += groups.bold;
    }
    last = m.index + m[0].length;
  }
  text += markdown.slice(last);
  return { text, links };
}

/** Trims trailing punctuation off a detected URL, keeping a closing paren that balances one inside. */
function trimUrl(url: string): string {
  for (;;) {
    if (/[.,;:!?*_~\]}>]$/.test(url)) {
      url = url.slice(0, -1);
    } else if (url.endsWith(")") && url.split("(").length < url.split(")").length) {
      url = url.slice(0, -1);
    } else {
      return url;
    }
  }
}

function detectUrls(text: string, taken: LinkSpan[]): LinkSpan[] {
  const found: LinkSpan[] = [];
  for (const m of text.matchAll(URL_REGEX)) {
    const uri = trimUrl(m[0]);
    if (uri.length <= "https://".length) continue;
    const span = { start: m.index, end: m.index + uri.length, uri };
    if (!taken.some((t) => span.start < t.end && t.start < span.end)) found.push(span);
  }
  return found;
}

/**
 * Builds Bluesky post text and facets from a markdown body: markdown links become link facets over
 * their display text, bare http(s) URLs are linked wherever they appear, and atproto's own detection
 * supplies mentions, hashtags and scheme-less domains. Mentions that don't resolve to a DID are
 * dropped: a non-DID `did` fails record validation and rejects the whole post.
 */
export async function buildRichText(
  markdown: string,
  agent: unknown,
): Promise<{ text: string; facets: Facet[] | undefined }> {
  const { text, links } = stripMarkdown(markdown);
  const spans = [...links, ...detectUrls(text, links)];

  const rt = new RichText({ text });
  try {
    await rt.detectFacets(agent as AtpAgent);
  } catch {
    // Mention resolution failed outright; still link and tag what we can.
    rt.detectFacetsWithoutResolution();
  }

  const unicode = rt.unicodeText;
  const own = spans.map((s): Facet => ({
    $type: "app.bsky.richtext.facet",
    index: {
      byteStart: unicode.utf16IndexToUtf8Index(s.start),
      byteEnd: unicode.utf16IndexToUtf8Index(s.end),
    },
    features: [{ $type: "app.bsky.richtext.facet#link", uri: s.uri }],
  }));

  const detected = (rt.facets ?? []).filter((f) => {
    const overlaps = own.some(
      (o) => f.index.byteStart < o.index.byteEnd && o.index.byteStart < f.index.byteEnd,
    );
    // Unresolved mentions carry "" (failed lookup) or the bare handle (no resolution at all).
    const unresolvedMention = f.features.some(
      (feat) =>
        AppBskyRichtextFacet.isMention(feat) &&
        !(feat as AppBskyRichtextFacet.Mention).did.startsWith("did:"),
    );
    return !overlaps && !unresolvedMention;
  });

  const facets = [...own, ...detected].sort((a, b) => a.index.byteStart - b.index.byteStart);
  return { text, facets: facets.length > 0 ? facets : undefined };
}
