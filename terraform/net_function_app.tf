module "dotnet_function_app" {
  source                   = "Azure/avm-res-web-site/azurerm"
  name                     = "${local.appname}-func-app-dotnet${local.hyphen-env}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  kind                     = "functionapp"
  os_type                  = "Linux"
  service_plan_resource_id = azurerm_service_plan.dotnet_asp_flex.id

  fc1_runtime_name      = "dotnet-isolated"
  fc1_runtime_version   = "8.0"
  function_app_uses_fc1 = true

  enable_telemetry = true

  instance_memory_in_mb       = 2048
  storage_authentication_type = "SystemAssignedIdentity"
  # Do I want to predefine the storage  ?
  storage_container_type = "blobContainer"

  site_config = {
    cors = {
      cors1 = {
        allowed_origins     = var.cors
        support_credentials = true
      }
    }
  }

  app_settings = {
    "PostingQueue__queueServiceUri"         = azurerm_storage_account.data_storage.primary_queue_endpoint
    "PostingQueue"                          = azurerm_storage_account.data_storage.primary_queue_endpoint
    "ConfigTable"                           = azurerm_storage_account.data_storage.primary_table_endpoint
    "SecretStore"                           = azurerm_key_vault.key_vault.vault_uri
    "OPENID_PROVIDER_AUTHENTICATION_SECRET" = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=clientsecret)"
    "TwitchClientId"                        = var.twitchClientId
    "TwitchClientSecret"                    = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=TwitchClientSecret)"
    "TwitchSignatureSecret"                 = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=TwitchSignatureSecret)"
    "TwitchCallbackUrl"                     = var.twitchCallbackUrl
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = azurerm_application_insights.application_insights.connection_string
  }

  auth_settings_v2 = {
    entraAuth = {
      auth_enabled                  = true
      default_provider              = "AzureActiveDirectory"
      require_authentication        = false
      require_https                 = true
      unauthenticated_client_action = "AllowAnonymous"

      custom_oidc_v2 = {
        entra = {
          name                          = "AzureActiveDirectory"
          client_id                     = var.func_app_registered_client_id
          openid_configuration_endpoint = var.openid_configuration_endpoint
          client_secret_setting_name    = "OPENID_PROVIDER_AUTHENTICATION_SECRET"
        }
      }

      login = {
        entraLogin = {
          logout_endpoint               = "/.auth/logout"
          token_store_enabled           = true
          token_refresh_extension_hours = 72
          validate_nonce                = true
        }
      }
    }
  }
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
  principal_id         = module.dotnet_function_app.system_assigned_mi_principal_id
}

resource "azurerm_role_assignment" "dotnetfuncapp-data_storage-table" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = module.dotnet_function_app.system_assigned_mi_principal_id
}