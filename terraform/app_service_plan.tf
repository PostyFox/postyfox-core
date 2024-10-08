resource "azapi_resource" "service_plan" {
  type                      = "Microsoft.Web/serverfarms@2023-12-01"
  schema_validation_enabled = false
  location                  = azurerm_resource_group.rg.location
  name                      = "${local.appname}-flex_asp${local.hyphen-env}"
  parent_id                 = azurerm_resource_group.rg.id
  body = jsonencode({
    kind = "functionapp",
    sku = {
      tier = "FlexConsumption",
      name = "FC1"
    },
    properties = {
      reserved = true
    }
  })
}