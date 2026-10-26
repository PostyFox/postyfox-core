import { createHash, randomBytes, randomUUID } from "node:crypto";
import type { OAuthCompleteResult, OAuthProvider, OAuthStartResult } from "../types.js";
import { fetchWithUserAgent } from "../user-agent.js";

// From https://deviantart.readme.io/docs/authentication.
const AUTHORIZE_URL = "https://www.deviantart.com/oauth2/authorize";
const TOKEN_URL = "https://www.deviantart.com/oauth2/token";
// `user` for /user/whoami, `stash` for /stash/submit, `publish` for /stash/publish.
const SCOPES = "basic user stash publish";

export interface DeviantartAppCredentials {
  clientId: string;
  clientSecret: string;
}

/** Secret persisted for a DeviantArt connector: what deliver()/isAuthenticated()/refresh() read back. */
export interface DeviantartSecret {
  AccessToken: string;
  RefreshToken?: string;
  /** ISO-8601. Access tokens last an hour, so core refreshes them before use once this has passed. */
  ExpiresAt?: string;
}

interface TokenResponse {
  access_token?: string;
  refresh_token?: string;
  expires_in?: number;
}

/** What startAuthorization hands to completeAuthorization through core's opaque requestTokenSecret. */
interface PendingAuthorization {
  callbackUrl: string;
  codeVerifier: string;
}

/** A refresh the server refused (revoked or expired refresh token): the user has to reconnect. */
export class DeviantartGrantRejectedError extends Error {}

/** DeviantArt's OAuth2 authorization-code flow with PKCE (S256), as OAuth 2.1 apps require. */
export class DeviantartOAuth2Provider implements OAuthProvider {
  constructor(
    private readonly app: DeviantartAppCredentials,
    private readonly fetchImpl: typeof fetch = fetchWithUserAgent,
  ) {}

  async startAuthorization({ callbackUrl }: { callbackUrl: string }): Promise<OAuthStartResult> {
    const state = randomUUID();
    const codeVerifier = randomBytes(48).toString("base64url");
    const url = new URL(AUTHORIZE_URL);
    url.searchParams.set("response_type", "code");
    url.searchParams.set("client_id", this.app.clientId);
    url.searchParams.set("redirect_uri", callbackUrl);
    url.searchParams.set("scope", SCOPES);
    url.searchParams.set("state", state);
    url.searchParams.set("code_challenge", createHash("sha256").update(codeVerifier).digest("base64url"));
    url.searchParams.set("code_challenge_method", "S256");
    // The token exchange must present the same redirect_uri and the PKCE verifier; core keeps
    // requestTokenSecret server-side between start and callback, so both travel in it.
    const pending: PendingAuthorization = { callbackUrl, codeVerifier };
    return { authorizeUrl: url.toString(), requestToken: state, requestTokenSecret: JSON.stringify(pending) };
  }

  async completeAuthorization({
    requestTokenSecret,
    verifier,
  }: {
    requestTokenSecret: string;
    verifier: string;
  }): Promise<OAuthCompleteResult> {
    const pending = JSON.parse(requestTokenSecret) as PendingAuthorization;
    const token = await this.requestToken({
      grant_type: "authorization_code",
      code: verifier,
      redirect_uri: pending.callbackUrl,
      code_verifier: pending.codeVerifier,
    });
    return { secretJson: JSON.stringify(toSecret(token)) };
  }

  /** Exchanges the stored refresh token for a new access token, keeping the old refresh token if none is issued. */
  async refresh(secret: DeviantartSecret): Promise<DeviantartSecret> {
    if (!secret.RefreshToken) throw new DeviantartGrantRejectedError("DeviantArt connection has no refresh token");
    const token = await this.requestToken({ grant_type: "refresh_token", refresh_token: secret.RefreshToken });
    return toSecret(token, secret.RefreshToken);
  }

  private async requestToken(params: Record<string, string>): Promise<TokenResponse> {
    const res = await this.fetchImpl(TOKEN_URL, {
      method: "POST",
      headers: { "content-type": "application/x-www-form-urlencoded", accept: "application/json" },
      body: new URLSearchParams({
        ...params,
        client_id: this.app.clientId,
        client_secret: this.app.clientSecret,
      }),
    });
    const text = await res.text();
    if (res.status === 400 || res.status === 401) {
      // invalid_request/invalid_client: the code or refresh token is spent, or the app was revoked.
      throw new DeviantartGrantRejectedError(`DeviantArt token request was rejected (${res.status}): ${text.slice(0, 500)}`);
    }
    if (!res.ok) throw new Error(`DeviantArt token request failed (${res.status}): ${text.slice(0, 500)}`);
    const token = JSON.parse(text) as TokenResponse;
    if (!token.access_token) throw new Error("DeviantArt token response had no access token");
    return token;
  }
}

function toSecret(token: TokenResponse, previousRefreshToken?: string): DeviantartSecret {
  return {
    AccessToken: token.access_token!,
    RefreshToken: token.refresh_token ?? previousRefreshToken,
    ExpiresAt:
      typeof token.expires_in === "number"
        ? new Date(Date.now() + token.expires_in * 1000).toISOString()
        : undefined,
  };
}
