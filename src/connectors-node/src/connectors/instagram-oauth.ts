import { randomUUID } from "node:crypto";
import type { OAuthCompleteResult, OAuthProvider, OAuthStartResult } from "../types.js";

const AUTHORIZE_URL = "https://www.instagram.com/oauth/authorize";
const TOKEN_URL = "https://api.instagram.com/oauth/access_token";
const GRAPH_URL = "https://graph.instagram.com";

// instagram_business_basic covers isAuthenticated/listTargets; instagram_business_content_publish
// is what deliver() needs. Both are requested up front since Business Login has no incremental
// scope grant.
const SCOPES = "instagram_business_basic,instagram_business_content_publish";

export interface InstagramAppCredentials {
  appId: string;
  appSecret: string;
}

interface ShortLivedTokenResponse {
  access_token: string;
  user_id: string;
}

interface LongLivedTokenResponse {
  access_token: string;
  expires_in: number; // seconds
}

/** Secret persisted for an Instagram connector: what deliver()/isAuthenticated()/refresh() read back. */
export interface InstagramSecret {
  AccessToken: string;
  IgUserId: string;
  ExpiresAt: string; // ISO-8601, read by the core sweeper to decide when a refresh is due
}

function appendQueryParam(url: string, key: string, value: string): string {
  const sep = url.includes("?") ? "&" : "?";
  return `${url}${sep}${encodeURIComponent(key)}=${encodeURIComponent(value)}`;
}

/**
 * Business Login for Instagram's OAuth2 authorization-code flow. Unlike Tumblr's OAuth1 (long-lived
 * from the start) or Mastodon-style OAuth2 (tokens that don't expire), the token minted here is
 * short-lived and must be immediately exchanged for a long-lived one (~60 days), which then needs
 * periodic refreshing — see {@link InstagramSecret.ExpiresAt} and the core token-refresh sweeper.
 */
export class InstagramOAuth2Provider implements OAuthProvider {
  constructor(private readonly app: InstagramAppCredentials) {}

  async startAuthorization({ callbackUrl }: { callbackUrl: string }): Promise<OAuthStartResult> {
    // No request-token round trip in OAuth2 (unlike OAuth1): the correlation key is a random state
    // value the provider echoes back verbatim on the callback, exactly like the Mastodon-style
    // branch of the megalodon connector.
    const state = randomUUID();
    const url = new URL(AUTHORIZE_URL);
    url.searchParams.set("client_id", this.app.appId);
    url.searchParams.set("redirect_uri", callbackUrl);
    url.searchParams.set("response_type", "code");
    url.searchParams.set("scope", SCOPES);
    const authorizeUrl = appendQueryParam(url.toString(), "state", state);
    return { authorizeUrl, requestToken: state, requestTokenSecret: callbackUrl };
  }

  async completeAuthorization({
    requestTokenSecret,
    verifier,
  }: {
    requestTokenSecret: string;
    verifier: string;
  }): Promise<OAuthCompleteResult> {
    // requestTokenSecret carries the callback URL through from startAuthorization: the token
    // exchange must present the exact same redirect_uri it authorized against.
    const callbackUrl = requestTokenSecret;
    const short = await this.exchangeCode(callbackUrl, verifier);
    const long = await this.exchangeForLongLivedToken(short.access_token);
    const secret: InstagramSecret = {
      AccessToken: long.access_token,
      IgUserId: short.user_id,
      ExpiresAt: new Date(Date.now() + long.expires_in * 1000).toISOString(),
    };
    return { secretJson: JSON.stringify(secret) };
  }

  /** Exchanges a callback `code` for a short-lived (~1h) access token. */
  private async exchangeCode(callbackUrl: string, code: string): Promise<ShortLivedTokenResponse> {
    const body = new URLSearchParams({
      client_id: this.app.appId,
      client_secret: this.app.appSecret,
      grant_type: "authorization_code",
      redirect_uri: callbackUrl,
      code,
    });
    const res = await fetch(TOKEN_URL, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body,
    });
    const text = await res.text();
    if (!res.ok) throw new Error(`Instagram token exchange failed (${res.status}): ${text.slice(0, 500)}`);
    return JSON.parse(text) as ShortLivedTokenResponse;
  }

  /** Exchanges a short-lived token for a long-lived (~60 day) one. */
  private async exchangeForLongLivedToken(shortLivedToken: string): Promise<LongLivedTokenResponse> {
    const url = new URL(`${GRAPH_URL}/access_token`);
    url.searchParams.set("grant_type", "ig_exchange_token");
    url.searchParams.set("client_secret", this.app.appSecret);
    url.searchParams.set("access_token", shortLivedToken);
    const res = await fetch(url.toString());
    const text = await res.text();
    if (!res.ok) throw new Error(`Instagram long-lived token exchange failed (${res.status}): ${text.slice(0, 500)}`);
    return JSON.parse(text) as LongLivedTokenResponse;
  }
}

/** Refreshes a still-valid (≥24h old) long-lived token, resetting its ~60-day expiry. */
export async function refreshLongLivedToken(accessToken: string): Promise<LongLivedTokenResponse> {
  const url = new URL(`${GRAPH_URL}/refresh_access_token`);
  url.searchParams.set("grant_type", "ig_refresh_token");
  url.searchParams.set("access_token", accessToken);
  const res = await fetch(url.toString());
  const text = await res.text();
  if (!res.ok) throw new Error(`Instagram token refresh failed (${res.status}): ${text.slice(0, 500)}`);
  return JSON.parse(text) as LongLivedTokenResponse;
}
