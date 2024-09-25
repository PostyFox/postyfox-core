resource "azurerm_user_assigned_identity" "func_apps_uai" {
  location            = azurerm_resource_group.rg.location
  name                = "${local.appname}-fa-uai${local.hyphen-env}"
  resource_group_name = azurerm_resource_group.rg.name
}

resource "azurerm_role_assignment" "dotnetuai-linuxfuncnet-data" {
  scope                = azurerm_storage_account.linux_funcnet_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "dotnetuai-linuxfuncnet-table" {
  scope                = azurerm_storage_account.linux_funcnet_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "dotnetuai-data-linuxfuncpost-data" {
  scope                = azurerm_storage_account.linux_funcpost_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "dotnetuai-linuxfuncpost-queue" {
  scope                = azurerm_storage_account.linux_funcpost_storage.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "dotnetuai-datastore-data" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "dotnetuai-datastore-table" {
  scope                = azurerm_storage_account.data_storage.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}