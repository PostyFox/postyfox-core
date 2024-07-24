using System.Text.Json.Serialization;

namespace Twitch.Net.PubSub.Events;

public class CheerEvent
{
    [JsonPropertyName("version")]
    public string Version { get; init; }
        
    /**
     * The inner data object
     */
    [JsonPropertyName("data")]
    public CheerEventData Data { get; init; }
        
    [JsonPropertyName("message_type")]
    public string MessageType { get; init; }
        
    [JsonPropertyName("message_id")]
    public Guid MessageId { get; init; }
}
    
public class CheerEventData
{
    [JsonPropertyName("user_name")]
    public string Username { get; init; } = string.Empty; // can be empty.

    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = string.Empty; // can be empty.
        
    [JsonPropertyName("channel_name")]
    public string Channel { get; init; }
        
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; init; }
        
    [JsonPropertyName("time")]
    public DateTime Time { get; init; }
        
    [JsonPropertyName("chat_message")]
    public string Message { get; init; }
        
    [JsonPropertyName("bits_used")]
    public int Bits { get; init; }
        
    [JsonPropertyName("total_bits_used")]
    public int TotalBits { get; init; }
        
    [JsonPropertyName("context")]
    public string Context { get; init; }
        
    [JsonPropertyName("is_anonymous")]
    public bool IsAnonymous { get; init; }

    [JsonPropertyName("badge_entitlement")]
    public Dictionary<string, int> BadgeData { get; init; } = new(); // can be empty
}