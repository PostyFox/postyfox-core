# Deploy a DotNet Core runtime Linux Function App
resource "azurerm_linux_function_app" "dotnet_funcpost_app" {
  name                = "${local.appname}-func-app-post${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location

  https_only = true

  storage_account_name          = azurerm_storage_account.linux_funcpost_storage.name
  storage_uses_managed_identity = true
  service_plan_id               = azurerm_service_plan.linux_func_service_plan.id

  identity {
    type = "SystemAssigned, UserAssigned"
    identity_ids = [ azurerm_user_assigned_identity.func_apps_uai.id ]
  }

  app_settings = {
    "ConfigTable"                            = azurerm_storage_account.data_storage.primary_table_endpoint
    "SecretStore"                            = azurerm_key_vault.key_vault.vault_uri
    "StorageAccount"                         = azurerm_storage_account.data_storage.primary_blob_endpoint
    "AAD_B2C_PROVIDER_AUTHENTICATION_SECRET" = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=clientsecret)"
  }

  site_config {
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime = true
    }

    application_insights_connection_string = azurerm_application_insights.application_insights.connection_string

    cors {
      allowed_origins = ["*"]
    }
  }

  lifecycle {
    ignore_changes = [
      app_settings["WEBSITE_ENABLE_SYNC_UPDATE_SITE"],
      app_settings["WEBSITE_RUN_FROM_PACKAGE"]
    ]
  }
}

resource "azurerm_app_service_custom_hostname_binding" "dotnet_funcpost_binding" {
  hostname            = "${local.portal-prefix}${local.posting-address}"
  app_service_name    = azurerm_linux_function_app.dotnet_funcpost_app.name
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

// Logging

resource "azurerm_monitor_diagnostic_setting" "dotnet_funcpost_app" {
  name                       = "${local.appname}-logging-app-dotnetpost${local.hyphen-env}"
  target_resource_id         = azurerm_linux_function_app.dotnet_funcpost_app.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_analytics.id

  metric {
    category = "AllMetrics"
    enabled  = true
  }

  dynamic "enabled_log" {
    for_each = var.app_logs
    content {
      category = enabled_log.value
    }
  }
}

// Add application identity to Storage Blob Owner on storage account
resource "azurerm_role_assignment" "dotnetfuncpostapp-dataowner" {
  scope                = azurerm_storage_account.linux_funcpost_storage.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_linux_function_app.dotnet_funcpost_app.identity[0].principal_id
}
