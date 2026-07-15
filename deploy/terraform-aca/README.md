# Terraform → Azure Container Apps

Deploys the four PostyFox core services as Azure Container Apps, **consuming pre-published images**
(built + pushed by CI as `{registry}/{repository}-{service}:{tag}`). Backing services (PostgreSQL,
RabbitMQ, object storage) are expected to be managed/external and supplied via variables.

- `core-api`, `post-api` — external ingress (`:8080`)
- `connectors-node` — internal ingress (`:8090`)
- `worker` — no ingress
- `core-api` owns migrations + catalogue seeding

## Usage
```bash
cp terraform.tfvars.example terraform.tfvars   # fill in; keep secrets out of VCS
export TF_VAR_bitwarden_api_key=... TF_VAR_internal_token=...   # prefer env for secrets
terraform init
terraform plan
terraform apply
```

Secrets are stored as Container App secrets and referenced by env `secret_name`. For production,
consider Key Vault references / managed identity instead of plaintext `TF_VAR_*`.
