# Deploy a Storage Account
resource "azurerm_storage_account" "spa_storage" {
  name                     = "${local.appname}spastor${var.environment}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  static_website {
    index_document = "index.html"
  }

  custom_domain {
    name = "${local.portal-prefix}${local.portal-address}"
  }
}