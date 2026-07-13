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

# Secrets
variable "postgres_connection" {
  type      = string
  sensitive = true
}
variable "encryption_key" {
  type        = string
  sensitive   = true
  description = "Base64 32-byte AES key for the secret store."
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
