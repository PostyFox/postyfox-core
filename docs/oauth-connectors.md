# OAuth "connect" flow for connectors

Some platforms let a user connect by clicking a button and authorizing in the provider's UI, rather
than pasting API tokens. Today this covers **Tumblr** (OAuth 1.0a), **Instagram** (OAuth2, Business
Login for Instagram) and the **Fediverse** platforms (Mastodon, Pleroma, Akkoma, Friendica,
Iceshrimp, GoToSocial, Hometown and Pixelfed), all served by one generic megalodon connector that
auto-detects the instance's software (nodeinfo → SNS) and runs whichever authorization the instance
uses (OAuth2 for Mastodon-family, MiAuth for Iceshrimp/Misskey-family).

The same generic start/callback plumbing serves three authorization families. The connector fills in
the `requestToken` / `requestTokenSecret` / `verifier` fields differently, but core treats them
opaquely:

| Family | `requestToken` (correlation) | `verifier` (from callback) | Exchange credential |
|--------|------------------------------|----------------------------|---------------------|
| OAuth 1.0a (Tumblr) | request token | `oauth_verifier` | request token + verifier |
| OAuth2 (Mastodon-style, Instagram) | random `state` echoed back | `code` | authorization `code` |
| MiAuth (Iceshrimp/Misskey-family) | session token | *(none)* | stored session token |

The callback route accepts `oauth_token`/`oauth_verifier`, `state`/`code`, or `token`/`session`, and
the verifier is optional (MiAuth carries no callback code: the session token minted at start is what
gets exchanged).

## How it works

```
Browser ──"Connect"──▶ core POST /api/connectors/{id}/oauth/start
                         → connectors-node builds the provider authorize URL (request token)
                         → core stashes the request-token secret, returns the authorize URL
Browser ──redirect──▶ provider authorize page ──user approves──▶
Browser ──callback──▶ core GET /api/connectors/oauth/callback?oauth_token&oauth_verifier
                         → connectors-node exchanges for the access token
                         → core stores it as the connector's secret; popup closes
```

- The frontend opens the authorize URL in a popup and refreshes on completion (falls back to a
  full-page redirect if popups are blocked).
- OAuth1 tokens are long-lived, so there is **no refresh** to manage.
- The connector's per-user secret holds only `{OAuthToken, OAuthTokenSecret}`; the app (consumer)
  credentials live in the configured operational secret store. Delivery still runs through
  `tumblr.js`.

## Operator setup (Tumblr)

1. **Register a Tumblr application** at <https://www.tumblr.com/oauth/apps>. Note the **OAuth
   Consumer Key** and **Consumer Secret**.
2. Set the app's **Default callback URL** (and OAuth2 redirect if asked) to:

   ```
   {OAUTH_CALLBACK_BASE_URL}/api/connectors/oauth/callback
   ```

   For the local full-stack (behind the edge) that is:

   ```
   http://localhost:4180/api/connectors/oauth/callback
   ```

   For a deployed environment, use the public edge host, e.g.
   `https://app.postyfox.com/api/connectors/oauth/callback`. It **must match exactly**.
3. Sign in with a Keycloak account carrying the `postyfox-admin` realm role, open
   **Administration**, and set the Tumblr consumer key and consumer secret. They are stored as
   `TumblrConsumerKey` and `TumblrConsumerSecret`.

4. Configure the callback base URL for core-api (`OAuth__CallbackBaseUrl`). This is the public base
   the provider redirects back to.

   The callback base is sourced per stack:
   - **Local full stack** (`docker-compose.yml`, bundled edge): `OAUTH_CALLBACK_BASE_URL`
     (defaults to `http://localhost:4180`).
   - **Deployed** (`docker-compose.server.yml`, external edge): reuses **`PUBLIC_BASE_URL`**, the
     same public edge URL you already configure in `.env`, so there is no extra variable to set.

   If either operational secret is missing, Tumblr OAuth and delivery fail closed with a
   configuration error.

## Operator setup (Instagram)

Instagram uses **Business Login for Instagram** (Meta's newer, direct-to-Instagram OAuth2 flow —
not the older Facebook-Login-based Instagram Graph API). Only Business and Creator accounts are
eligible; personal accounts cannot authorize at all.

1. Create a Meta app at <https://developers.facebook.com/apps/> and add the **Instagram** product,
   configured for **Instagram API with Business Login**. Note the app's **Instagram app ID** and
   **Instagram app secret** (these are distinct from the app's general Facebook app ID/secret).
2. Set the app's OAuth redirect URI to:

   ```
   {OAUTH_CALLBACK_BASE_URL}/api/connectors/oauth/callback
   ```

   Same callback base as Tumblr (see above) — it **must match exactly**.
3. Sign in with a Keycloak account carrying the `postyfox-admin` realm role, open
   **Administration**, and set the Instagram app ID and app secret. They are stored as
   `InstagramAppId` and `InstagramAppSecret`.
4. The callback base URL (`OAuth__CallbackBaseUrl` / `PUBLIC_BASE_URL`) is the same one already
   configured for Tumblr — nothing extra to set there.

   If either operational secret is missing, Instagram OAuth and delivery fail closed with a
   configuration error, same as Tumblr.

**Token lifetime.** The connect flow yields a long-lived token (~60 days) stored as
`{AccessToken, IgUserId, ExpiresAt}`. Unlike Tumblr's OAuth1 tokens, this one expires: a background
sweeper (`ConnectorTokenRefreshSweeper`, hosted in the posting worker) refreshes it automatically
once it's within `ConnectorRefresh:RefreshWithinDays` (default 10) days of expiring — no user action
needed under normal operation. If a token lapses anyway (the sweeper was down for an extended
period, or Instagram revoked it), `isAuthenticated` starts failing and the user needs to reconnect.

**Media delivery.** Unlike every other connector, Instagram's Content Publishing API fetches media
by public URL (`image_url`/`video_url`) rather than accepting a direct upload. The connector stages
normalized bytes in the object store under a short-lived presigned URL for Instagram to fetch, then
deletes the staged object once the container is created. This only works when the object store is
actually reachable from the public internet — true for real S3 in a deployed stack, but **not** true
for a local MinIO dev stack reachable only inside the Docker network. Instagram delivery cannot be
exercised end-to-end against a local-only dev stack; it needs at least a tunnel (e.g. ngrok) or a
real S3 bucket.

## Operator setup (Iceshrimp / Fediverse)

**Nothing to configure.** Unlike Tumblr, the Fediverse connector registers its own application on the
user's instance dynamically at connect time (`registerApp`), so there are no operator-provided
consumer credentials or environment variables. The only requirement is the shared callback base
(`OAuth__CallbackBaseUrl` / `PUBLIC_BASE_URL`, already set for Tumblr) so the instance can redirect
back to `{base}/api/connectors/oauth/callback`.

The user supplies their **instance URL** in the connector's config; the connect flow then detects the
instance's software (nodeinfo → megalodon SNS) and mints the app + session token per connect. The
per-user secret holds only `{AccessToken, Sns}`.

> **Note:** Misskey-family MiAuth redirects back to the registered callback and the exact query
> parameter carrying the session token can vary by instance software/version. The callback route
> accepts `token`/`session`/`state` as correlation candidates; verify against your target instance if
> a connect appears to succeed in the provider UI but does not complete.

## Adding another OAuth connector

- **connectors-node**: implement an `OAuthProvider` (`startAuthorization` / `completeAuthorization`)
  and attach it to the connector's `oauth` property (see `connectors/tumblr-oauth.ts` for OAuth1,
  `connectors/instagram-oauth.ts` for a "static app credentials + OAuth2 code exchange" example, or
  `connectors/megalodon.ts` for instance-scoped OAuth2/MiAuth). `startAuthorization` receives the
  connector's `configJson` for instance-scoped providers.
- **core**: set `SupportsOAuth: true` on the connector's `ConnectorDescriptor`. The generic
  start/callback endpoints and `HttpConnector` forwarding handle the rest.
- The frontend needs no per-platform change: it shows the "Connect" button whenever
  `/api/services` reports `supportsOAuth: true`.
- **If the token expires** (Tumblr's and the Fediverse's don't; Instagram's does): implement
  `Connector.refresh` in connectors-node and have the connector class implement
  `IRefreshableConnector` on the core side (see `instagram.ts` / `HttpConnector.RefreshTokenAsync`).
  The connector's secret JSON must carry an `ExpiresAt` field — `ConnectorTokenRefreshService` reads
  it generically across every `IRefreshableConnector`, so a new expiring-token platform needs no
  sweeper changes, only the connector-level `refresh` implementation.
