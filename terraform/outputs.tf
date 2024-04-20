# We output the Function App Possible IP Addresses here, as we need to use them in script later (to add to storage allowed FW)
# We can't do it in Terraform as it will create a cyclical reference.
output "nodejs_fa_ip_list" {
    value = azurerm_linux_function_app.nodejs_func_app.possible_outbound_ip_address_list
}

output "dotnet_fa_ip_list" {
    value = azurerm_linux_function_app.nodejs_func_app.possible_outbound_ip_address_list
}