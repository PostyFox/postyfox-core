using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Models;

public class SubscribeModel
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;
        
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("condition")]
    public IConditionModel Condition { get; init; } = null!;

    [JsonPropertyName("transport")]
    public SubscribeTransportModel Transport { get; init; } = null!;
}

public class SubscribeTransportModel
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;
        
    [JsonPropertyName("callback")]
    public string Callback { get; init; } = string.Empty;
        
    [JsonPropertyName("secret")]
    public string Secret { get; init; } = string.Empty;
}