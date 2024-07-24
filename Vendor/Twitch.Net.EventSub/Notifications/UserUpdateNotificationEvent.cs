using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "user_id": "1337",
 * "user_login": "cool_user",
 * "user_name": "Cool_User",
 * "email": "user@email.com",  // Requires user:read:email scope
 * "description": "cool description"
 */
public class UserUpdateNotificationEvent
{
    [JsonPropertyName("user_id")]
    public string UserIdString { get; init; } = string.Empty;
    public int UserId => int.Parse(UserIdString);
        
    [JsonPropertyName("user_login")]
    public string UserLogin { get; init; } = string.Empty;
        
    [JsonPropertyName("user_name")]
    public string UserName { get; init; } = string.Empty;
        
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;
        
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    public bool ContainsEmail => !string.IsNullOrEmpty(Email);
}