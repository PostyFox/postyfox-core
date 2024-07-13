using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Notifications;

/**
 * THIS IS THE BASE MODEL
 * "id": "1243456",
 * "broadcaster_user_id": "1337",
 * "broadcaster_user_login": "cool_user",
 * "broadcaster_user_name": "Cool_User",
 * "title": "Aren’t shoes just really hard socks?",
 * "started_at": "2020-07-15T17:16:03.17106713Z"
 */
public abstract class ChannelPredictBaseModel<T>
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
        
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;
        
    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; init; }
        
    // Not part of the base, but an easier way to implement the other ones.
    [JsonPropertyName("outcomes")]
    public T[] Outcomes { get; init; } = null!;
}