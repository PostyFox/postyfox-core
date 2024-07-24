using System.Text.Json.Serialization;
using Twitch.Net.EventSub.Models;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "user_id": "1234",
 * "user_login": "cool_user",
 * "user_name": "Cool_User",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cooler_user",
 * "broadcaster_user_name": "Cooler_User",
 * "total": 2,
 * "tier": "1000",
 * "cumulative_total": 284, //null if anonymous or not shared by the user
 * "is_anonymous": false
 */
public class ChannelSubscribeGiftNotificationEvent
{
    [JsonPropertyName("user_id")]
    public string UserIdString { get; init; } = string.Empty;
    public int UserId => int.Parse(UserIdString);
        
    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = string.Empty;
        
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterIdString { get; init; } = string.Empty;
    public int BroadcasterId => int.Parse(BroadcasterIdString);
        
    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = string.Empty;
        
    [JsonPropertyName("tier")]
    public string TierString { get; init; } = string.Empty;
    public SubscriptionPlan Tier => TierString.ToSubscriptionPlan();
        
    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; init; }
        
    /// <summary>
    /// How many gifts has been given from the user in total (if anon this is null)
    /// </summary>
    [JsonPropertyName("cumulative_total")]
    public int? TotalGifted { get; init; }
        
    /// <summary>
    /// How many gifted subs were gifted in this notification
    /// </summary>
    [JsonPropertyName("total")]
    public int GiftedAmount { get; init; }
}