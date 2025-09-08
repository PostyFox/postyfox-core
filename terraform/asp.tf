resource "azurerm_service_plan" "asp_flex" {
  name                = "${local.appname}-flex${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  os_type             = "Linux"
  sku_name            = "FC1"
}
