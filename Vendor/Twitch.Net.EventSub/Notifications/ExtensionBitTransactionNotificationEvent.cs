using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "id": "bits-tx-id",
 * "extension_client_id": "deadbeef",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cool_user",
 * "broadcaster_user_name": "Cool_User",
 * "user_name": "Coolest_User",
 * "user_login": "coolest_user",
 * "user_id": "1236",
 * "product": {
 *      "name": "great_product",
 *      "sku": "skuskusku",
 *      "bits": 1234,
 *      "in_development": false
 * }
 */
public class ExtensionBitTransactionNotificationEvent
{
    [JsonPropertyName("id")]
    public string TransactionId { get; init; } = string.Empty;
        
    [JsonPropertyName("extension_client_id")]
    public string ExtensionClientId { get; init; } = string.Empty;
        
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterIdString { get; init; } = string.Empty;
    public int BroadcasterId => int.Parse(BroadcasterIdString);
        
    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = string.Empty;
        
    [JsonPropertyName("user_id")]
    public string UserIdString { get; init; } = string.Empty;
    public int UserId => int.Parse(UserIdString);
        
    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = string.Empty;

    [JsonPropertyName("product")]
    public ExtensionProductModel Product { get; init; } = null!;
}

/**
 * "name": "great_product",
 * "sku": "skuskusku",
 * "bits": 1234,
 * "in_development": false
 */
public class ExtensionProductModel
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
        
    [JsonPropertyName("sku")]
    public string Sku { get; init; } = string.Empty;
        
    [JsonPropertyName("bits")]
    public int Bits { get; init; }
        
    [JsonPropertyName("in_development")]
    public bool InDevelopmentStatus { get; init; }
}