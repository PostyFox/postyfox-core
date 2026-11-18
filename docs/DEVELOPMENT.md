# Development guide

How to run, build, test and configure postyfox-core. For how the system fits together see
[ARCHITECTURE.md](./ARCHITECTURE.md); for deployment see [../deploy/README.md](../deploy/README.md).

## Prerequisites

- .NET 11 SDK
- Node.js 24 and npm (for `src/connectors-node`)
- Docker or Podman
- The Six Labors licence file (`sixlabors.lic`) in the repo root; see [ImageSharp licence](#imagesharp-licence)

## Run the local stack

```bash
cd deploy
docker compose up --build            # full stack incl. the OIDC edge (Keycloak + oauth2-proxy + gateway)
# Edge (log in here):  http://localhost:4180   → fans out to core-api / post-api after OIDC login
# Keycloak:            http://localhost:8082
# Swagger UI: http://localhost:8080/swagger  and  http://localhost:8081/swagger
# OpenAPI:    http://localhost:8080/openapi/v1.json  (and :8081)
# RabbitMQ:  http://localhost:15672   RustFS console: http://localhost:9001
# connectors-node: http://localhost:8090/health
```

Auth is always the production-representative OIDC path. There is no DevMode bypass. oauth2-proxy
performs the OIDC exchange against Keycloak and the APIs validate the forwarded `Authorization: Bearer`
token in-app, so reach them through the edge at <http://localhost:4180>. Hitting the APIs directly
(`:8080`/`:8081`) requires a valid bearer token. External/machine callers authenticate with
`X-API-Key: <key>` (create one via `POST /api/profile/keys`).

**Browser login:** open <http://localhost:4180> and sign in as `postyfox` /
`postyfox` (Keycloak admin console at <http://localhost:8082>, `admin` / `admin`). Keycloak's issuer
is pinned to `localhost:8082` (`KC_HOSTNAME`) so the browser and the in-cluster back channel stay
consistent; oauth2-proxy uses split front/back-channel URLs: see
[`deploy/oauth2-proxy/oauth2-proxy.cfg`](../deploy/oauth2-proxy/oauth2-proxy.cfg).

## Build and test

```bash
# from the repo root
dotnet build                 # whole solution (PostyFox.Platform.slnx)
dotnet test                  # all unit/integration tests
# Node connectors:  cd src/connectors-node && npm ci && npm test

# EF migrations
dotnet dotnet-ef migrations add <Name> --project src/PostyFox.Infrastructure
```

Tests use in-memory SQLite / EF-InMemory and fakes for I/O. No Docker required to run them.

## Project layout

```
src/
  PostyFox.Domain          entities + enums (no deps)
  PostyFox.Application      abstractions, services, template engine, pipeline handlers, connector contract
  PostyFox.Infrastructure   EF Core + migrations, S3, RabbitMQ, secrets wiring, Discord + Telegram connectors, HttpConnector
  PostyFox.Web              shared auth (OIDC bearer + API key) + OpenTelemetry wiring
  PostyFox.Api.Core         profile, delegation, terms, admin, catalogue, connectors, media, templates, triggers
  PostyFox.Api.Post         posts, drafts, history, automations, trigger webhooks
  PostyFox.Worker.Posting   queue consumers and background sweepers
  connectors-node/          Node/TS service for every platform except Discord and Telegram
clients/
  postyfox-connect/         browser extension for cookie pairing
tests/                      xUnit, one project per layer
testing-platforms/          disposable local platform stacks for manual checks
```

Adding a platform means implementing `IConnector` (usually in connectors-node, behind
`HttpConnector`) and adding a `ServiceDefinition` row. See ARCHITECTURE section 6 and the
[connectors-node README](../src/connectors-node/README.md).

## ImageSharp licence

`PostyFox.Infrastructure` uses SixLabors.ImageSharp 4, which validates a Six Labors licence at
compile time. Without one, Debug builds warn and **Release builds fail** ("No Six Labors license
found"). The root [`Directory.Build.props`](../Directory.Build.props) resolves it in this order:

1. `SIXLABORS_LICENSE_KEY` env var holding the full `.lic` file contents (how CI supplies it).
2. `sixlabors.lic` in the repo root (local dev). It is gitignored: never commit it.

For image builds, `deploy/docker-compose.yml` needs nothing extra: the root `sixlabors.lic` reaches
the Dockerfile's build stage via `COPY . .` and never reaches the final image. This works with
Docker and with Podman (classic builder). CI has no file and passes the licence as a build secret
instead, which works the same way locally:

```bash
docker build --secret id=sixlabors_license,src=sixlabors.lic \
  --build-arg PROJECT=src/PostyFox.Api.Core/PostyFox.Api.Core.csproj \
  --build-arg ASSEMBLY=PostyFox.Api.Core.dll --target final .
```

In GitHub, the licence is the `SIXLABORS_LICENSE_KEY` repository secret, set for both **Actions**
and **Dependabot** (Dependabot PRs can't read Actions secrets). To rotate it:

```bash
gh secret set SIXLABORS_LICENSE_KEY -R PostyFox/postyfox-core < sixlabors.lic
gh secret set SIXLABORS_LICENSE_KEY -R PostyFox/postyfox-core --app dependabot < sixlabors.lic
```

## Configuration

Nested keys use `__`. Key settings: `ConnectionStrings__Postgres`, `ObjectStore__*`, `RabbitMq__*`,
`Secrets__Provider` (`InMemory` | `HashiCorpVault` | `BitWarden` | `AzureKeyVault` | `Infisical`; provider options under
`Secrets__<Provider>__*`, e.g. `Secrets__BitWarden__ServerUrl`), `Auth__Oidc__Enabled` / `Auth__Oidc__Issuer` /
`Auth__Oidc__JwksUrl` / `Auth__Oidc__Audience`, `Auth__UserHeader`,
`NodeConnectors__BaseUrl` + `NodeConnectors__InternalToken` (→ connectors-node; also set as
`INTERNAL_TOKEN` on that service; `RATE_LIMIT_MAX` on connectors-node caps requests per client IP per
minute, default 600), `ApplyMigrations`, `SeedServiceDefinitions`,
`OTEL_EXPORTER_OTLP_ENDPOINT`. The public edge URL is set as `OAuth__CallbackBaseUrl` on core-api
(OAuth callbacks) and `PUBLIC_BASE_URL` on connectors-node (Instagram's staged media); the compose
files and Helm chart derive both from one value. Provider defaults per deployment are in
[deploy/README.md](../deploy/README.md#secret-store).

Trigger signing secrets live in the secret store under `trigger-{sourceType}-signing`. Administrators
with the Keycloak `postyfox-admin` realm role can manage connector operational credentials at
`/admin`. Telegram uses `TelegramApiID` / `TelegramApiHash`; Tumblr uses `TumblrConsumerKey` /
`TumblrConsumerSecret`; Instagram uses `InstagramAppId` / `InstagramAppSecret`; SoFurry uses `SofurryClientId` /
`SofurryClientSecret`; DeviantArt uses `DeviantartClientId` / `DeviantartClientSecret`. Values are
written to the configured secret provider and are never returned. Operator setup for each OAuth
platform is in [docs/oauth-connectors.md](./oauth-connectors.md).

## Localisation

User-facing API messages live in `PostyFox.Application/Resources/Messages.resx` (en-GB, the
default) and are used through the generated `Messages` class. Both APIs pick the culture from the
request's `Accept-Language` (`UsePostyFoxLocalization`), falling back to en-GB. To add a language,
add `Messages.<culture>.resx` and the culture to `WebExtensions.SupportedCultures`. Delivery errors
from platforms and connector field labels are not localised.

## Package pins

- Transitive packages are pinned explicitly rather than suppressing audit warnings:
  `Microsoft.OpenApi` 3.11.0 in the API projects and `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5 in the test
  projects. Recheck them when the framework packages move.
