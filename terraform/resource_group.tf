resource "azurerm_resource_group" "rg" {
  name     = "${local.appname}-rg${local.hyphen-env}"
  location = local.location
}