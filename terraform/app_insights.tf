resource "azurerm_application_insights" "application_insights" {
  name                = "${local.appname}-app-insights${local.hyphen-env}"
  location            = local.location
  resource_group_name = azurerm_resource_group.rg.name
  application_type    = "Node.JS"
}