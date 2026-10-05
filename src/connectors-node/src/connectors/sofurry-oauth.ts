import { createHash, randomBytes, randomUUID } from "node:crypto";
import type { OAuthCompleteResult, OAuthProvider, OAuthStartResult } from "../types.js";

// From https://api.sofurry.com/.well-known/openid-configuration.
const AUTHORIZE_URL = "https://api.sofurry.com/oauth/authorize";
const TOKEN_URL = "https://api.sofurry.com/oauth/token";

export interface SofurryAppCredentials {
  clientId: string;
  clientSecret: string;
}

/** Secret persisted for a SoFurry connector: what deliver()/isAuthenticated()/refresh() read back. */
export interface SofurrySecret {
  AccessToken: string;
  RefreshToken?: string;
  /** ISO-8601, read by the core sweeper to decide when a refresh is due. Absent when the server sent no expiry. */
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
export class SofurryGrantRejectedError extends Error {}

/**
 * SoFurry's OAuth2 authorization-code flow with PKCE (S256). SoFurry's OpenID configuration
 * advertises no upload scope, so no scope is requested: a user token reaches the whole public API.
 */
export class SofurryOAuth2Provider implements OAuthProvider {
  constructor(
    private readonly app: SofurryAppCredentials,
    private readonly fetchImpl: typeof fetch = fetch,
  ) {}

  async startAuthorization({ callbackUrl }: { callbackUrl: string }): Promise<OAuthStartResult> {
    const state = randomUUID();
    const codeVerifier = randomBytes(48).toString("base64url");
    const url = new URL(AUTHORIZE_URL);
    url.searchParams.set("client_id", this.app.clientId);
    url.searchParams.set("redirect_uri", callbackUrl);
    url.searchParams.set("response_type", "code");
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
  async refresh(secret: SofurrySecret): Promise<SofurrySecret> {
    if (!secret.RefreshToken) throw new SofurryGrantRejectedError("SoFurry connection has no refresh token");
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
      // invalid_grant/invalid_client: the code or refresh token is spent, or the app was revoked.
      throw new SofurryGrantRejectedError(`SoFurry token request was rejected (${res.status}): ${text.slice(0, 500)}`);
    }
    if (!res.ok) throw new Error(`SoFurry token request failed (${res.status}): ${text.slice(0, 500)}`);
    const token = JSON.parse(text) as TokenResponse;
    if (!token.access_token) throw new Error("SoFurry token response had no access token");
    return token;
  }
}

function toSecret(token: TokenResponse, previousRefreshToken?: string): SofurrySecret {
  return {
    AccessToken: token.access_token!,
    RefreshToken: token.refresh_token ?? previousRefreshToken,
    ExpiresAt:
      typeof token.expires_in === "number"
        ? new Date(Date.now() + token.expires_in * 1000).toISOString()
        : undefined,
  };
}
