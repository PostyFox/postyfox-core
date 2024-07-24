using System.Text.Json.Serialization;

namespace Twitch.Net.Communication.Models;

public class PingModel
{
    [JsonPropertyName("type")] 
    public string Type { get; init; } = "PING";
}