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
 * "tier": "1000",
 * "message": {
 *     "text": "Love the stream! FevziGG",
 *     "emotes": [
 *        {
 *            "begin": 23,
 *            "end": 30,
 *            "id": "302976485"
 *        }
 *     ]
 * },
 * "cumulative_months": 15,
 * "streak_months": 1, // null if not shared
 * "duration_months": 6
 */
public class ChannelSubscribeMessageNotificationEvent
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
        
    [JsonPropertyName("cumulative_months")]
    public int TotalMonths { get; init; }
        
    /// <summary>
    /// Null if not shared.
    /// </summary>
    [JsonPropertyName("streak_months")]
    public int? StreakMonths { get; init; }
        
    [JsonPropertyName("duration_months")]
    public int DurationMonths { get; init; }
        
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

public class Message
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("emotes")]
    public Emote[] Emotes { get; init; } = Array.Empty<Emote>();
}

public class Emote
{
    [JsonPropertyName("begin")]
    public int Begin { get; init; }
        
    [JsonPropertyName("end")]
    public int End { get; init; }
        
    [JsonPropertyName("id")]
    public string EmoteId { get; init; } = string.Empty;
}