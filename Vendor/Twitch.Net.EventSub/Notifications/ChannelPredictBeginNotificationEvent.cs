using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * "id": "1243456",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cool_user",
 * "broadcaster_user_name": "Cool_User",
 * "title": "Aren’t shoes just really hard socks?",
 * "outcomes": [
 *      {"id": "1243456", "title": "Yeah!", "color": "blue"},
 *      {"id": "2243456", "title": "No!", "color": "pink"},
 * ],
 * "started_at": "2020-07-15T17:16:03.17106713Z",
 * "locks_at": "2020-07-15T17:21:03.17106713Z"
 */
public class ChannelPredictBeginNotificationEvent : ChannelPredictBaseModel<PredictBeginOutcome>
{
    [JsonPropertyName("locks_at")]
    public DateTime LocksAt { get; init; }
}

/**
 * "id": "1243456",
 * "title": "Yeah!",
 * "color": "blue"
 */
public class PredictBeginOutcome
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
        
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
        
    [JsonPropertyName("color")]
    public string Color { get; init; } = string.Empty;
}