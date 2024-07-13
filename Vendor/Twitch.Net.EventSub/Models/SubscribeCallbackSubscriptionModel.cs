using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Models;

public class  SubscribeCallbackSubscriptionModel
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
        
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
        
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
        
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
        
    [JsonPropertyName("cost")]
    public int Cost { get; init; }

    [JsonPropertyName("transport")]
    public SubscribeCallbackSubscriptionTransportModel Transport { get; init; } = null!;
        
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }
}

public class SubscribeCallbackSubscriptionTransportModel
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;
        
    [JsonPropertyName("callback")]
    public string Callback { get; init; } = string.Empty;
}