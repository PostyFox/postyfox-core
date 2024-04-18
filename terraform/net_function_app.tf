# Deploy a DotNet Core runtime Linux Function App
resource "azurerm_linux_function_app" "dotnet_func_app" {
  name                = "${local.appname}-func-app-dotnet${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location

  https_only = true

  storage_account_name          = azurerm_storage_account.linux_funcnet_storage.name
  storage_uses_managed_identity = true
  service_plan_id               = azurerm_service_plan.linux_func_service_plan.id

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    "ConfigTable"    = azurerm_storage_account.data_storage.primary_table_endpoint
    "SecretStore"    = azurerm_key_vault.key_vault.vault_uri
    "StorageAccount" = azurerm_storage_account.data_storage.primary_blob_endpoint
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
      support_credentials = true
    }
  }

  auth_settings_v2 {
    auth_enabled             = true
    forward_proxy_convention = "NoProxy"
    http_route_api_prefix    = "/.auth"
    require_authentication   = true
    require_https            = true
    runtime_version          = "~1"
    unauthenticated_action   = "RedirectToLoginPage"
    default_provider         = "AAD_B2C"

    custom_oidc_v2 {
      name = "AAD_B2C"
      client_id = var.func_app_registered_client_id
      openid_configuration_endpoint = var.openid_configuration_endpoint
    }

    login {
      cookie_expiration_convention      = "FixedTime"
      cookie_expiration_time            = "08:00:00"
      logout_endpoint                   = "/.auth/logout"
      nonce_expiration_time             = "00:05:00"
      preserve_url_fragments_for_logins = false
      token_refresh_extension_time      = 72
      token_store_enabled               = true
      validate_nonce                    = true
    }
  }

  lifecycle {
    ignore_changes = [ 
      app_settings["WEBSITE_ENABLE_SYNC_UPDATE_SITE"],
      app_settings["WEBSITE_RUN_FROM_PACKAGE"]
    ]
  }
}

// Logging

resource "azurerm_monitor_diagnostic_setting" "dotnet_func_app" {
  name                       = "${local.appname}-logging-app-dotnet${local.hyphen-env}"
  target_resource_id         = azurerm_linux_function_app.dotnet_func_app.id
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
resource "azurerm_role_assignment" "dotnetfuncapp-dataowner" {
  scope                = azurerm_storage_account.linux_funcnet_storage.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "dotnetfuncapp-dataowner-dat" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "dotnetfuncapp-table" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}