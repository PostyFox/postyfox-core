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
Add to repository → Settings → Secrets (required for production/manual remote deploys):
- `DEPLOY_HOST`: server hostname
- `DEPLOY_USER`: SSH user
- `DEPLOY_SSH_KEY`: private SSH key
- `DEPLOY_PORT`: (optional) SSH port

### 4. Self-hosted runner
Install a Linux self-hosted GitHub Actions runner on the dev deployment host. Dev deploy jobs run there directly; production still uses SSH from GitHub-hosted runners.

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
    ├─→ Dev: self-hosted runner deploy ✅
    │
    └─→ Prod: wait for approval → deploy ⏳
```

## Key Files

| File | Purpose |
|------|---------|
| `.github/workflows/deploy.yml` | Auto deploy: dev on self-hosted runner, prod via SSH |
| `.github/workflows/deploy-manual.yml` | Manual deploy: dev on self-hosted runner, prod via SSH |
| `deploy/docker-compose.server.yml` | Deployment base composition |
| `deploy/docker-compose.dev.yml` | Dev overrides (isolation, lighter resources) |
| `deploy/docker-compose.prod.yml` | Prod overrides (replication, HA, monitoring) |
| `deploy/.env.dev.example` | Dev configuration template |
| `deploy/.env.prod.example` | Prod configuration template |
| `deploy/vault/config/vault.hcl` | Vault server config (file backend, Shamir seal) |
| `deploy/vault/bootstrap.sh` | Vault init + auto-unseal sidecar script |
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
- Manual approval before deploy
- Replicated post-api and worker (HA)
- Shared auth/storage (external Keycloak, RustFS)
- Isolated postgres & rabbitmq (prod-specific)
- Self-initialising, auto-unsealed HashiCorp Vault (internal)
- Production resource limits and health checks
- Observability enabled (OTel)

## Common Commands

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

## Ports

Only the OIDC edge (oauth2-proxy) publishes a host port — the APIs, gateway, and connectors-node stay
on the internal network and are reached through the edge. The edge port is configured per stack in
`.env`:

- `EDGE_PORT` (default `4180`) — put your TLS terminator / load balancer in front of it.

All public traffic goes to `http://<host>:${EDGE_PORT}`, which authenticates via Keycloak and
path-routes `/api/posts` + `/api/webhooks` to post-api and everything else to core-api.

## External Dependencies

Both stacks share these external services (configure in `.env`):

- **Keycloak**: Authentication (OAuth2/OIDC)
- **RustFS**: Object storage (S3-compatible)

Ensure these are:
- Accessible from deployment server network
- Configured with PostyFox realms/buckets
- Properly secured and backed up

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

> The chosen store must be seeded with the platform secrets the app expects —
> `TelegramApiID`/`TelegramApiHash` and each `trigger-{sourceType}-signing` key.

## HashiCorp Vault

Both the dev and prod stacks include a self-managing HashiCorp Vault (`vault` service), defined in
`docker-compose.server.yml` so it is shared by both overrides. It uses the file storage backend with
the default Shamir seal and stays on the internal network — like the APIs, it never publishes a host
port.

The dev/prod stacks default to this bundled Vault as their secret store
(`SECRETS_PROVIDER=HashiCorpVault`). A companion `vault-init` sidecar handles everything with no
manual step:

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
VAULT_VERSION=1.18       # Vault image tag
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

## Troubleshooting

### Deployment fails
1. Check GitHub Actions logs: https://github.com/yourorg/postyfox-core/actions
2. Check server logs: `ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml logs"`
3. Verify SSH access: `ssh -i deploy_key deploy@server "docker ps"`

### Services won't start
```bash
# Check resources available
ssh deploy@server "docker stats"

# Check postgres health
ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml exec postgres pg_isready -U postyfox"

# Check network connectivity to external services
ssh deploy@server "cd /opt/postyfox/dev && docker compose -f docker-compose.server.yml -f docker-compose.dev.yml exec core-api getent hosts rustfs.example.com"
```

### Rollback to previous version
```bash
# Get previous SHA from git
git log --oneline -5

# Redeploy with specific image
ssh deploy@server "cd /opt/postyfox/prod && \
  IMAGE_TAG=<previous-sha> docker compose -f docker-compose.server.yml -f docker-compose.prod.yml pull && \
  docker compose -f docker-compose.server.yml -f docker-compose.prod.yml up -d"
```

## See Also

- [Full Deployment Guide](./DEPLOYMENT.md)
- [Architecture](../docs/ARCHITECTURE.md)
- [Local Development](../README.md#run-locally)
