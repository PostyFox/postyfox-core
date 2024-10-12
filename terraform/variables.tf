
variable "environment" {
  type        = string
  description = "(Optional) Optional Environment to tag deployment assets with"
  default     = ""
}

variable "func_app_registered_client_id" {
  type        = string
  description = "(Required) The client ID of the application registered in the B2C tenant for the Func App auth to use"
}

variable "func_app_tenant_endpoint" {
  type        = string
  description = "(Required) The B2C tenant endpoint address"
}

variable "openid_configuration_endpoint" {
  type = string
}

variable "app_logs" {
  type = list(string)
}

variable "kv_logs" {
  type = list(string)
}

variable "cors" {
  type        = list(string)
  description = "(Required) CORS URLs which are applied to user facing Function Apps and Storage Accounts"
}

variable "allowed_ips" {
  type        = list(string)
  description = "(Optional) Defines the IP Addresses which can bypass the deny rules on resources to access KeyVault and Data Storage"
}

variable "twitchClientId" {
  type = string
}

variable "twitchCallbackUrl" {
  type = string
}

variable "auth_openid_well_known_configuration" {
  type = string
}