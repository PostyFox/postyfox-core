using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "id": "1234",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cool_user",
 * "broadcaster_user_name": "Cool_User",
 * "user_id": "9001",
 * "user_login": "cooler_user",
 * "user_name": "Cooler_User",
 * "user_input": "pogchamp",
 * "status": "unfulfilled", // unfulfilled, fulfilled or cancelled
 * "reward": {
 *      "id": "9001",
 *      "title": "title",
 *      "cost": 100,
 *      "prompt": "reward prompt"
 * },
 * "redeemed_at": "2020-07-15T17:16:03.17106713Z"
 */
public class ChannelRedeemRedemptionNotificationEvent
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
        
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
        
    [JsonPropertyName("user_input")]
    public string UserInput { get; init; } = string.Empty;
        
    [JsonPropertyName("status")]
    public string StatusString { get; init; } = string.Empty;
    public ChannelRedeemStatus Status => StatusString.ToRedeemStatus();
        
    [JsonPropertyName("reward")]
    public ChannelRedeemRedemptionReward Reward { get; init; } = null!;
        
    [JsonPropertyName("redeemed_at")]
    public DateTime RedeemedAt { get; init; }
}

/**
 * "id": "9001",
 * "title": "title",
 * "cost": 100,
 * "prompt": "reward prompt"
 */
public class ChannelRedeemRedemptionReward
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
        
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
        
    [JsonPropertyName("cost")]
    public int Cost { get; init; }
        
    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;
}

public enum ChannelRedeemStatus
{
    Unknown,
    Unfulfilled,
    Fulfilled,
    Cancelled
}
    
public static class ChannelRedeemStatusExtension
{
    // to lower only in-case the event sub ever changes..
    public static ChannelRedeemStatus ToRedeemStatus(this string value) => value.ToLower() switch
    {
        "unfulfilled" => ChannelRedeemStatus.Unfulfilled,
        "fulfilled" => ChannelRedeemStatus.Fulfilled,
        "cancelled" => ChannelRedeemStatus.Cancelled,
        _ => ChannelRedeemStatus.Unknown
    };
}