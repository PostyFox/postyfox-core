using System.Text.Json.Serialization;

namespace Twitch.Net.Shared.RateLimits;

public class UserAccountStatus
{
    [JsonPropertyName("is_verified_bot")]
    public bool IsVerifiedBot { get; init; }
    [JsonPropertyName("is_known_bot")]
    public bool IsKnownBot { get; init; }
}