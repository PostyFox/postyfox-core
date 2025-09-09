# Deploy a Storage Account
resource "azurerm_storage_account" "funcapp_storage" {
  name                            = "${local.appname}funcstor${var.environment}"
  resource_group_name             = azurerm_resource_group.rg.name
  location                        = azurerm_resource_group.rg.location
  account_tier                    = "Standard"
  account_replication_type        = "ZRS"
  allow_nested_items_to_be_public = false

  infrastructure_encryption_enabled = true

  shared_access_key_enabled = false

  public_network_access_enabled = true

  min_tls_version = "TLS1_3"

  blob_properties {
    delete_retention_policy {
      days = 7
    }

    versioning_enabled = false # We can rollback via the code

    container_delete_retention_policy {
      days = 7
    }
  }

  network_rules {
    default_action = "Allow"
    bypass         = ["AzureServices"]
  }
}

resource "azurerm_storage_container" "dotnet_container" {
  name               = "dotnet-flexapp"
  storage_account_id = azurerm_storage_account.funcapp_storage.id
}

resource "azurerm_storage_container" "nodejs_container" {
  name               = "nodejs-flexapp"
  storage_account_id = azurerm_storage_account.funcapp_storage.id
}

resource "azurerm_storage_container" "posting_container" {
  name               = "posting-flexapp"
  storage_account_id = azurerm_storage_account.funcapp_storage.id
}