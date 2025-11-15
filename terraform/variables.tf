
variable "environment" {
  type        = string
  description = "(Optional) Optional Environment to tag deployment assets with"
  default     = ""
}

variable "oidc_client_id" {
  type        = string
  description = "(Required) The client ID of the application registered with the OIDC provider for authentication"
}

variable "oidc_issuer" {
  type        = string
  description = "(Required) The OIDC issuer URL (e.g., https://auth.example.com/realms/myrealm)"
}

variable "openid_configuration_endpoint" {
  type        = string
  description = "(Required) The OIDC well-known configuration endpoint URL"
}

variable "kv_logs" {
  type = list(string)
}

variable "cors" {
  type        = list(string)
  description = "(Required) CORS URLs which are applied to user facing Function Apps and Storage Accounts"
}

variable "twitchClientId" {
  type = string
}

variable "twitchCallbackUrl" {
  type = string
}

variable "container_registry_url" {
  type        = string
  description = "(Optional) The URL of the container registry (e.g., myregistry.azurecr.io)"
  default     = ""
}

variable "container_registry_username" {
  type        = string
  description = "(Optional) The username for the container registry"
  default     = ""
}

variable "container_image_name" {
  type        = string
  description = "(Optional) The name of the container image to deploy"
  default     = "postyfox-app"
}

variable "container_image_tag" {
  type        = string
  description = "(Optional) The tag of the container image to deploy"
  default     = "latest"
}

variable "container_app_logs" {
  type        = list(string)
  description = "(Optional) List of log categories to enable for container app diagnostics"
  default     = ["ContainerAppConsoleLogs", "ContainerAppSystemLogs"]
}