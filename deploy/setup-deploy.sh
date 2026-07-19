#!/bin/bash
# Server setup script for PostyFox Docker deployment
# Run this on the deployment server to initialize directory structure and permissions
# Usage: curl https://raw.githubusercontent.com/.../setup-deploy.sh | bash

set -e

echo "=== PostyFox Docker Deployment Server Setup ==="
echo ""

# Check if running as root or with sudo
if [[ $EUID -ne 0 ]]; then
   echo "This script must be run as root or with sudo"
   exit 1
fi

# Create deployment directories
echo "Creating deployment directories..."
mkdir -p /opt/postyfox/dev
mkdir -p /opt/postyfox/prod

# Set permissions
echo "Setting permissions..."
chmod 755 /opt/postyfox
chmod 755 /opt/postyfox/dev
chmod 755 /opt/postyfox/prod

# Create Docker networks for stack isolation
echo "Creating Docker networks..."
docker network create postyfox-dev || true
docker network create postyfox-prod || true

# Create volumes for data persistence
echo "Creating Docker volumes..."
docker volume create postyfox-dev-pgdata || true
docker volume create postyfox-dev-rabbitmq-data || true
docker volume create postyfox-prod-pgdata || true
docker volume create postyfox-prod-rabbitmq-data || true

echo ""
echo "=== Setup Complete ==="
echo ""
echo "Next steps:"
echo "1. On your deployment server:"
echo "   - Copy .env.dev.example to /opt/postyfox/dev/.env and configure"
echo "   - Copy .env.prod.example to /opt/postyfox/prod/.env and configure"
echo "   - Ensure external services (Keycloak, RustFS) are accessible"
echo ""
echo "2. In your GitHub repository settings:"
echo "   - Add repository secret: DEPLOY_HOST (deployment server hostname/IP)"
echo "   - Add repository secret: DEPLOY_USER (deployment user account)"
echo "   - Add repository secret: DEPLOY_SSH_KEY (SSH private key for deployment)"
echo "   - Add optional secret: DEPLOY_PORT (SSH port, default: 22)"
echo ""
echo "3. Create GitHub environments:"
echo "   - 'development' - auto-deploy on successful build via a self-hosted Linux runner"
echo "   - 'production' - requires manual approval"
echo "   - For development, install a self-hosted GitHub Actions runner on the dev host"
echo ""
echo "4. Configure health monitoring and backups for:"
echo "   - /opt/postyfox/dev/volumes/postgres"
echo "   - /opt/postyfox/prod/volumes/postgres"
echo "   - External RustFS storage"
echo ""
