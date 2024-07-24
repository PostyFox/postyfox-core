using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "id": "9001",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cool_user",
 * "broadcaster_user_name": "Cool_User",
 * "is_enabled": true,
 * "is_paused": false,
 * "is_in_stock": true,
 * "title": "Cool Reward",
 * "cost": 100,
 * "prompt": "reward prompt",
 * "is_user_input_required": true,
 * "should_redemptions_skip_request_queue": false,
 * "cooldown_expires_at": null, // "2019-11-16T10:11:12.123Z"
 * "redemptions_redeemed_current_stream": null, // 123
 * "max_per_stream": {
 *      "is_enabled": true,
 *      "value": 1000
 * },
 * "max_per_user_per_stream": {
 *      "is_enabled": true,
 *      "value": 1000
 * },
 * "global_cooldown": {
 *      "is_enabled": true,
 *      "seconds": 1000
 * },
 * "background_color": "#FA1ED2",
 * "image": {
 *      "url_1x": "https://static-cdn.jtvnw.net/image-1.png",
 *      "url_2x": "https://static-cdn.jtvnw.net/image-2.png",
 *      "url_4x": "https://static-cdn.jtvnw.net/image-4.png"
 * },
 * "default_image": {
 *      "url_1x": "https://static-cdn.jtvnw.net/default-1.png",
 *      "url_2x": "https://static-cdn.jtvnw.net/default-2.png",
 *      "url_4x": "https://static-cdn.jtvnw.net/default-4.png"
 * }
 */
public class ChannelRedeemChangeNotificationEvent
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
        
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; init; }
        
    [JsonPropertyName("is_paused")]
    public bool IsPaused { get; init; }
        
    [JsonPropertyName("is_in_stock")]
    public bool IsInStock { get; init; }
        
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
        
    [JsonPropertyName("cost")]
    public int Cost { get; init; }

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;
        
    [JsonPropertyName("is_user_input_required")]
    public bool UserInputRequired { get; init; }
        
    [JsonPropertyName("should_redemptions_skip_request_queue")]
    public bool SkipRequestQueue { get; init; }
        
    [JsonPropertyName("cooldown_expires_at")]
    public DateTime CooldownExpiresAt { get; init; }
        
    [JsonPropertyName("redemptions_redeemed_current_stream")]
    public int? RedeemsCurrentStream { get; init; }

    [JsonPropertyName("max_per_stream")]
    public EnabledNumericValueModel MaxPerStream { get; init; } = null!;

    [JsonPropertyName("max_per_user_per_stream")]
    public EnabledNumericValueModel MaxPerUserPerStream { get; init; } = null!;

    [JsonPropertyName("global_cooldown")]
    public GlobalCooldownModel GlobalCooldown { get; init; } = null!;
        
    [JsonPropertyName("background_color")]
    public string BackgroundColor { get; init; } = string.Empty;

    [JsonPropertyName("image")]
    public ImageModel Image { get; init; } = null!;

    [JsonPropertyName("default_image")]
    public ImageModel DefaultImage { get; init; } = null!;
}
    
/**
 * "is_enabled": true,
 * "seconds": 1000
 */
public class GlobalCooldownModel
{
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; init; }
        
    [JsonPropertyName("seconds")]
    public int Seconds { get; init; }
}
    
/**
 * "is_enabled": true,
 * "value": 1000
 */
public class EnabledNumericValueModel
{
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; init; }
        
    [JsonPropertyName("value")]
    public int Value { get; init; }
}
    
/**
 * "url_1x": "https://static-cdn.jtvnw.net/default-1.png",
 * "url_2x": "https://static-cdn.jtvnw.net/default-2.png",
 * "url_4x": "https://static-cdn.jtvnw.net/default-4.png"
 */
public class ImageModel
{
    [JsonPropertyName("url_1x")]
    public string Size1X { get; init; } = string.Empty;
        
    [JsonPropertyName("url_2x")]
    public string Size2X { get; init; } = string.Empty;
        
    [JsonPropertyName("url_4x")]
    public string Size4X { get; init; } = string.Empty;
}