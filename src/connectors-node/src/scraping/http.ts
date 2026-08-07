import { CookieJar } from "tough-cookie";

const DEFAULT_TIMEOUT_MS = 30_000;
const DEFAULT_MAX_RESPONSE_BYTES = 5 * 1024 * 1024;
const MAX_REDIRECTS = 10;

export interface ScraperResponse {
  status: number;
  url: string;
  body: string;
  headers: Headers;
}

export interface ScraperRequest {
  method?: "GET" | "POST";
  headers?: RequestInit["headers"];
  body?: RequestInit["body"];
  redirect?: "follow" | "manual";
}

export interface ScraperSession {
  request(path: string, request?: ScraperRequest): Promise<ScraperResponse>;
}

export type FetchLike = (input: string | URL | Request, init?: RequestInit) => Promise<Response>;

/**
 * Cookie-aware HTTP session for form-driven websites. The origin is fixed at construction time so
 * connector-controlled paths cannot become an SSRF escape hatch.
 */
export class CookieScraperSession implements ScraperSession {
  private readonly origin: string;
  private readonly jar = new CookieJar();

  private constructor(
    private readonly baseUrl: URL,
    private readonly fetchImpl: FetchLike,
    private readonly timeoutMs: number,
    private readonly maxResponseBytes: number,
  ) {
    this.origin = baseUrl.origin;
  }

  static async create(
    baseUrl: string,
    cookieHeader: string,
    options: {
      fetch?: FetchLike;
      timeoutMs?: number;
      maxResponseBytes?: number;
    } = {},
  ): Promise<CookieScraperSession> {
    const session = new CookieScraperSession(
      new URL(baseUrl),
      options.fetch ?? fetch,
      options.timeoutMs ?? DEFAULT_TIMEOUT_MS,
      options.maxResponseBytes ?? DEFAULT_MAX_RESPONSE_BYTES,
    );
    await session.importCookieHeader(cookieHeader);
    return session;
  }

  async request(path: string, request: ScraperRequest = {}): Promise<ScraperResponse> {
    let url = new URL(path, this.baseUrl);
    if (url.origin !== this.origin) throw new Error("scraper request must remain on the configured origin");
    let method = request.method ?? "GET";
    let body = request.body;
    const headers = new Headers(request.headers);
    let response: Response | undefined;

    for (let redirectCount = 0; redirectCount <= MAX_REDIRECTS; redirectCount++) {
      const cookies = await this.jar.getCookieString(url.toString());
      if (cookies) headers.set("cookie", cookies);
      else headers.delete("cookie");
      headers.set("user-agent", "PostyFox/1.0");

      response = await this.fetchImpl(url, {
        method,
        headers,
        body,
        redirect: "manual",
        signal: AbortSignal.timeout(this.timeoutMs),
      });

      for (const cookie of response.headers.getSetCookie())
        await this.jar.setCookie(cookie, url.toString());

      if (![301, 302, 303, 307, 308].includes(response.status) || request.redirect === "manual") break;
      if (redirectCount === MAX_REDIRECTS) throw new Error("scraper response exceeded the redirect limit");

      const location = response.headers.get("location");
      if (!location) throw new Error("scraper redirect did not include a location");
      const redirected = new URL(location, url);
      if (redirected.origin !== this.origin)
        throw new Error("scraper redirect must remain on the configured origin");
      url = redirected;

      if (response.status === 303 || ((response.status === 301 || response.status === 302) && method === "POST")) {
        method = "GET";
        body = undefined;
        headers.delete("content-type");
      }
    }

    if (!response) throw new Error("scraper request produced no response");

    const declaredLength = Number(response.headers.get("content-length") ?? 0);
    if (declaredLength > this.maxResponseBytes)
      throw new Error(`scraper response exceeds ${this.maxResponseBytes} bytes`);

    const bytes = Buffer.from(await response.arrayBuffer());
    if (bytes.length > this.maxResponseBytes)
      throw new Error(`scraper response exceeds ${this.maxResponseBytes} bytes`);

    return {
      status: response.status,
      url: url.toString(),
      body: bytes.toString("utf8"),
      headers: response.headers,
    };
  }

  async exportCookieHeader(): Promise<string> {
    return this.jar.getCookieString(this.baseUrl.toString());
  }

  private async importCookieHeader(cookieHeader: string): Promise<void> {
    if (/[\r\n]/.test(cookieHeader)) throw new Error("cookie header contains invalid line breaks");

    for (const pair of cookieHeader.split(";")) {
      const trimmed = pair.trim();
      const separator = trimmed.indexOf("=");
      if (separator <= 0) continue;
      const name = trimmed.slice(0, separator).trim();
      const value = trimmed.slice(separator + 1).trim();
      await this.jar.setCookie(`${name}=${value}; Path=/; Secure`, this.baseUrl.toString());
    }

    if (!(await this.jar.getCookieString(this.baseUrl.toString())))
      throw new Error("FurAffinity cookie header contains no cookies");
  }
}
