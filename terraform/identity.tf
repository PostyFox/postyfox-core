resource "azurerm_user_assigned_identity" "storage_fa_user" {
  location            = azurerm_resource_group.rg.location
  name                = "${local.appname}-fa-identity${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
}

# Grants access to the function app storage account
resource "azurerm_role_assignment" "storage_fa_user" {
  scope                = azurerm_storage_account.funcapp_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.storage_fa_user.principal_id
}

resource "azurerm_role_assignment" "table_fa_user" {
  scope                = azurerm_storage_account.funcapp_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_user_assigned_identity.storage_fa_user.principal_id
}