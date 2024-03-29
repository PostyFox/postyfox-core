resource "azurerm_service_plan" "linux_func_service_plan" {
  name                = "${local.appname}-asp${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
  location            = azurerm_resource_group.rg.location
  os_type             = "Linux"
  sku_name            = "Y1" # Consumption Plan
}