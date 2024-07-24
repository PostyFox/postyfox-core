using System.Text.Json.Serialization;

namespace Twitch.Net.EventSub.Models;

public interface IConditionModel
{
    // empty
}
    
public class BroadcasterConditionModel : IConditionModel
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterId { get; init; } = string.Empty;
}
    
public class ToBroadcasterConditionModel : IConditionModel
{
    [JsonPropertyName("to_broadcaster_user_id")]
    public string BroadcasterId { get; init; } = string.Empty;
}
    
public class BroadcasterRewardModel : IConditionModel
{
    [JsonPropertyName("broadcaster_user_id")]
    public string BroadcasterId { get; init; } = string.Empty;
        
    [JsonPropertyName("reward_id")]
    public string? RewardId { get; init; }
}
    
public class ExtensionConditionModel : IConditionModel
{
    [JsonPropertyName("extension_client_id")]
    public string ClientId { get; init; } = string.Empty;
}
    
public class ClientConditionModel : IConditionModel
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; init; } = string.Empty;
}
    
public class UserConditionModel : IConditionModel
{
    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = string.Empty;
}