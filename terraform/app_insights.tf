resource "azurerm_application_insights" "application_insights-node" {
  name                = "${local.appname}-app-insights-nodejs${local.hyphen-env}"
  location            = local.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "Node.JS"
}

resource "azurerm_application_insights" "application_insights-net" {
  name                = "${local.appname}-app-insights-dotnet${local.hyphen-env}"
  location            = local.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "web"
}