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
    type = "SystemAssigned, UserAssigned"
    identity_ids = [ azurerm_user_assigned_identity.func_apps_uai.id ]
  }

  app_settings = {
    "PostingQueue__queueServiceUri"                 = azurerm_storage_account.data_storage.primary_queue_endpoint
    "PostingQueue"                                  = azurerm_storage_account.data_storage.primary_queue_endpoint        
    "ConfigTable"                                   = azurerm_storage_account.data_storage.primary_table_endpoint
    "SecretStore"                                   = azurerm_key_vault.key_vault.vault_uri
    "StorageAccount"                                = azurerm_storage_account.data_storage.primary_blob_endpoint
    "AAD_B2C_PROVIDER_AUTHENTICATION_SECRET"        = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=clientsecret)"
    "TwitchClientId"                                = var.twitchClientId
    "TwitchClientSecret"                            = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=TwitchClientSecret)"   
    "TwitchSignatureSecret"                         = "@Microsoft.KeyVault(VaultName=${local.appname}-kv${local.hyphen-env};SecretName=TwitchSignatureSecret)"    
    "TwitchCallbackUrl"                             = var.twitchCallbackUrl
    "WEBSITE_RUN_FROM_PACKAGE_BLOB_MI_RESOURCE_ID"  = azurerm_user_assigned_identity.func_apps_uai.id
    "MI_Resource_ID"                                = azurerm_user_assigned_identity.func_apps_uai.id
    "WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED"        = 1
    "SCM_DO_BUILD_DURING_DEPLOYMENT"                = "false"
  }

  site_config {
    application_stack {
      dotnet_version              = "8.0"
      use_dotnet_isolated_runtime = true
    }

    application_insights_connection_string = azurerm_application_insights.application_insights.connection_string

    cors {
      allowed_origins     = var.cors
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
    unauthenticated_action   = "Return401"
    default_provider         = "AAD_B2C"

    custom_oidc_v2 {
      name                          = "AAD_B2C"
      client_id                     = var.func_app_registered_client_id
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

resource "azurerm_app_service_custom_hostname_binding" "dotnet_func_binding" {
  hostname            = "${local.portal-prefix}${local.mainapi-address}"
  app_service_name    = azurerm_linux_function_app.dotnet_func_app.name
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

// Permissions ...

// - Func App Account 
resource "azurerm_role_assignment" "dotnetfuncapp-data" {
  scope                = azurerm_storage_account.linux_funcnet_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

// - Data Account

resource "azurerm_role_assignment" "dotnetfuncapp-data_storage-blob" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "dotnetfuncapp-data_storage-table" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

// - Posting Account

resource "azurerm_role_assignment" "dotnetfuncapp-data-posting" {
  scope                = azurerm_storage_account.linux_funcpost_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "dotnetfuncapp-queue-posting" {
  scope                = azurerm_storage_account.linux_funcpost_storage.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}