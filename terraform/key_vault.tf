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

  public_network_access_enabled = true

  sku_name = "standard"
}

# Secrets that should be manually added to this Key Vault:
# - "clientsecret": OIDC client secret for authentication
# - "TwitchClientSecret": Twitch application client secret
# - "TwitchSignatureSecret": Twitch webhook signature secret
# - "ContainerRegistryPassword": Container registry password for pulling images

// These are needed for the portal to not be an idiot
resource "azurerm_role_assignment" "dotnet_fa_user" {
  scope                = azurerm_key_vault.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.dotnet_function_app.identity_id
}

resource "azurerm_role_assignment" "nodejs_fa_user" {
  scope                = azurerm_key_vault.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.nodejs_function_app.identity_id
}

resource "azurerm_role_assignment" "posting_fa_user" {
  scope                = azurerm_key_vault.key_vault.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = module.posting_function_app.identity_id
}