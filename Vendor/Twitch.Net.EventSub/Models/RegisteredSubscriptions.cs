using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Models;

/**
 * "data": [
 *      {
 *          "id": "26b1c993-bfcf-44d9-b876-379dacafe75a",
 *          "status": "enabled",
 *          "type": "streams.online",
 *          "version": "1",
 *          "cost": 1,
 *          "condition": {
 *              "broadcaster_user_id": "1234"
 *          },
 *          "created_at": "2020-11-10T20:08:33Z",
 *          "transport": {
 *              "method": "webhook",
 *              "callback": "https://this-is-a-callback.com"
 *          }
 *      },
 *      {
 *          "id": "35016908-41ff-33ce-7879-61b8dfc2ee16",
 *          "status": "webhook_callback_verification_pending",
 *          "type": "users.update",
 *          "version": "1",
 *          "cost": 1,
 *          "condition": {
 *              "user_id": "1234"
 *          },
 *          "created_at": "2020-11-10T20:31:52Z",
 *          "transport": {
 *              "method": "webhook",
 *              "callback": "https://this-is-a-callback.com"
 *          }
 *      }
 *  ],
 *  "total": 2,
 *  "total_cost": 2,
 *  "max_total_cost": 10000,
 *  "pagination": {}
 */
public class RegisteredSubscriptions
{
    [JsonPropertyName("data")]
    public List<RegisteredSubscriptionModel> Data { get; init; } = new();

    [JsonPropertyName("total")]
    public int Total { get; init; }
        
    [JsonPropertyName("total_cost")]
    public int TotalCost { get; init; }
        
    [JsonPropertyName("max_total_cost")]
    public int MaxTotalCost { get; init; }
        
    // TODO : Handle pagination somehow, got too little data to test & build this - really have to get this done soon....
}

public class RegisteredSubscriptionModel : SubscribeCallbackSubscriptionModel
{
    [JsonPropertyName("condition")]
    public RegisteredSubscriptionModelCondition Condition { get; init; } = null!;
}

/**
     * Simplify the deserialize part, as it would otherwise be too complicated to handle
     * as it is based on "type" that is part of the data struct object.
     * and this is also the way that Twitch "returns" the data from the api
     */
public class RegisteredSubscriptionModelCondition
{
    [JsonPropertyName("broadcaster_user_id")]
    public string? BroadcasterId { get; init; }
        
    [JsonPropertyName("to_broadcaster_user_id")]
    public string?ToBroadcasterId { get; init; }
        
    [JsonPropertyName("from_broadcaster_user_id")]
    public string? FromBroadcasterId { get; init; }
        
    [JsonPropertyName("reward_id")]
    public string? RewardId { get; init; }
        
    [JsonPropertyName("extension_client_id")]
    public string? ExtensionClientId { get; init; }
        
    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }
        
    [JsonPropertyName("user_id")]
    public string? UserId { get; init; }
}