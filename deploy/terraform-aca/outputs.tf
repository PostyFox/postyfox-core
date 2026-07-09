output "core_api_fqdn" {
  value = azurerm_container_app.core_api.ingress[0].fqdn
}
output "post_api_fqdn" {
  value = azurerm_container_app.post_api.ingress[0].fqdn
}
output "connectors_node_fqdn" {
  value = azurerm_container_app.connectors_node.ingress[0].fqdn
}
