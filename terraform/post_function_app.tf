# Deploy a DotNet Core runtime Linux Function App
module "posting_function_app" {
  source = "github.com/aneillans/azure-flex-functionapp/terraform"

  storage_account_name = "${local.appname}funcpststr${var.environment}"
  function_app_name    = "${local.appname}-func-app-post${local.hyphen-env}"
  location             = azurerm_resource_group.rg.location
  resource_group_id    = azurerm_resource_group.rg.id
  resource_group_name  = azurerm_resource_group.rg.name
  plan_name            = "${local.appname}-flex_post${local.hyphen-env}"

  auth_client_id                       = var.func_app_registered_client_id
  auth_client_secret_setting_name      = "OPENID_PROVIDER_AUTHENTICATION_SECRET"
  auth_enabled                         = true
  auth_openid_well_known_configuration = var.auth_openid_well_known_configuration
  auth_require_authentication          = true
  auth_require_https                   = true
  auth_unauthentication_action         = "Return401"

  auth_login_token_store_enabled = true
  auth_login_token_refresh_hours = 72
  auth_login_validate_nonce      = true
  auth_login_logout_endpoint     = "/.auth/logout"

  cors_support_credentials = true
  cors_allowed_origins     = var.cors

  app_settings = [
    {
      name  = "PostingQueue__queueServiceUri",
      value = azurerm_storage_account.data_storage.primary_queue_endpoint
    },
    {
      name  = "PostingQueue",
      value = azurerm_storage_account.data_storage.primary_queue_endpoint
    },
    {
      name  = "ConfigTable",
      value = azurerm_storage_account.data_storage.primary_table_endpoint
    },
    {
      name  = "SecretStore",
      value = azurerm_key_vault.key_vault.vault_uri
    },
    {
      name  = "StorageAccount",
      value = azurerm_storage_account.data_storage.primary_blob_endpoint
    },
    {
      name  = "OPENID_PROVIDER_AUTHENTICATION_SECRET",
      value = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=clientsecret)"
    },
    {
      name  = "TwitchClientId",
      value = var.twitchClientId
    },
    {
      name  = "TwitchClientSecret",
      value = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=TwitchClientSecret)"
    },
    {
      name  = "TwitchSignatureSecret",
      value = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=TwitchSignatureSecret)"
    },
    {
      name  = "TwitchCallbackUrl",
      value = var.twitchCallbackUrl
    },
    {
      name  = "APPLICATIONINSIGHTS_CONNECTION_STRING",
      value = azurerm_application_insights.application_insights.connection_string
    }
  ]
}

resource "azurerm_app_service_custom_hostname_binding" "dotnet_funcpost_binding" {
  hostname            = "${local.portal-prefix}${local.posting-address}"
  app_service_name    = module.posting_function_app.name
  resource_group_name = azurerm_resource_group.rg.name

  lifecycle {
    ignore_changes = [ssl_state, thumbprint]
  }
}

resource "azurerm_app_service_managed_certificate" "dotnet_funcpost_cert" {
  custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.dotnet_funcpost_binding.id
}

resource "azurerm_app_service_certificate_binding" "dotnet_funcpost_cert_binding" {
  hostname_binding_id = azurerm_app_service_custom_hostname_binding.dotnet_funcpost_binding.id
  certificate_id      = azurerm_app_service_managed_certificate.dotnet_funcpost_cert.id
  ssl_state           = "SniEnabled"
}

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