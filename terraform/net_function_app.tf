# Deploy a DotNet Core runtime Linux Function App
resource "azurerm_linux_function_app" "dotnet_func_app" {
  name                = "${local.appname}-func-app-dotnet${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location

  https_only = true

  storage_account_name          = azurerm_storage_account.linux_funcnet_storage.name
  storage_uses_managed_identity = true
  service_plan_id               = azurerm_service_plan.linux_func_service_plan.id

  site_config {
    application_stack {
      dotnet_version = "7.0"
      use_dotnet_isolated_runtime = true
    }

    application_insights_key = azurerm_application_insights.application_insights-net.instrumentation_key
  }
}