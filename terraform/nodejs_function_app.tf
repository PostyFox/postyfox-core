# Deploy a NodeJS runtime Linux Function App, which will predominately run workloads mutually shared with PostyBirb
module "nodejs_function_app" {
  source                        = "Azure/avm-res-web-site/azurerm"
  name                          = "${local.appname}-func-app-nodejs${local.hyphen-env}"
  resource_group_name           = azurerm_resource_group.rg.name
  location                      = azurerm_resource_group.rg.location
  kind                          = "functionapp"
  os_type                       = "Linux"
  service_plan_resource_id      = azurerm_service_plan.asp_flex.id
  storage_account_name          = azurerm_storage_account.funcapp_storage.name
  storage_container_endpoint    = "${azurerm_storage_account.funcapp_storage.primary_blob_endpoint}${azurerm_storage_container.nodejs_container.name}"
  storage_uses_managed_identity = true
  storage_container_type        = "blobContainer"
  enable_application_insights   = false # Use a shared AppInsights

  fc1_runtime_name      = "node"
  fc1_runtime_version   = "20"
  function_app_uses_fc1 = true

  enable_telemetry = true

  instance_memory_in_mb       = 2048
  storage_authentication_type = "SystemAssignedIdentity"

  managed_identities = {
    system_assigned = true
  }

  site_config = {
    application_insights_connection_string = azurerm_application_insights.application_insights.connection_string
    cors = {
      cors1 = {
        allowed_origins     = var.cors
        support_credentials = true
      }
    }
  }

  app_settings = {
    "PostingQueue__queueServiceUri"            = azurerm_storage_account.data_storage.primary_queue_endpoint
    "ConfigTable"                              = azurerm_storage_account.data_storage.primary_table_endpoint
    "SecretStore"                              = azurerm_key_vault.key_vault.vault_uri
    "StorageAccount"                           = azurerm_storage_account.data_storage.primary_blob_endpoint
    "AzureActiveDirectory_PROVIDER_AUTHENTICATION_SECRET" = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=clientsecret)"
    "SCM_DO_BUILD_DURING_DEPLOYMENT"           = "false"
    "AzureWebJobsDashboard__accountName"       = azurerm_storage_account.funcapp_storage.name
    "AzureWebJobsStorage__accountName"         = azurerm_storage_account.funcapp_storage.name
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

resource "azurerm_app_service_custom_hostname_binding" "nodejs_func_binding" {
  hostname            = "${local.portal-prefix}${local.nodejsapi-address}"
  app_service_name    = module.nodejs_function_app.name
  resource_group_name = azurerm_resource_group.rg.name

  lifecycle {
    ignore_changes = [ssl_state, thumbprint]
  }
}

resource "azurerm_app_service_managed_certificate" "nodejs_func_cert" {
  custom_hostname_binding_id = azurerm_app_service_custom_hostname_binding.nodejs_func_binding.id
}

resource "azurerm_app_service_certificate_binding" "nodejs_func_cert_binding" {
  hostname_binding_id = azurerm_app_service_custom_hostname_binding.nodejs_func_binding.id
  certificate_id      = azurerm_app_service_managed_certificate.nodejs_func_cert.id
  ssl_state           = "SniEnabled"
}


# # resource "azurerm_monitor_diagnostic_setting" "nodejs_func_app" {
# #   name                       = "${local.appname}-logging-app-nodejs${local.hyphen-env}"
# #   target_resource_id         = azurerm_linux_function_app.nodejs_func_app.id
# #   log_analytics_workspace_id = azurerm_log_analytics_workspace.log_analytics.id

# #   metric {
# #     category = "AllMetrics"
# #     enabled  = true
# #   }

# #   dynamic "enabled_log" {
# #     for_each = var.app_logs
# #     content {
# #       category = enabled_log.value
# #     }
# #   }
# # }

# # // Add application identity to Storage Blob Owner on storage account
# # resource "azurerm_role_assignment" "nodejsfuncapp-dataowner" {
# #   scope                = azurerm_storage_account.linux_func_storage.id
# #   role_definition_name = "Storage Blob Data Owner"
# #   principal_id         = azurerm_linux_function_app.nodejs_func_app.identity[0].principal_id
# # }

resource "azurerm_role_assignment" "nodejsfuncapp-storage-blob" {
  scope                = azurerm_storage_account.funcapp_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = module.nodejs_function_app.identity_principal_id
}

resource "azurerm_role_assignment" "nodejsfuncapp-data_storage-blob" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = module.nodejs_function_app.identity_principal_id
}

resource "azurerm_role_assignment" "nodejsfuncapp-data_storage-table" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = module.nodejs_function_app.identity_principal_id
}