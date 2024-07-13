using System.Text.Json.Serialization;

namespace Twitch.Net.PubSub.Topics;

public class TopicModel
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("data")]
    public TopicDataModel Data { get; init; }

    [JsonPropertyName("nonce")]
    public string Nonce { get; init; }
}

public class TopicDataModel
{
    [JsonPropertyName("topics")]
    public List<string> Topics { get; init; }
        
    [JsonPropertyName("auth_token")]
    public string Token { get; init; }
}