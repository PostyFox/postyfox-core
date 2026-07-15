variable "resource_group_name" { type = string }
variable "location" {
  type    = string
  default = "uksouth"
}

# Published images: {registry}/{repository}-{service}:{tag}
variable "image_registry" {
  type    = string
  default = "ghcr.io"
}
variable "image_repository" {
  type    = string
  default = "your-org/postyfox"
}
variable "image_tag" {
  type    = string
  default = "latest"
}
variable "registry_server" {
  type        = string
  default     = ""
  description = "Container registry server for pull credentials (optional if public)."
}
variable "registry_username" {
  type    = string
  default = ""
}
variable "registry_password" {
  type      = string
  default   = ""
  sensitive = true
}

# Backing services (managed / external)
variable "object_store_service_url" { type = string }
variable "object_store_bucket" {
  type    = string
  default = "postyfox"
}
variable "rabbitmq_host" { type = string }
variable "rabbitmq_user" {
  type    = string
  default = "guest"
}
variable "otel_endpoint" {
  type    = string
  default = ""
}

# In-app OIDC bearer validation. DevMode does not exist in ACA, so the APIs trust the validated JWT
# the OIDC edge forwards (not any header). Point these at your Keycloak realm.
variable "auth_oidc_issuer" {
  type        = string
  description = "OIDC issuer (token `iss`), e.g. https://keycloak.example/realms/PostyFox"
}
variable "auth_oidc_jwks_url" {
  type        = string
  description = "JWKS URL for signing-key validation."
}
variable "auth_oidc_audience" {
  type    = string
  default = "oauth2-proxy"
}

# Secret store (Neillans.Adapters.Secrets). Non-secret provider options; credentials are below.
variable "secrets_provider" {
  type        = string
  default     = "BitWarden"
  description = "Secret store backend: InMemory | BitWarden | AzureKeyVault | Infisical."
}
variable "bitwarden_server_url" {
  type    = string
  default = ""
}
variable "bitwarden_organization_id" {
  type    = string
  default = ""
}
variable "bitwarden_identity_url" {
  type    = string
  default = ""
}

# Secrets
variable "postgres_connection" {
  type      = string
  sensitive = true
}
# Secret-store credentials for the selected secrets_provider (BitWarden shown). Supply EITHER
# bitwarden_api_key OR the Organization API key pair (bitwarden_client_id + bitwarden_client_secret).
variable "bitwarden_api_key" {
  type      = string
  default   = ""
  sensitive = true
}
variable "bitwarden_client_id" {
  type      = string
  default   = ""
  sensitive = true
}
variable "bitwarden_client_secret" {
  type      = string
  default   = ""
  sensitive = true
}
variable "internal_token" {
  type      = string
  sensitive = true
}
variable "object_store_access_key" {
  type      = string
  sensitive = true
}
variable "object_store_secret_key" {
  type      = string
  sensitive = true
}
variable "rabbitmq_password" {
  type      = string
  sensitive = true
}
