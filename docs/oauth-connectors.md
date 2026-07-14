# OAuth "connect" flow for connectors

Some platforms let a user connect by clicking a button and authorizing in the provider's UI, rather
than pasting API tokens. Today this covers **Tumblr** (OAuth 1.0a).

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

## Adding another OAuth connector

- **connectors-node**: implement an `OAuthProvider` (`startAuthorization` / `completeAuthorization`)
  and attach it to the connector's `oauth` property (see `connectors/tumblr-oauth.ts`).
- **core**: set `SupportsOAuth: true` on the connector's `ConnectorDescriptor`. The generic
  start/callback endpoints and `HttpConnector` forwarding handle the rest.
- The frontend needs no per-platform change — it shows the "Connect" button whenever
  `/api/services` reports `supportsOAuth: true`.
