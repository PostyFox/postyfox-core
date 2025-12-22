# Deploy a DotNet Core runtime Linux Function App
module "dotnet_function_app" {
  source = "github.com/aneillans/azure-flex-functionapp/terraform"

  storage_account_name = "${local.appname}funcnetstr${var.environment}"
  function_app_name    = "${local.appname}-func-app-dotnet${local.hyphen-env}"
  location             = azurerm_resource_group.rg.location
  resource_group_id    = azurerm_resource_group.rg.id
  resource_group_name  = azurerm_resource_group.rg.name
  plan_name            = "${local.appname}-flex_net${local.hyphen-env}"

  auth_client_id                       = var.oidc_client_id
  auth_client_secret_setting_name      = "OPENID_PROVIDER_AUTHENTICATION_SECRET"
  auth_enabled                         = true
  auth_openid_well_known_configuration = var.openid_configuration_endpoint
  auth_require_authentication          = false
  auth_require_https                   = true
  auth_unauthentication_action         = "AllowAnonymous"

  auth_login_token_store_enabled = true
  auth_login_token_refresh_hours = 72
  auth_login_validate_nonce      = true
  auth_login_logout_endpoint     = var.logout_endpoint

  runtime         = "dotnet-isolated"
  runtime_version = "10.0"

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

resource "azurerm_app_service_custom_hostname_binding" "dotnet_func_binding" {
  hostname            = "${local.portal-prefix}${local.mainapi-address}"
  app_service_name    = module.dotnet_function_app.name
  resource_group_name = azurerm_resource_group.rg.name

  lifecycle {
    ignore_changes = [ssl_state, thumbprint]
  }
}

resource "azurerm_app_service_managed_certificate" "dotnet_func_cert" {
  custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.dotnet_func_binding.id
}

resource "azurerm_app_service_certificate_binding" "dotnet_func_cert_binding" {
  hostname_binding_id = azurerm_app_service_custom_hostname_binding.dotnet_func_binding.id
  certificate_id      = azurerm_app_service_managed_certificate.dotnet_func_cert.id
  ssl_state           = "SniEnabled"
}

# // Logging

# resource "azurerm_monitor_diagnostic_setting" "dotnet_func_app" {
#   name                       = "${local.appname}-logging-app-dotnet${local.hyphen-env}"
#   target_resource_id         = azurerm_linux_function_app.dotnet_func_app.id
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

# // Permissions ...

# // - Func App Account 
# resource "azurerm_role_assignment" "dotnetfuncapp-data" {
#   scope                = azurerm_storage_account.linux_funcnet_storage.id
#   role_definition_name = "Storage Blob Data Contributor"
#   principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
# }

# resource "azurerm_role_assignment" "dotnetfuncapp-table" {
#   scope                = azurerm_storage_account.linux_funcnet_storage.id
#   role_definition_name = "Storage Table Data Contributor"
#   principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
# }

# // - Data Account
resource "azurerm_role_assignment" "dotnetfuncapp-data_storage-blob" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = module.dotnet_function_app.identity_id
}

resource "azurerm_role_assignment" "dotnetfuncapp-data_storage-table" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = module.dotnet_function_app.identity_id
}