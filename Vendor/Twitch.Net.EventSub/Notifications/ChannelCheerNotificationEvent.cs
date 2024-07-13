using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "is_anonymous": false,
 * "user_id": "1234",          // null if is_anonymous=true
 * "user_login": "cool_user",  // null if is_anonymous=true
 * "user_name": "Cool_User",   // null if is_anonymous=true
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cooler_user",
 * "broadcaster_user_name": "Cooler_User",
 * "message": "pogchamp",
 * "bits": 1000
 */
public class ChannelCheerNotificationEvent
{
    [JsonPropertyName("user_id")]
    public string? UserIdString { get; init; }
    public int? UserId => int.TryParse(UserIdString, out var result) ? result : null;
        
    [JsonPropertyName("user_login")]
    public string? UserLogin { get; init; }
        
    [JsonPropertyName("user_name")]
    public string? UserName { get; init; }
        
    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; init; }
        
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterIdString { get; init; } = string.Empty;
    public int BroadcasterId => int.Parse(BroadcasterIdString);
        
    [JsonPropertyName("broadcaster_user_login")]
    public string BroadcasterUserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("broadcaster_user_name")]
    public string BroadcasterUserName { get; init; } = string.Empty;
        
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
        
    [JsonPropertyName("bits")]
    public int Bits { get; init; }
}