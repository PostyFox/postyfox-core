/**
 * Extracts the most actionable message from an error thrown by a connector's HTTP client.
 *
 * Each social/Fediverse client surfaces a server rejection differently, and the bare `Error.message`
 * hides the useful part:
 *   - megalodon (axios): message is only "Request failed with status code 400"; the real detail is
 *     the response body on `err.response.data`.
 *   - atproto (XRPCError): carries a numeric `status` and a machine-readable `error` code alongside
 *     the message.
 *   - tumblr.js: builds "API error: <status> <msg>", and may attach the parsed body.
 *
 * Normalising them here means a delivery/auth failure is logged and stored with its cause intact
 * rather than reduced to a generic string. Used by every connector's deliver()/isAuthenticated().
 */
export function describeError(err: unknown): string {
  if (err && typeof err === "object") {
    const e = err as Record<string, unknown>;

    // axios (megalodon): the server's response body holds the detail.
    const resp = e.response as { status?: number; data?: unknown } | undefined;
    if (resp && (resp.data !== undefined || typeof resp.status === "number")) {
      const status = resp.status ?? "?";
      const body = stringifyDetail(resp.data);
      return body ? `HTTP ${status}: ${truncate(body, 2000)}` : `HTTP ${status}`;
    }

    // atproto XRPCError: numeric status + machine-readable error code + message.
    if (typeof e.status === "number" && (typeof e.error === "string" || typeof e.message === "string")) {
      const code = typeof e.error === "string" && e.error ? ` ${e.error}` : "";
      const msg = typeof e.message === "string" && e.message ? `: ${e.message}` : "";
      return `HTTP ${e.status}${code}${msg}`;
    }

    // tumblr.js (and any client that attaches the parsed body): surface it alongside the message.
    if (e.body !== undefined) {
      const body = stringifyDetail(e.body);
      if (body) {
        return typeof e.message === "string" && e.message
          ? `${e.message}: ${truncate(body, 2000)}`
          : truncate(body, 2000);
      }
    }
  }

  return err instanceof Error ? err.message : String(err);
}

function stringifyDetail(value: unknown): string {
  if (value === undefined || value === null) return "";
  if (typeof value === "string") return value;
  try {
    return JSON.stringify(value);
  } catch {
    return String(value);
  }
}

function truncate(text: string, max: number): string {
  return text.length > max ? `${text.slice(0, max)}… (${text.length} chars)` : text;
}
