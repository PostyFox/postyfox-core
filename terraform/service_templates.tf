resource "azurerm_storage_table_entity" "telegram-template" {
  storage_table_id = azurerm_storage_table.availableservices.id

  partition_key = "Service"
  row_key       = "Telegram"

  entity = {
    ServiceName   = "Telegram"
    IsEnabled     = true
    ServiceID     = "Telegram"
    Configuration = "{\"PhoneNumber\":\"\"}"
  }
}

resource "azurerm_storage_table_entity" "discordwh-template" {
  storage_table_id = azurerm_storage_table.availableservices.id

  partition_key = "Service"
  row_key       = "DiscordWH"

  entity = {
    ServiceName   = "Discord Web Hook"
    IsEnabled     = true
    ServiceID     = "DiscordWH"
    Configuration = "{\"Webhook\":\"\"}"
  }
}

resource "azurerm_storage_table_entity" "bluesky-template" {
  storage_table_id = azurerm_storage_table.availableservices.id

  partition_key = "Service"
  row_key       = "BlueSky"

  entity = {
    ServiceName         = "BlueSky"
    IsEnabled           = true
    ServiceID           = "BlueSky"
    Configuration       = "{\"Handle\":\"\"}"
    SecureConfiguration = "{\"AppPassword\":\"\"}"
  }
}