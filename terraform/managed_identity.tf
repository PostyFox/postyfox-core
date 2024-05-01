resource "azurerm_user_assigned_identity" "func_apps_uai" {
  location            = azurerm_resource_group.rg.location
  name                = "${local.appname}-fa-uai${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
}