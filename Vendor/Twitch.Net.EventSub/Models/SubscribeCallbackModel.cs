using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Models;

public class SubscribeCallbackModel
{
    [JsonPropertyName("challenge")]
    public string Challenge { get; init; } = string.Empty;
}