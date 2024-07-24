using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "user_id": "1234",
 * "user_login": "cool_user",
 * "user_name": "Cool_User",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cooler_user",
 * "broadcaster_user_name": "Cooler_User",
 * "followed_at": "2020-07-15T18:16:11.17106713Z"
 */
public class ChannelFollowNotificationEvent
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
        
    [JsonPropertyName("followed_at")]
    public DateTime FollowedAt { get; init; }
}