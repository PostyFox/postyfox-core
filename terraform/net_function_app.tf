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
    "ConfigTable" = azurerm_storage_account.data_storage.primary_table_endpoint
  }

  site_config {
    application_stack {
      dotnet_version = "8.0"
      use_dotnet_isolated_runtime = true
    }

    application_insights_key = azurerm_application_insights.application_insights-net.instrumentation_key
  }
}

// Add application identity to Storage Blob Owner on storage account
resource "azurerm_role_assignment" "dotnetfuncapp-dataowner" {
  scope                = azurerm_storage_account.linux_funcnet_storage.id
  role_definition_name = "Storage Blob Data Owner"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "dotnetfuncapp-table" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "dotnetfuncapp-datacontributor" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}