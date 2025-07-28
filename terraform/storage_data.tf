# Deploy a Storage Account
resource "azurerm_storage_account" "data_storage" {
  name                            = "${local.appname}datastor${var.environment}"
  resource_group_name             = azurerm_resource_group.rg.name
  location                        = azurerm_resource_group.rg.location
  account_tier                    = "Standard"
  account_replication_type        = "ZRS"
  allow_nested_items_to_be_public = false

  infrastructure_encryption_enabled = true

  # shared_access_key_enabled = false # Would love to disable this, but Terraform doesn't support Table access without it.

  public_network_access_enabled = true

  min_tls_version = "TLS1_2"

  blob_properties {
    delete_retention_policy {
      days = 7
    }

    versioning_enabled = true

    container_delete_retention_policy {
      days = 7
    }
  }

  lifecycle {
    ignore_changes = [
      network_rules[0].ip_rules
    ]
  }
}

resource "azurerm_storage_table" "availableservices" {
  name                 = "AvailableServices"
  storage_account_name = azurerm_storage_account.data_storage.name
}

# External Triggers are externally triggering events that cause posting activity - i.e Twitch etc
resource "azurerm_storage_table" "externaltriggers" {
  name                 = "ExternalTriggers"
  storage_account_name = azurerm_storage_account.data_storage.name
}

# Posting queue - of items to be posted out to the users' social platforms
resource "azurerm_storage_queue" "postingqueue" {
  name                 = "postingqueue"
  storage_account_name = azurerm_storage_account.data_storage.name
}