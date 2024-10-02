
resource "azurerm_storage_account" "linux_test_storage" {
  name                     = "${local.appname}functeststor${var.environment}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  allow_nested_items_to_be_public = true

  infrastructure_encryption_enabled = true

  shared_access_key_enabled = true

  public_network_access_enabled = true

  blob_properties {
    delete_retention_policy {
      days = 7
    }

    versioning_enabled = true

    container_delete_retention_policy {
      days = 7
    }
  }
}

# Deploy a DotNet Core runtime Linux Function App
resource "azurerm_linux_function_app" "test_dotnet_func_app" {
  name                = "${local.appname}-func-app-test${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location

  https_only = true

  storage_account_name          = azurerm_storage_account.linux_test_storage.name
  storage_uses_managed_identity = true
  service_plan_id               = azurerm_service_plan.linux_consumption_func_service_plan.id

  identity {
    type = "SystemAssigned"
  }

  app_settings = {
    "PostingQueue__queueServiceUri"                 = azurerm_storage_account.data_storage.primary_queue_endpoint
    "PostingQueue"                                  = azurerm_storage_account.data_storage.primary_queue_endpoint        
    "ConfigTable"                                   = azurerm_storage_account.data_storage.primary_table_endpoint
    "SecretStore"                                   = azurerm_key_vault.key_vault.vault_uri
    "StorageAccount"                                = azurerm_storage_account.data_storage.primary_blob_endpoint
    "TwitchClientId"                                = var.twitchClientId
    "TwitchCallbackUrl"                             = var.twitchCallbackUrl
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
}

// Logging

resource "azurerm_monitor_diagnostic_setting" "test_func_app" {
  name                       = "${local.appname}-logging-app-test${local.hyphen-env}"
  target_resource_id         = azurerm_linux_function_app.test_dotnet_func_app.id
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
resource "azurerm_role_assignment" "testfuncapp-data" {
  scope                = azurerm_storage_account.linux_test_storage.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_linux_function_app.test_dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "testfuncapp-table" {
  scope                = azurerm_storage_account.linux_test_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_linux_function_app.test_dotnet_func_app.identity[0].principal_id
}

// - Data Account

resource "azurerm_role_assignment" "testfuncapp-data_storage-blob" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_function_app.test_dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "testfuncapp-data_storage-table" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_linux_function_app.test_dotnet_func_app.identity[0].principal_id
}

// - Posting Account

resource "azurerm_role_assignment" "testfuncapp-data-posting" {
  scope                = azurerm_storage_account.linux_funcpost_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_function_app.test_dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "testfuncapp-queue-posting" {
  scope                = azurerm_storage_account.linux_funcpost_storage.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_linux_function_app.test_dotnet_func_app.identity[0].principal_id
}