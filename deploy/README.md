# PostyFox Docker Deployment

Quick reference for GitHub Actions-based Docker deployment with two isolated stacks (dev/prod).

## Quick Start

### 1. Server Setup (one-time)
Go through and check folders, env file etc is there.

### 2. Configure Environment
```bash
sudo nano /opt/postyfox/dev/.env      # Configure dev stack
sudo nano /opt/postyfox/prod/.env     # Configure prod stack
```

### 3. GitHub Secrets
Add to repository → Settings → Secrets. Split by GitHub Environment — `development` needs none of
these (the self-hosted runner deploys locally); `production` needs the Kubernetes/Helm secrets:

**`production` environment secrets** (Kubernetes/Helm deploy):
- `KUBE_CONFIG`: base64-encoded kubeconfig for the target cluster (a Service Account token scoped
  to the `postyfox` namespace is strongly recommended over a personal/admin credential)
- `DB_PASSWORD`, `RABBITMQ_PASSWORD`, `REDIS_PASSWORD`: passwords for the chart's own
  Postgres/RabbitMQ/Redis (only needed if you leave `postgres.enabled`/etc `true` in
  `values-prod.yaml` — see that file)
- `EXTERNAL_POSTGRES_CONNECTION`: full connection string, only used if `postgres.enabled: false`
- `VAULT_ROLE_ID` / `VAULT_SECRET_ID`: AppRole credentials pinned into the bundled Vault
- `INTERNAL_TOKEN`: shared token between core/worker and connectors-node
- `RUSTFS_ACCESS_KEY` / `RUSTFS_SECRET_KEY`: object store credentials
- `OIDC_CLIENT_SECRET`, `OAUTH2_PROXY_COOKIE_SECRET`: OIDC edge secrets

These are rendered into a workflow-local, gitignored values file at deploy time (never committed —
see the "Render secret overrides" step in `release.yml`/`deploy-manual.yml`) and layered on top of
`deploy/helm/postyfox/values-prod.yaml`, which holds the non-secret cluster configuration
(hostnames, ingress class, replica counts, toggles).

### 4. Self-hosted runner
Install a Linux self-hosted GitHub Actions runner on the dev deployment host. Dev deploy jobs run
there directly (docker-compose); production deploys run on GitHub-hosted runners against the
Kubernetes cluster via `helm upgrade`.

### 5. GitHub Environments
Create in repository → Settings → Environments:
- `development`: auto-deploy from main on the self-hosted runner
- `production`: requires approval for manual deployment

## Deployment Flow

```
Push to main
    ↓
platform-ci.yml: test, build, push images
    ↓
    └─→ Dev: self-hosted runner deploy (docker-compose) ✅

release.yml (manual dispatch, semver)
    ↓
    ├─→ Dev: self-hosted runner deploy (docker-compose) ✅
    │
    └─→ Prod: wait for approval → helm upgrade --install (Kubernetes) ⏳
```

Production is a **separate Kubernetes deployment target** from dev — dev stays on docker-compose
(self-hosted runner, single host); prod is a Helm chart (`deploy/helm/postyfox`) deployed into a
`postyfox` namespace on a real cluster. See [Kubernetes / Helm (Production)](#kubernetes--helm-production) below.

## Key Files

| File | Purpose |
|------|---------|
| `.github/workflows/deploy.yml` | Auto deploy: dev on self-hosted runner (docker-compose) |
| `.github/workflows/deploy-manual.yml` | Manual deploy: dev on self-hosted runner, prod via Helm |
| `.github/workflows/release.yml` | Semver release: dev via docker-compose, prod via Helm (approval-gated) |
| `deploy/docker-compose.server.yml` | Dev-stack deployment base composition |
| `deploy/docker-compose.dev.yml` | Dev overrides (isolation, lighter resources) |
| `deploy/.env.dev.example` | Dev configuration template |
| `deploy/vault/config/vault.hcl` | Vault server config (file backend, Shamir seal; also ported into the Helm chart's vault ConfigMap) |
| `deploy/vault/bootstrap.sh` | Vault init + auto-unseal sidecar script (also ported into the Helm chart) |
| `deploy/helm/postyfox/` | Production Helm chart (full stack: apps + optional Postgres/RabbitMQ/Redis/Vault/otel-collector/gateway/oauth2-proxy) |
| `deploy/helm/postyfox/values.yaml` | Chart defaults — generic, reusable by any deployer |
| `deploy/helm/postyfox/values-prod.yaml` | Non-secret overlay for the real PostyFox cluster |
| `deploy/DEPLOYMENT.md` | Full deployment guide |

## Stack Features

### Development
- Auto-deploy on every successful build
- Runs directly on a self-hosted Linux GitHub Actions runner
- Single instances of each service
- Shared auth/storage (external Keycloak, RustFS)
- Isolated postgres & rabbitmq (dev-specific)
- Self-initialising, auto-unsealed HashiCorp Vault (internal)
- Lighter resource constraints

### Production
- Manual approval before deploy (`production` GitHub Environment)
- **Kubernetes deployment via Helm** (`deploy/helm/postyfox`), not docker-compose — see
  [Kubernetes / Helm (Production)](#kubernetes--helm-production) below
- Replicated core-api/post-api (HA)
- Shared cluster Postgres (toggleable — disable the chart's own and point at yours), shared
  Keycloak + RustFS
- Bundled RabbitMQ/Redis/Vault (each independently toggleable for bring-your-own)
- Production resource limits and health checks
- Observability enabled (OTel)
- Deployed as its own release, independent of the frontend's Helm release (same namespace)

## Common Commands (Development)

### Check Status
### Check Status
```bash
ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml ps"
```

### View Logs
```bash
ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml logs -f core-api"
```

### Stop/Start
```bash
ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml stop"
ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml start"
```

### Manual Redeploy
```bash
ssh deploy@server "cd /opt/postyfox/dev && \
  source .env && \
  docker compose -f docker-compose.server.yml -f docker-compose.dev.yml pull && \
  docker compose -f docker-compose.server.yml -f docker-compose.dev.yml up -d"
```

## Maintenance Mode

### Development (docker-compose)
The stack shows a maintenance page instead of a raw error in three situations. The first is a
manual toggle for planned deploys; the other two are automatic and need no action:

- **Planned maintenance (manual)** — the `gateway` service checks for
  `deploy/gateway/maintenance/maintenance.flag` on every request (no reload needed). Create it
  before a deploy and remove it after:
  ```bash
  # Turn maintenance mode ON (put this before stopping/redeploying services)
  ssh deploy@server "touch /opt/postyfox/dev/gateway/maintenance/maintenance.flag"

  # Turn maintenance mode OFF (once the new version is up and healthy)
  ssh deploy@server "rm /opt/postyfox/dev/gateway/maintenance/maintenance.flag"
  ```
  This returns a `503` with the maintenance page for all traffic, regardless of backend health.
- **Backend outage (automatic)** — if core-api/post-api are unreachable (e.g. mid-restart), the
  `gateway` service's `error_page` directive serves the same maintenance page for the resulting
  `502`/`503`/`504`, with the original status code preserved.
- **Gateway outage (automatic)** — if `gateway` itself is unreachable, oauth2-proxy's own custom
  `error.html` (`deploy/oauth2-proxy/templates/`) renders the same maintenance branding.

The maintenance page content lives at `deploy/gateway/maintenance/maintenance.html` — edit it to
change the wording/branding shown to users.

### Production (Kubernetes)
Same design (planned-maintenance toggle + automatic backend/gateway-outage fallback), but the
manual toggle is a declarative Helm value instead of touching a file on a host:

```bash
helm upgrade postyfox deploy/helm/postyfox -n postyfox --reuse-values --set gateway.maintenanceMode=true
# ...once the new version is up and healthy...
helm upgrade postyfox deploy/helm/postyfox -n postyfox --reuse-values --set gateway.maintenanceMode=false
```

The page content is `deploy/helm/postyfox/files/gateway/maintenance.html`, packaged into the
gateway's ConfigMap by the chart.

## Ports

### Development (docker-compose)
Only the OIDC edge (oauth2-proxy) publishes a host port — the APIs, gateway, and connectors-node stay
on the internal network and are reached through the edge. The edge port is configured per stack in
`.env`:

- `EDGE_PORT` (default `4180`) — put your TLS terminator / load balancer in front of it.

All public traffic goes to `http://<host>:${EDGE_PORT}`, which authenticates via Keycloak and
path-routes `/api/posts` + `/api/webhooks` to post-api and everything else to core-api.

### Production (Kubernetes)
Only oauth2-proxy is reachable at all — via the chart's `Ingress` resource (`ingress.enabled: true`
in `values-prod.yaml`), terminated by whatever ingress controller/TLS setup your cluster already
has (cert-manager annotation + `ingress.className` are both configurable). core-api/post-api/
connectors-node/gateway/backing services are all `ClusterIP`-only — no other host ports or
NodePorts are opened by the chart.

## External Dependencies

### Development (docker-compose)
Both stacks share these external services (configure in `.env`):

- **Keycloak**: Authentication (OAuth2/OIDC)
- **RustFS**: Object storage (S3-compatible)

Ensure these are:
- Accessible from deployment server network
- Configured with PostyFox realms/buckets
- Properly secured and backed up

### Production (Kubernetes)
The Helm chart never deploys Keycloak or RustFS — point `config.authOidc*`/`oauth2Proxy.*` and
`config.objectStoreServiceUrl` (in `values-prod.yaml`) at your existing in-cluster or external
instances. Postgres/RabbitMQ/Redis/Vault are each optionally bundled by the chart
(`*.enabled` toggles) or can likewise point at your own existing infrastructure.

## Secret store

Connector, platform, and trigger-signing secrets are stored via the
[`adapters-secrets`](https://github.com/aneillans/adapters-secrets) library (`ISecretsProvider`),
not a database. The backend is selected with `SECRETS_PROVIDER` (→ `Secrets__Provider`):

| Provider | `SECRETS_PROVIDER` | Notes |
|----------|--------------------|-------|
| HashiCorp Vault | `HashiCorpVault` | KV v2 engine, AppRole/token auth. Default for the docker **dev/prod** stacks (bundled `vault` service) and the **Helm** chart. |
| Azure Key Vault | `AzureKeyVault` | Default for the **Terraform / ACA** deployment (pairs with the Container App's managed identity). |
| BitWarden / VaultWarden | `BitWarden` | Selectable everywhere. **Delete is unsupported** (best-effort cleanup only). |
| Infisical | `Infisical` | Selectable everywhere. |
| In-memory | `InMemory` | Non-persistent — secrets are lost on restart. Default for the base `docker-compose.yml` and bare local runs. |

Per-deployment defaults:

| Deployment | Default provider |
|------------|------------------|
| `docker-compose.yml` (base / bare) | `InMemory` |
| dev/prod compose (`+ docker-compose.{dev,prod}.yml`) | `HashiCorpVault` (bundled) |
| Helm (`deploy/helm`) | `HashiCorpVault` (external Vault) |
| Terraform / ACA (`deploy/terraform-aca`) | `AzureKeyVault` |

Options are passed as env vars named `Secrets__<Provider>__<Option>`. The dev/prod compose files and
`.env` templates wire the HashiCorp Vault set (see the [HashiCorp Vault](#hashicorp-vault) section):

```
SECRETS_PROVIDER=HashiCorpVault
VAULT_ROLE_ID=...        # → Secrets__HashiCorpVault__RoleId    (AppRole)
VAULT_SECRET_ID=...      # → Secrets__HashiCorpVault__SecretId  (AppRole)
VAULT_MOUNT=secret       # → Secrets__HashiCorpVault__MountPoint
VAULT_BASE_PATH=postyfox # → Secrets__HashiCorpVault__BasePath
# VaultAddress defaults to the internal http://vault:8200; a Token may be used instead of an AppRole.
```

For BitWarden set `BITWARDEN_SERVER_URL`/`BITWARDEN_API_KEY` (→ `Secrets__BitWarden__ServerUrl`/`ApiKey`),
or an Organization API key (`BITWARDEN_CLIENT_ID`/`CLIENT_SECRET`/`ORGANIZATION_ID`/`IDENTITY_URL`).
For Azure Key Vault set `Secrets__AzureKeyVault__VaultUri` (+ optional `TenantId`/`ClientId`/`ClientSecret`);
for Infisical set `Secrets__Infisical__ClientId`/`ClientSecret`/`ProjectId`/`Environment` (+ `SiteUrl`/`SecretPath`).

> Connector operational secrets can be managed in the admin UI by a user with the Keycloak
> `postyfox-admin` realm role. The current catalog is `TelegramApiID`/`TelegramApiHash` and
> `TumblrConsumerKey`/`TumblrConsumerSecret`. Trigger keys (`trigger-{sourceType}-signing`) still
> require direct seeding.

For an external Keycloak realm, ensure the oauth2-proxy client includes realm roles in its ID token.
The built-in `roles` client scope must be assigned with **Full scope allowed**, or add a
**User Realm Role** mapper with token claim name `realm_access.roles`, multivalued enabled, and
**Add to ID token** enabled. Core authorizes the validated ID token forwarded by oauth2-proxy; merely
assigning the role to a user is insufficient if the client does not emit it. Sign out and back in
after changing role assignments or mappers so oauth2-proxy receives a new ID token.

oauth2-proxy uses the bundled internal Redis service for server-side sessions. This keeps the
ID/access/refresh tokens out of browser cookies, which otherwise exceed the 4 KB cookie limit once
Keycloak role claims are present. Deployed stacks require a URL-safe
`OAUTH2_PROXY_REDIS_PASSWORD`; generate one with `openssl rand -hex 32`. Redis persistence is
intentionally disabled: losing it signs users out but does not lose application data.

## HashiCorp Vault

Both the dev docker-compose stack AND the production Helm chart include a self-managing HashiCorp
Vault (docker-compose: `vault` service in `docker-compose.server.yml`; Helm: `templates/vault.yaml`,
ported near-verbatim from the same `vault.hcl`/`bootstrap.sh`). It uses the file storage backend
with the default Shamir seal and stays on the internal network — like the APIs, it never publishes
a host port / has no Ingress rule.

Both default to this bundled Vault as their secret store (`SECRETS_PROVIDER=HashiCorpVault` /
`config.secretsProvider: HashiCorpVault`). A companion `vault-init` sidecar handles everything with
no manual step, identically in both deployments:

1. On first boot it runs `vault operator init` and writes the generated **unseal keys + root token**
   to `init.json` on the `vaultkeys` volume.
2. It then watches Vault and re-applies those saved keys whenever it is found sealed — first boot,
   after a `docker compose restart`, or after a crash — so the stack always comes up unsealed.
3. Once unsealed it **provisions the app's secret store**: a KV v2 mount (`VAULT_MOUNT`, default
   `secret`), a scoped policy over `VAULT_BASE_PATH` (default `postyfox`), and an **AppRole** whose
   RoleId/SecretId are *pinned* to `VAULT_ROLE_ID` / `VAULT_SECRET_ID`. The API/worker containers
   authenticate with those same two values (`Secrets__HashiCorpVault__RoleId`/`SecretId`) — so no
   token has to be handed off at runtime. The app services wait on `vault-init` being healthy before
   they start.

Volumes (named per stack):

| Volume | Mount | Purpose |
|--------|-------|---------|
| `vaultdata` (`postyfox-{dev,prod}-vault-data`) | `/vault/file` | Encrypted Vault storage |
| `vaultkeys` (`postyfox-{dev,prod}-vault-keys`) | `/vault/init` | Generated unseal keys + root token |

Tunables (in `.env`):

```
VAULT_VERSION=2.0.4     # Vault image tag
VAULT_KEY_SHARES=5       # Shamir key shares generated on first init
VAULT_KEY_THRESHOLD=3    # shares required to unseal
VAULT_ROLE_ID=...        # AppRole RoleId — pinned by vault-init, used by the app
VAULT_SECRET_ID=...      # AppRole SecretId — pinned by vault-init, used by the app (keep secret)
VAULT_MOUNT=secret       # KV v2 mount the app's secrets live under
VAULT_BASE_PATH=postyfox # path prefix within the mount
```

> Set `VAULT_ROLE_ID` + `VAULT_SECRET_ID` to strong random values (e.g. `openssl rand -hex 24`)
> before first boot. To point the stack at a different store instead, set `SECRETS_PROVIDER` to
> another provider and leave the AppRole vars empty — provisioning then becomes a no-op (the `vault`
> service still runs, just unused).

Reach it from another container (e.g. the root token / status):

```bash
docker compose -f docker-compose.server.yml -f docker-compose.dev.yml exec vault sh -c 'VAULT_ADDR=http://127.0.0.1:8200 vault status'
docker compose -f docker-compose.server.yml -f docker-compose.dev.yml exec vault-init cat /vault/init/init.json
```

> ⚠️ **Security trade-off.** Storing the unseal keys next to the server is what makes unattended
> unsealing possible — it trades Shamir key-splitting for convenience. Back up and tightly restrict
> the `vaultkeys` volume. For a stronger posture, switch Vault to a Transit / cloud-KMS auto-unseal
> seal and remove the `vault-init` sidecar.

## Kubernetes / Helm (Production)

The prod release deploys `deploy/helm/postyfox` as a Helm release named `postyfox` in the
`postyfox` namespace. `values-prod.yaml` (committed, no secrets) holds the cluster-specific
non-secret config; real secret values are supplied at deploy time from GitHub Environment secrets
(see [GitHub Secrets](#3-github-secrets) above) and never touch the repo.

```bash
# Manual helm invocation equivalent to what the pipeline runs (secrets supplied separately):
helm upgrade --install postyfox deploy/helm/postyfox \
  --namespace postyfox --create-namespace \
  -f deploy/helm/postyfox/values-prod.yaml \
  -f <your-untracked-secrets-values.yaml> \
  --set image.tag=v1.2.3 \
  --atomic --wait --timeout 5m

# Status / logs
kubectl -n postyfox get pods
kubectl -n postyfox logs deploy/postyfox-core-api -f
kubectl -n postyfox describe pod <pod-name>

# Rollback to the previous release (or a specific revision)
helm -n postyfox history postyfox
helm -n postyfox rollback postyfox            # previous revision
helm -n postyfox rollback postyfox <revision>  # specific revision

# Uninstall (⚠️ deletes all chart-managed resources, including any bundled Postgres/RabbitMQ/
# Vault PVCs unless retained by StorageClass reclaim policy)
helm -n postyfox uninstall postyfox
```

`--atomic` means a failed `helm upgrade` (e.g. a container that never becomes ready) automatically
rolls back to the previous working release — the pipeline's "Helm upgrade" step already fails loud
if this happens.

See [`deploy/helm/postyfox/values.yaml`](./helm/postyfox/values.yaml) for every configurable
option (all backing services are individually toggleable for self-contained vs bring-your-own),
and [`values-prod.yaml`](./helm/postyfox/values-prod.yaml) for this project's own overlay.

The frontend SPA (`postyfox-frontend` repo) deploys as its **own, independent** Helm release
(`deploy/helm/postyfox-frontend` in that repo) into the same `postyfox` namespace — see that
repo's README for details. This chart's gateway proxies `/` to it when
`gateway.frontend.enabled: true`, degrading gracefully to the maintenance page if that Service
isn't present/ready yet.

## Troubleshooting

### Development deploy fails
1. Check GitHub Actions logs: https://github.com/yourorg/postyfox-core/actions
2. Check server logs: `ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml logs"`
3. Verify SSH access: `ssh -i deploy_key deploy@server "docker ps"`

### Development services won't start
```bash
# Check resources available
ssh deploy@server "docker stats"

# Check postgres health
ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml exec postgres pg_isready -U postyfox"

# Check network connectivity to external services
ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml exec core-api getent hosts rustfs.example.com"
```

### Production (Kubernetes) deploy fails
1. Check GitHub Actions logs (the `production` environment's approval gate + the `Helm upgrade`
   step's output — `--atomic` auto-rolls-back and reports the failure reason).
2. `kubectl -n postyfox get events --sort-by=.lastTimestamp | tail -30`
3. `kubectl -n postyfox describe pod <pod-name>` for a specific failing container (image pull
   errors, readiness probe failures, missing ConfigMap/Secret keys, etc.)
4. `helm -n postyfox status postyfox` / `helm -n postyfox get values postyfox` to see what was
   actually applied.

### Production rollback
See [Kubernetes / Helm (Production)](#kubernetes--helm-production) above — `helm rollback` is the
production equivalent of the dev `git log` + redeploy-by-SHA flow.

## See Also

- [Full Deployment Guide](./DEPLOYMENT.md)
- [Architecture](../docs/ARCHITECTURE.md)
- [Local Development](../README.md#run-locally)
