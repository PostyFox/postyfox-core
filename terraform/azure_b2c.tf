resource "azurerm_aadb2c_directory" "b2c_tenant" {
  country_code            = "GB"
  data_residency_location = "Europe"
  display_name            = "${local.appname}${local.hyphen-env}"
  domain_name             = "${local.appname}${var.environment}.onmicrosoft.com"
  resource_group_name     = azurerm_resource_group.rg.name
  sku_name                = "PremiumP1"
}