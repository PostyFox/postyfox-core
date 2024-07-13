using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "id": "1234",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cooler_user",
 * "broadcaster_user_name": "Cooler_User"
 * "type": "live"
 * "started_at": "2020-10-11T10:11:12.123Z"
 */
public class StreamOnlineNotificationEvent
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
        
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
        
    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; init; }
}