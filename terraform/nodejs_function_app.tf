# Deploy a NodeJS runtime Linux Function App, which will predominately run workloads mutually shared with PostyBirb

resource "azurerm_linux_function_app" "nodejs_func_app" {
  name                = "${local.appname}-func-app-nodejs${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location

  https_only = true

  storage_account_name          = azurerm_storage_account.linux_func_storage.name
  storage_uses_managed_identity = true
  service_plan_id               = azurerm_service_plan.linux_func_service_plan.id

  site_config {
    application_stack {
      node_version = 18
    } 
  }

  # app_settings = {
  #   FUNCTIONS_EXTENSION_VERSION = "~4",
  #   FUNCTIONS_WORKER_RUNTIME = "node",
  #   WEBSITE_NODE_DEFAULT_VERSION = "~18"
  # }
}