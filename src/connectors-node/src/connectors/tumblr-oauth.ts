import { createHmac } from "node:crypto";
import OAuth from "oauth-1.0a";
import type { OAuthCompleteResult, OAuthProvider, OAuthStartResult } from "../types.js";

const REQUEST_TOKEN_URL = "https://www.tumblr.com/oauth/request_token";
const AUTHORIZE_URL = "https://www.tumblr.com/oauth/authorize";
const ACCESS_TOKEN_URL = "https://www.tumblr.com/oauth/access_token";

export interface TumblrConsumerCredentials {
  consumerKey: string;
  consumerSecret: string;
}

/** Reads Tumblr app (consumer) credentials from the environment, if configured. */
export function tumblrConsumerFromEnv(): TumblrConsumerCredentials | undefined {
  const consumerKey = process.env.TUMBLR_CONSUMER_KEY;
  const consumerSecret = process.env.TUMBLR_CONSUMER_SECRET;
  if (!consumerKey || !consumerSecret) return undefined;
  return { consumerKey, consumerSecret };
}

/**
 * Tumblr's OAuth 1.0a three-legged flow. Consumer (app) credentials come from the operator's
 * environment; the per-user token + secret it yields are what tumblr.js needs to post. Tokens are
 * long-lived, so there is no refresh to manage.
 */
export class TumblrOAuth1Provider implements OAuthProvider {
  private readonly oauth: OAuth;

  constructor(private readonly consumer: TumblrConsumerCredentials) {
    this.oauth = new OAuth({
      consumer: { key: consumer.consumerKey, secret: consumer.consumerSecret },
      signature_method: "HMAC-SHA1",
      hash_function: (base, key) => createHmac("sha1", key).update(base).digest("base64"),
    });
  }

  async startAuthorization({ callbackUrl }: { callbackUrl: string }): Promise<OAuthStartResult> {
    const form = await this.signedPost(REQUEST_TOKEN_URL, { oauth_callback: callbackUrl });
    const requestToken = form.get("oauth_token");
    const requestTokenSecret = form.get("oauth_token_secret");
    if (!requestToken || !requestTokenSecret) {
      throw new Error("Tumblr did not return a request token");
    }
    return {
      authorizeUrl: `${AUTHORIZE_URL}?oauth_token=${encodeURIComponent(requestToken)}`,
      requestToken,
      requestTokenSecret,
    };
  }

  async completeAuthorization({
    requestToken,
    requestTokenSecret,
    verifier,
  }: {
    requestToken: string;
    requestTokenSecret: string;
    verifier: string;
  }): Promise<OAuthCompleteResult> {
    const form = await this.signedPost(
      ACCESS_TOKEN_URL,
      { oauth_verifier: verifier },
      { key: requestToken, secret: requestTokenSecret },
    );
    const token = form.get("oauth_token");
    const tokenSecret = form.get("oauth_token_secret");
    if (!token || !tokenSecret) {
      throw new Error("Tumblr did not return an access token");
    }
    // Only the per-user token pair is stored; the consumer credentials stay in the environment.
    return { secretJson: JSON.stringify({ OAuthToken: token, OAuthTokenSecret: tokenSecret }) };
  }

  /** Signs and POSTs an OAuth1 request; returns the parsed form-encoded response. */
  private async signedPost(
    url: string,
    oauthParams: Record<string, string>,
    token?: { key: string; secret: string },
  ): Promise<URLSearchParams> {
    const authData = this.oauth.authorize({ url, method: "POST", data: oauthParams }, token);
    const header = this.oauth.toHeader(authData);
    const res = await fetch(url, {
      method: "POST",
      headers: { ...header, "Content-Type": "application/x-www-form-urlencoded" },
    });
    const text = await res.text();
    if (!res.ok) {
      throw new Error(`Tumblr OAuth request failed (${res.status}): ${text.slice(0, 200)}`);
    }
    return new URLSearchParams(text);
  }
}
