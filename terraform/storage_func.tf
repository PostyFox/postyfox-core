resource "azurerm_storage_account" "linux_func_storage" {
  name                            = "${local.appname}funcnodestor${var.environment}"
  resource_group_name             = azurerm_resource_group.rg.name
  location                        = azurerm_resource_group.rg.location
  account_tier                    = "Standard"
  account_replication_type        = "LRS"
  allow_nested_items_to_be_public = true

  infrastructure_encryption_enabled = true

  shared_access_key_enabled = false

  public_network_access_enabled = true

  blob_properties {
    delete_retention_policy {
      days = 7
    }

    versioning_enabled = true

    container_delete_retention_policy {
      days = 7
    }
  }
}

resource "azurerm_storage_account" "linux_funcnet_storage" {
  name                     = "${local.appname}funcnetstor${var.environment}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  allow_nested_items_to_be_public = true

  infrastructure_encryption_enabled = true

  shared_access_key_enabled = false

  public_network_access_enabled = true

  blob_properties {
    delete_retention_policy {
      days = 7
    }

    versioning_enabled = true

    container_delete_retention_policy {
      days = 7
    }
  }
}


resource "azurerm_storage_account" "linux_funcpost_storage" {
  name                     = "${local.appname}funcpststr${var.environment}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  allow_nested_items_to_be_public = true

  infrastructure_encryption_enabled = true

  shared_access_key_enabled = false

  public_network_access_enabled = true

  blob_properties {
    delete_retention_policy {
      days = 7
    }

    versioning_enabled = true

    container_delete_retention_policy {
      days = 7
    }
  }
}