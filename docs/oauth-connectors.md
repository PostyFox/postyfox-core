# OAuth "connect" flow for connectors

Some platforms let a user connect by clicking a button and authorizing in the provider's UI, rather
than pasting API tokens. Today this covers **Tumblr** (OAuth 1.0a) and the **Fediverse** platforms —
Mastodon, Pleroma, Akkoma, Friendica, Firefish, Iceshrimp, GoToSocial, Hometown and Pixelfed — all
served by one generic megalodon connector that auto-detects the instance's software (nodeinfo → SNS)
and runs whichever authorization the instance uses (OAuth2 for Mastodon-family, MiAuth for
Firefish/Iceshrimp).

The same generic start/callback plumbing serves three authorization families. The connector fills in
the `requestToken` / `requestTokenSecret` / `verifier` fields differently, but core treats them
opaquely:

| Family | `requestToken` (correlation) | `verifier` (from callback) | Exchange credential |
|--------|------------------------------|----------------------------|---------------------|
| OAuth 1.0a (Tumblr) | request token | `oauth_verifier` | request token + verifier |
| OAuth2 (Mastodon-style) | random `state` echoed back | `code` | authorization `code` |
| MiAuth (Iceshrimp/Firefish) | session token | *(none)* | stored session token |

The callback route accepts `oauth_token`/`oauth_verifier`, `state`/`code`, or `token`/`session`, and
the verifier is optional (MiAuth carries no callback code — the session token minted at start is what
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
  credentials stay in the environment (below). Delivery still runs through `tumblr.js`.

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
3. Provide the credentials + callback base via environment (e.g. in `deploy/.env`):

   | Variable | Service | Purpose |
   |----------|---------|---------|
   | `TUMBLR_CONSUMER_KEY` | connectors-node | Tumblr app consumer key |
   | `TUMBLR_CONSUMER_SECRET` | connectors-node | Tumblr app consumer secret |
   | callback base URL | core-api (`OAuth__CallbackBaseUrl`) | Public base the provider redirects back to |

   The callback base is sourced per stack:
   - **Local full stack** (`docker-compose.yml`, bundled edge): `OAUTH_CALLBACK_BASE_URL`
     (defaults to `http://localhost:4180`).
   - **Deployed** (`docker-compose.server.yml`, external edge): reuses **`PUBLIC_BASE_URL`** — the
     same public edge URL you already configure in `.env`, so there is no extra variable to set.

   The env vars are wired in both `docker-compose.yml` and `docker-compose.server.yml`, and listed
   in the `deploy/.env*.example` templates. If `TUMBLR_CONSUMER_KEY/SECRET` are unset, the connector
   still lists in the catalogue but `SupportsOAuth` is reported false and the "Connect" button is
   hidden.

## Operator setup (Iceshrimp / Fediverse)

**Nothing to configure.** Unlike Tumblr, the Fediverse connector registers its own application on the
user's instance dynamically at connect time (`registerApp`), so there are no operator-provided
consumer credentials or environment variables. The only requirement is the shared callback base
(`OAuth__CallbackBaseUrl` / `PUBLIC_BASE_URL`, already set for Tumblr) so the instance can redirect
back to `{base}/api/connectors/oauth/callback`.

The user supplies their **instance URL** in the connector's config; the connect flow then detects the
instance's software (nodeinfo → megalodon SNS) and mints the app + session token per connect. The
per-user secret holds only `{AccessToken, Sns}`.

> **Note:** Firefish/Misskey MiAuth redirects back to the registered callback and the exact query
> parameter carrying the session token can vary by instance software/version. The callback route
> accepts `token`/`session`/`state` as correlation candidates; verify against your target instance if
> a connect appears to succeed in the provider UI but does not complete.

## Adding another OAuth connector

- **connectors-node**: implement an `OAuthProvider` (`startAuthorization` / `completeAuthorization`)
  and attach it to the connector's `oauth` property (see `connectors/tumblr-oauth.ts` for OAuth1, or
  `connectors/megalodon.ts` for OAuth2/MiAuth). `startAuthorization` receives the connector's
  `configJson` for instance-scoped providers.
- **core**: set `SupportsOAuth: true` on the connector's `ConnectorDescriptor`. The generic
  start/callback endpoints and `HttpConnector` forwarding handle the rest.
- The frontend needs no per-platform change — it shows the "Connect" button whenever
  `/api/services` reports `supportsOAuth: true`.
