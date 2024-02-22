data "azurerm_aadb2c_directory" "b2c_tenant" {
    domain_name         = "${local.b2ctenant}.onmicrosoft.com"
    resource_group_name = azurerm_resource_group.rg.name
}

data "azurerm_client_config" "current" {
}