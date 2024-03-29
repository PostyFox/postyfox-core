
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