resource "azurerm_storage_table_entity" "telegram-template" {
  storage_table_id = azurerm_storage_table.availableservices.id

  partition_key = "Service"
  row_key       = "Telegram"

  entity = {
    ServiceName             = "Telegram"
    "IsEnabled@odata.type"  = "Edm.Boolean"
    IsEnabled               = true
    ServiceID               = "Telegram"
    Configuration           = "{\"PhoneNumber\":\"\",\"DefaultPostingTarget\":\"\"}"
    ConfigurationReferences = "{\"DefaultPostingTarget\":\"Telegram_GetChannelsAndChats\"}"
    Endpoint                = module.dotnet_function_app.default_hostname
  }
}

resource "azurerm_storage_table_entity" "discordwh-template" {
  storage_table_id = azurerm_storage_table.availableservices.id

  partition_key = "Service"
  row_key       = "DiscordWH"

  entity = {
    ServiceName            = "Discord Web Hook"
    "IsEnabled@odata.type" = "Edm.Boolean"
    IsEnabled              = true
    ServiceID              = "DiscordWH"
    Configuration          = "{\"Webhook\":\"\"}"
    Endpoint               = module.dotnet_function_app.default_hostname
  }
}

# resource "azurerm_storage_table_entity" "bluesky-template" {
#   storage_table_id = azurerm_storage_table.availableservices.id

#   partition_key = "Service"
#   row_key       = "BlueSky"

#   entity = {
#     ServiceName            = "BlueSky"
#     "IsEnabled@odata.type" = "Edm.Boolean"
#     IsEnabled              = true
#     ServiceID              = "BlueSky"
#     Configuration          = "{\"Handle\":\"\"}"
#     SecureConfiguration    = "{\"AppPassword\":\"\"}"
#     Endpoint               = azurerm_linux_function_app.nodejs_func_app.default_hostname
#   }
# }

# resource "azurerm_storage_table_entity" "tumblr-template" {
#   storage_table_id = azurerm_storage_table.availableservices.id

#   partition_key = "Service"
#   row_key       = "Tumblr"

#   entity = {
#     ServiceName            = "Tumblr"
#     "IsEnabled@odata.type" = "Edm.Boolean"
#     IsEnabled              = true
#     ServiceID              = "Tumblr"
#     Configuration          = "{\"Username\":\"\", \"OAuthState\":\"\", \"OAuthExpires\":\"\"}"
#     SecureConfiguration    = "{\"OAuthAccessToken\":\"\", \"OAuthRefreshToken\":\"\"}"
#     Endpoint               = azurerm_linux_function_app.nodejs_func_app.default_hostname
#   }
# }