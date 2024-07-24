using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cooler_user",
 * "broadcaster_user_name": "Cooler_User"
 */
public class StreamOfflineNotificationEvent
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterIdString { get; init; } = string.Empty;
    public int BroadcasterId => int.Parse(BroadcasterIdString);
        
    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = string.Empty;
}