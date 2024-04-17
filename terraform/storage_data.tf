# Deploy a Storage Account
resource "azurerm_storage_account" "data_storage" {
  name                     = "${local.appname}datastor${var.environment}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  shared_access_key_enabled = false
}

resource "azurerm_storage_table" "availableservices" {
  name                 = "AvailableServices"
  storage_account_name = azurerm_storage_account.data_storage.name
}