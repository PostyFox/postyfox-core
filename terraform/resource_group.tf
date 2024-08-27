resource "azurerm_resource_group" "rg" {
  name     = "${local.appname}-rg${local.hyphen-env}"
  location = local.location
}

resource "azurerm_role_assignment" "sp-datacontributor" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "sp-tablecontributor" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "sp-queuecontributor" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "sp-datacontributor-net" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Storage Blob Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "sp-tablecontributor-net" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "sp-queuecontributor-net" {
  scope                = azurerm_resource_group.rg.id
  role_definition_name = "Storage Queue Data Contributor"
  principal_id         = azurerm_linux_function_app.dotnet_func_app.identity[0].principal_id
}