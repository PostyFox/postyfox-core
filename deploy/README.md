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
| `deploy/docker-compose.yml` | Base composition |
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
ssh deploy@server "cd /opt/postyfox/dev && docker compose ps"
```

### View Logs
```bash
ssh deploy@server "cd /opt/postyfox/dev && docker compose logs -f core-api"
```

### Stop/Start
```bash
ssh deploy@server "cd /opt/postyfox/dev && docker compose stop"
ssh deploy@server "cd /opt/postyfox/dev && docker compose start"
```

### Manual Redeploy
```bash
ssh deploy@server "cd /opt/postyfox/dev && \
  source .env && \
  docker compose pull && \
  docker compose up -d"
```

## Ports

### Dev Stack
- Core API: 8080
- Post API: 8081
- RabbitMQ Admin: 15672
- OpenTelemetry: 4317/4318

### Prod Stack
- Core API: 9080 (or configured)
- Post API: 9081 (or configured)
- RabbitMQ Admin: 15673 (or configured)
- OpenTelemetry: 4317/4318

## External Dependencies

Both stacks share these external services (configure in `.env`):

- **Keycloak**: Authentication (OAuth2/OIDC)
- **RustFS**: Object storage (S3-compatible)

Ensure these are:
- Accessible from deployment server network
- Configured with PostyFox realms/buckets
- Properly secured and backed up

## Troubleshooting

### Deployment fails
1. Check GitHub Actions logs: https://github.com/yourorg/postyfox-core/actions
2. Check server logs: `ssh deploy@server "cd /opt/postyfox/dev && docker compose logs"`
3. Verify SSH access: `ssh -i deploy_key deploy@server "docker ps"`

### Services won't start
```bash
# Check resources available
ssh deploy@server "docker stats"

# Check postgres health
ssh deploy@server "docker exec postyfox-dev-postgres pg_isready -U postyfox"

# Check network connectivity to external services
ssh deploy@server "docker exec postyfox-dev-core-api nslookup rustfs"
```

### Rollback to previous version
```bash
# Get previous SHA from git
git log --oneline -5

# Redeploy with specific image
ssh deploy@server "cd /opt/postyfox/prod && \
  IMAGE_TAG=<previous-sha> docker compose pull && \
  docker compose up -d"
```

## See Also

- [Full Deployment Guide](./DEPLOYMENT.md)
- [Architecture](../docs/ARCHITECTURE.md)
- [Local Development](../README.md#run-locally)

