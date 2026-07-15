# PostyFox Docker Deployment

Quick reference for GitHub Actions-based Docker deployment with two isolated stacks (dev/prod).

## Quick Start

### 1. Server Setup (one-time)
```bash
curl https://raw.githubusercontent.com/your-org/postyfox-core/main/deploy/setup-deploy.sh | sudo bash
```

### 2. Configure Environment
```bash
sudo nano /opt/postyfox/dev/.env      # Configure dev stack
sudo nano /opt/postyfox/prod/.env     # Configure prod stack
```

### 3. GitHub Secrets
Add to repository → Settings → Secrets:
- `DEPLOY_HOST`: server hostname
- `DEPLOY_USER`: SSH user
- `DEPLOY_SSH_KEY`: private SSH key
- `DEPLOY_PORT`: (optional) SSH port

### 4. GitHub Environments
Create in repository → Settings → Environments:
- `development`: auto-deploy from main
- `production`: requires approval for manual deployment

## Deployment Flow

```
Push to main
    ↓
platform-ci.yml: test, build, push images
    ↓
    ├─→ Dev: auto-deploy ✅
    │
    └─→ Prod: wait for approval → deploy ⏳
```

## Key Files

| File | Purpose |
|------|---------|
| `.github/workflows/deploy.yml` | Deployment workflow (GitHub Actions) |
| `deploy/docker-compose.server.yml` | Deployment base composition |
| `deploy/docker-compose.dev.yml` | Dev overrides (isolation, lighter resources) |
| `deploy/docker-compose.prod.yml` | Prod overrides (replication, HA, monitoring) |
| `deploy/.env.dev.example` | Dev configuration template |
| `deploy/.env.prod.example` | Prod configuration template |
| `deploy/DEPLOYMENT.md` | Full deployment guide |

## Stack Features

### Development
- Auto-deploy on every successful build
- Single instances of each service
- Shared auth/storage (external Keycloak, RustFS)
- Isolated postgres & rabbitmq (dev-specific)
- Lighter resource constraints

### Production
- Manual approval before deploy
- Replicated post-api and worker (HA)
- Shared auth/storage (external Keycloak, RustFS)
- Isolated postgres & rabbitmq (prod-specific)
- Production resource limits and health checks
- Observability enabled (OTel)

## Common Commands

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
not a database. The backend is selected per stack with `SECRETS_PROVIDER` (→ `Secrets__Provider`):

| Provider | `SECRETS_PROVIDER` | Notes |
|----------|--------------------|-------|
| In-memory | `InMemory` | Non-persistent — secrets are lost on restart. Local/dev default. |
| BitWarden / VaultWarden | `BitWarden` | Deployed default. **Delete is unsupported** (best-effort cleanup only). |
| Azure Key Vault | `AzureKeyVault` | — |
| Infisical | `Infisical` | — |

Options are passed as env vars named `Secrets__<Provider>__<Option>`. The compose files and
`.env` templates wire the BitWarden set:

```
SECRETS_PROVIDER=BitWarden
BITWARDEN_SERVER_URL=https://vault.example.com   # → Secrets__BitWarden__ServerUrl
BITWARDEN_API_KEY=...                            # → Secrets__BitWarden__ApiKey
# ...or an Organization API key (mutually exclusive with the API key):
BITWARDEN_CLIENT_ID=... / BITWARDEN_CLIENT_SECRET=... / BITWARDEN_ORGANIZATION_ID=... / BITWARDEN_IDENTITY_URL=...
```

For Azure Key Vault set `Secrets__AzureKeyVault__VaultUri` (+ optional `TenantId`/`ClientId`/`ClientSecret`);
for Infisical set `Secrets__Infisical__ClientId`/`ClientSecret`/`ProjectId`/`Environment` (+ `SiteUrl`/`SecretPath`).

> The chosen store must be seeded with the platform secrets the app expects —
> `TelegramApiID`/`TelegramApiHash` and each `trigger-{sourceType}-signing` key.

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

