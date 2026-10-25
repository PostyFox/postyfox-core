/** Self-identifying User-Agent for outbound requests that have no better one to send. */
export const DEFAULT_USER_AGENT = "Mozilla/5.0 (compatible; PostyFox/1.0; +http://docs.postyfox.com)";

/** `fetch` that adds {@link DEFAULT_USER_AGENT} unless the caller already set a User-Agent. */
export const fetchWithUserAgent: typeof fetch = (input, init) => {
  const headers = new Headers(init?.headers ?? (input instanceof Request ? input.headers : undefined));
  if (!headers.has("user-agent")) headers.set("user-agent", DEFAULT_USER_AGENT);
  return fetch(input, { ...init, headers });
};
