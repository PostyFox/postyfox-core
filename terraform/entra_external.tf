module "extra_external_id" {
  source = "github.com/aneillans/azure-entra-externalid/terraform"

  resource_group_id = azurerm_resource_group.rg.id

  domain_name = var.entra_tenant_id

  display_name      = "PostyFox"
  country_code      = "GB"
  location          = "europe"

  sku_name = "Base"

}