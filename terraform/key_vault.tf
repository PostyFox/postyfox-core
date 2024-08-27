resource "azurerm_key_vault" "key_vault" {
  name                            = "${local.appname}-kv${local.hyphen-env}"
  location                        = azurerm_resource_group.rg.location
  resource_group_name             = azurerm_resource_group.rg.name
  enabled_for_disk_encryption     = true
  enabled_for_deployment          = true
  enabled_for_template_deployment = true
  tenant_id                       = data.azurerm_client_config.current.tenant_id
  soft_delete_retention_days      = 7
  purge_protection_enabled        = true
  enable_rbac_authorization       = true

  public_network_access_enabled = true

  sku_name = "standard"
}

# A secret called client secret should be added to this vault :)

resource "azurerm_role_assignment" "secret_permissions" {
  scope                = azurerm_key_vault.key_vault.id
  role_definition_name = "Key Vault Secrets Officer"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}


// These are needed for the portal to not be an idiot
resource "azurerm_role_assignment" "dotnet_fa_user" {
  scope                = azurerm_key_vault.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.func_apps_uai.principal_id
}

resource "azurerm_role_assignment" "nodejs_fa_user" {
  scope                = azurerm_key_vault.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_function_app.nodejs_func_app.identity[0].principal_id
}

resource "azurerm_role_assignment" "posting_fa_user" {
  scope                = azurerm_key_vault.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_function_app.dotnet_funcpost_app.identity[0].principal_id
}

resource "azurerm_monitor_diagnostic_setting" "keyvault" {
  name                       = "${local.appname}-logging-keyvault${local.hyphen-env}"
  target_resource_id         = azurerm_key_vault.key_vault.id
  log_analytics_workspace_id = azurerm_log_analytics_workspace.log_analytics.id

  metric {
    category = "AllMetrics"
    enabled  = false
  }

  dynamic "enabled_log" {
    for_each = var.kv_logs
    content {
      category = enabled_log.value
    }
  }
}