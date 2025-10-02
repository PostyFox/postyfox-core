module "extra_external_id" {
  source = "github.com/aneillans/azure-entra-externalid/terraform"

  resource_group_id = azurerm_resource_group.rg.id

  domain_name = "postyfox.onmicrosoft.com"

  display_name      = "PostyFox"
  country_code      = "GB"
  location          = "europe"

}