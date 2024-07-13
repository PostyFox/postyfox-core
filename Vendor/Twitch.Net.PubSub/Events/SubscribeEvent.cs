using System.Text.Json.Serialization;

namespace Twitch.Net.PubSub.Events;

public class SubscribeEvent
{
    [JsonPropertyName("user_name")]
    public string Username { get; init; } = string.Empty; // can be empty.

    [JsonPropertyName("user_id")]
    public string UserId { get; init; } = string.Empty; // can be empty.
        
    [JsonPropertyName("channel_name")]
    public string Channel { get; init; }
        
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; init; }
        
    [JsonPropertyName("sub_plan")]
    public string SubPlan { get; init; }
        
    [JsonPropertyName("sub_plan_name")]
    public string SubPlanName { get; init; }
        
    [JsonPropertyName("cumulative_months")]
    public short Months { get; init; }
        
    [JsonPropertyName("streak_months")]
    public short Streak { get; init; }
        
    [JsonPropertyName("context")]
    public string Context { get; init; }
        
    [JsonPropertyName("is_gift")]
    public bool Gifted { get; init; }
        
    [JsonPropertyName("sub_message")]
    public SubscribeEventMessageData SubMessage { get; init; }
        
    [JsonPropertyName("recipient_id")]
    public string RecipientId { get; init; }
        
    [JsonPropertyName("recipient_user_name")]
    public string RecipientUser { get; init; }
        
    [JsonPropertyName("recipient_display_name")]
    public string RecipientDisplayName { get; init; }
        
    [JsonPropertyName("multi_month_duration")]
    public byte MonthsGifted { get; init; }
}
    
public class SubscribeEventMessageData
{
    [JsonPropertyName("message")]
    public string Message { get; init; }
        
    [JsonPropertyName("emotes")]
    public SubscribeEventMessageEmotes[] Emotes { get; init; }
}

public class SubscribeEventMessageEmotes
{
    [JsonPropertyName("start")]
    public int Start { get; init; }
        
    [JsonPropertyName("end")]
    public int End { get; init; }
        
    [JsonPropertyName("id")]
    public string Id { get; init; }
}