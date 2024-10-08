# Deploy a DotNet Core runtime Linux Function App
module "posting_function_app" {
  source = "github.com/aneillans/azure-flex-functionapp/terraform"

  storage_account_name = "${local.appname}funcpststr${var.environment}"
  function_app_name    = "${local.appname}-func-app-post${local.hyphen-env}"
  location             = azurerm_resource_group.rg.location
  resource_group_id    = azurerm_resource_group.rg.id
  resource_group_name  = azurerm_resource_group.rg.name
  plan_name            = "${local.appname}-flex_asp${local.hyphen-env}"
  app_service_plan     = "${local.appname}-flex_asp${local.hyphen-env}"
}

# app_settings = {
#   "PostingQueue__queueServiceUri"          = azurerm_storage_account.data_storage.primary_queue_endpoint
#   "PostingQueue"                           = azurerm_storage_account.data_storage.primary_queue_endpoint
#   "ConfigTable"                            = azurerm_storage_account.data_storage.primary_table_endpoint
#   "SecretStore"                            = azurerm_key_vault.key_vault.vault_uri
#   "StorageAccount"                         = azurerm_storage_account.data_storage.primary_blob_endpoint
#   "AAD_B2C_PROVIDER_AUTHENTICATION_SECRET" = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=clientsecret)"
#   "TwitchClientId"                         = var.twitchClientId
#   "TwitchClientSecret"                     = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=TwitchClientSecret)"
#   "TwitchSignatureSecret"                  = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=TwitchSignatureSecret)"
#   "TwitchCallbackUrl"                      = var.twitchCallbackUrl
#   "WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED" = 1
#   "SCM_DO_BUILD_DURING_DEPLOYMENT"         = "false"
# }


# application_insights_connection_string = azurerm_application_insights.application_insights.connection_string

# cors {
#   allowed_origins = ["*"]
# }


# resource "azurerm_app_service_custom_hostname_binding" "dotnet_funcpost_binding" {
#   hostname            = "${local.portal-prefix}${local.posting-address}"
#   app_service_name    = azurerm_linux_function_app.dotnet_funcpost_app.name
#   resource_group_name = azurerm_resource_group.rg.name

#   lifecycle {
#     ignore_changes = [ssl_state, thumbprint]
#   }
# }

# resource "azurerm_app_service_managed_certificate" "dotnet_funcpost_cert" {
#   custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.dotnet_funcpost_binding.id
# }

# resource "azurerm_app_service_certificate_binding" "dotnet_funcpost_cert_binding" {
#   hostname_binding_id = azurerm_app_service_custom_hostname_binding.dotnet_funcpost_binding.id
#   certificate_id      = azurerm_app_service_managed_certificate.dotnet_funcpost_cert.id
#   ssl_state           = "SniEnabled"
# }

# // Logging

# resource "azurerm_monitor_diagnostic_setting" "dotnet_funcpost_app" {
#   name                       = "${local.appname}-logging-app-dotnetpost${local.hyphen-env}"
#   target_resource_id         = azurerm_linux_function_app.dotnet_funcpost_app.id
#   log_analytics_workspace_id = azurerm_log_analytics_workspace.log_analytics.id

#   metric {
#     category = "AllMetrics"
#     enabled  = true
#   }

#   dynamic "enabled_log" {
#     for_each = var.app_logs
#     content {
#       category = enabled_log.value
#     }
#   }
# }

# resource "azurerm_role_assignment" "funcpost-data-posting" {
#   scope                = azurerm_storage_account.linux_funcpost_storage.id
#   role_definition_name = "Storage Blob Data Contributor"
#   principal_id         = azurerm_linux_function_app.dotnet_funcpost_app.identity[0].principal_id
# }

# resource "azurerm_role_assignment" "funcpost-queue-posting" {
#   scope                = azurerm_storage_account.linux_funcpost_storage.id
#   role_definition_name = "Storage Queue Data Contributor"
#   principal_id         = azurerm_linux_function_app.dotnet_funcpost_app.identity[0].principal_id
# }