resource "azurerm_log_analytics_workspace" "log_analytics" {
  name                = "${local.appname}-la${local.hyphen-env}"
  location            = azurerm_resource_group.rg.location
  resource_group_name = azurerm_resource_group.rg.name
  sku                 = "PerGB2018"
  retention_in_days   = 30
}

resource "azurerm_application_insights" "application_insights-node" {
  name                = "${local.appname}-app-insights-nodejs${local.hyphen-env}"
  location            = local.location
  resource_group_name = azurerm_resource_group.rg.name
  workspace_id        = azurerm_log_analytics_workspace.log_analytics.id
  application_type    = "Node.JS"
}

resource "azurerm_application_insights" "application_insights-net" {
  name                = "${local.appname}-app-insights-dotnet${local.hyphen-env}"
  location            = local.location
  resource_group_name = azurerm_resource_group.rg.name
  workspace_id        = azurerm_log_analytics_workspace.log_analytics.id
  application_type    = "web"
}