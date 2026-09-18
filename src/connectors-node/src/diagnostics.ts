/**
 * Best-effort diagnostics for a cookie-pairing connector's secret (FurAffinity, Toyhouse): the
 * User-Agent that will be replayed, and which cookie names are present — never their values, which
 * are session credentials. Returns null for a secret that isn't shaped like a cookie-paired session
 * (no `CookieHeader`), so logging stays silent for every other platform (Bluesky, Tumblr, Instagram,
 * Fediverse) whose secrets carry OAuth tokens instead. Never throws: a malformed secret is the
 * connector's own problem to report, not this helper's.
 */
export function cookiePairingDiagnostics(
  secretJson: string | null | undefined,
): { userAgent: string | null; cookieNames: string[] } | null {
  if (!secretJson) return null;
  let secret: unknown;
  try {
    secret = JSON.parse(secretJson);
  } catch {
    return null;
  }
  if (typeof secret !== "object" || secret === null) return null;

  const { CookieHeader, UserAgent } = secret as Record<string, unknown>;
  if (typeof CookieHeader !== "string") return null;

  const cookieNames = CookieHeader.split(";")
    .map((pair) => pair.split("=")[0]?.trim())
    .filter((name): name is string => Boolean(name));

  return {
    userAgent: typeof UserAgent === "string" ? UserAgent : null,
    cookieNames,
  };
}
