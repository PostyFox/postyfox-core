# Deploy a Storage Account
resource "azurerm_storage_account" "data_storage" {
  name                     = "${local.appname}datastor${var.environment}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"
}

# Deploy a container for Static Storage
