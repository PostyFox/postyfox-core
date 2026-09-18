import type { ScraperResponse } from "./http.js";

/**
 * Detects Cloudflare's own challenge response (rather than the target site's real page), so a
 * connector can surface a clear, actionable error instead of parsing challenge HTML as content.
 * Shared by every scraped connector fronted by Cloudflare (FurAffinity, Toyhouse): the diagnosis and
 * the fix are identical regardless of site.
 */
export function isCloudflareChallenge(response: ScraperResponse): boolean {
  const mitigated = response.headers.get("cf-mitigated")?.toLowerCase() === "challenge";
  const challengePage =
    /<title[^>]*>\s*just a moment(?:\.\.\.)?\s*<\/title>/i.test(response.body) ||
    /\/cdn-cgi\/challenge-platform\//i.test(response.body) ||
    /window\._cf_chl_opt/i.test(response.body);
  return mitigated || challengePage;
}

export function throwIfCloudflareChallenge(response: ScraperResponse, siteName: string): void {
  if (isCloudflareChallenge(response))
    throw new Error(
      `${siteName} requires a Cloudflare challenge; refresh the imported session cookies in a browser`,
    );
}
