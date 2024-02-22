resource "azurerm_resource_group" "rg" {
  name     = "${local.appname}-rg${local.hyphen-env}"
  location = local.location
}

resource "azurerm_role_assignment" "sp-datacontributor" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = data.azurerm_client_config.current.object_id
}