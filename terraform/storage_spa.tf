# Deploy a Storage Account
resource "azurerm_storage_account" "spa_storage" {
  name                     = "${local.appname}spastor${var.environment}"
  resource_group_name      = azurerm_resource_group.rg.name
  location                 = azurerm_resource_group.rg.location
  account_tier             = "Standard"
  account_replication_type = "LRS"

  infrastructure_encryption_enabled = true

  shared_access_key_enabled = false

  public_network_access_enabled = true

  min_tls_version = "TLS1_2"

  blob_properties {
    delete_retention_policy {
      days = 7
    }

    versioning_enabled = false # We can rollback via the code

    container_delete_retention_policy {
      days = 7
    }
  }

  static_website {
    index_document = "index.html"
  }

  dynamic "custom_domain" {
    for_each = (var.environment == "prod" ? [] : [1])
    content {
      name = "${local.portal-prefix}${local.portal-address}"
    }
  }
}