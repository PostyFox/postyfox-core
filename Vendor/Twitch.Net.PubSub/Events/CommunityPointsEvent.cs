using System.Text.Json.Serialization;

namespace Twitch.Net.PubSub.Events;

public class CommunityPointsEvent
{
    [JsonPropertyName("type")]
    public string Type { get; init; }
        
    /**
     * The data when an event is redeemed, created, updated
     */
    [JsonPropertyName("data")]
    public CommunityPointsData Data { get; init; }

    /**
     * The data when an event is in "progress" - fired during "DELETE"
     */
    [JsonPropertyName("progress")]
    public CommunityPointsProgress Progress { get; init; } 
        
    /**
     * Simplify knowing what type of event it is
     */
    public CommunityPointsEventType EventType => Type switch
    {
        "reward-redeemed" => CommunityPointsEventType.Redeem,
        "automatic-reward-updated" => CommunityPointsEventType.AutomaticUpdated, // these may not be correct anymore as twitch changed the "event"
        "custom-reward-created" => CommunityPointsEventType.CustomCreated,
        "custom-reward-updated" => CommunityPointsEventType.CustomUpdated,
        "custom-reward-deleted" => CommunityPointsEventType.CustomDeleted,
        "update-redemption-statuses-progress" => CommunityPointsEventType.InProgress,
        "update-redemption-statuses-finished" => CommunityPointsEventType.ProgressFinished,
        "redemption-status-update" => CommunityPointsEventType.RedemptionUpdate,
        _ => CommunityPointsEventType.Unknown
    };

    /**
     * Simplify knowing what state a Redeem is in 
     */
    public CommunityPointsRedeemState RedeemState => string.IsNullOrEmpty(Data?.Reward?.Status)
        ? CommunityPointsRedeemState.Invalid
        : Data.Reward?.Status switch
        {
            "UNFULFILLED" => CommunityPointsRedeemState.ActionNeeded,
            "FULFILLED" => CommunityPointsRedeemState.Approved, // pre-approved (skip queue)
            "ACTION_TAKEN" => CommunityPointsRedeemState.ActionTaken, // approved or rejected (not possible to know what as of now)
            _ => CommunityPointsRedeemState.Invalid
        };
}

public class CommunityPointsData
{
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; init; }
        
    public CommunityPointsRedemption Reward { get; private set; }

    [JsonPropertyName("redemption")]
    public CommunityPointsRedemption Redemption
    {
        init => Reward = value;
    }
        
    [JsonPropertyName("new_reward")]
    public CommunityPointsRedemption NewReward
    {
        init => Reward = value;
    }
        
    [JsonPropertyName("updated_reward")]
    public CommunityPointsRedemption UpdatedReward
    {
        init => Reward = value;
    }
        
    [JsonPropertyName("deleted_reward")]
    public CommunityPointsRedemption DeletedReward
    {
        init => Reward = value;
    }
}

public class CommunityPointsRedemption
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }
        
    [JsonPropertyName("user")]
    public CommunityPointsUser User { get; init; }
        
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; init; }
        
    [JsonPropertyName("redeemed_at")]
    public DateTime? RedeemedAt { get; init; }
        
    [JsonPropertyName("reward")]
    public CommunityPointsReward Reward { get; init; }
        
    [JsonPropertyName("status")]
    public string Status { get; init; }
        
    [JsonPropertyName("cursor")]
    public string Cursor { get; init; }
        
    [JsonPropertyName("user_input")]
    public string UserInput { get; init; }
}
    
public class CommunityPointsUser
{
    [JsonPropertyName("id")]
    public string Id { get; init; }
        
    [JsonPropertyName("login")]
    public string Login { get; init; }
        
    [JsonPropertyName("display_name")]
    public string DisplayName { get; init; }
}

public class CommunityPointsReward
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }
        
    [JsonPropertyName("reward_type")]
    public string RewardType { get; init; }
        
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; init; }
        
    [JsonPropertyName("title")]
    public string Title { get; init; }
        
    [JsonPropertyName("prompt")]
    public string Prompt { get; init; }
        
    [JsonPropertyName("cost")]
    public long Cost { get; init; }
        
    [JsonPropertyName("default_cost")]
    public long DefaultCost { get; init; }
        
    [JsonPropertyName("min_cost")]
    public long MinCost { get; init; }
        
    [JsonPropertyName("is_user_input_required")]
    public bool InputRequired { get; init; }
        
    [JsonPropertyName("is_sub_only")]
    public bool SubOnly { get; init; }
        
    [JsonPropertyName("is_enabled")]
    public bool Enabled { get; init; }
        
    [JsonPropertyName("is_paused")]
    public bool Paused { get; init; }
        
    [JsonPropertyName("is_hidden_for_subs")]
    public bool HiddenForSubs { get; init; }
        
    [JsonPropertyName("is_in_stock")]
    public bool InStock { get; init; }
        
    [JsonPropertyName("should_redemptions_skip_request_queue")]
    public bool SkipQueue { get; init; }
        
    [JsonPropertyName("updated_for_indicator_at")]
    public DateTime? LatestUpdated { get; init; }
        
    [JsonPropertyName("globally_updated_for_indicator_at")]
    public DateTime? GlobalLatestUpdated { get; init; }
        
    [JsonPropertyName("image")]
    public CommunityPointsImage Image { get; init; }
        
    [JsonPropertyName("default_image")]
    public CommunityPointsImage DefaultImage { get; init; }
        
    [JsonPropertyName("background_color")]
    public string BackgroundColor { get; init; }
        
    [JsonPropertyName("default_background_color")]
    public string DefaultBackgroundColor { get; init; }
        
    [JsonPropertyName("template_id")]
    public string TemplateId { get; init; }
        
    //[JsonPropertyName("redemptions_redeemed_current_stream")] - Not sure what this is...
        
    [JsonPropertyName("cooldown_expires_at")]
    public DateTime? CooldownExpiresAt { get; init; }
        
    [JsonPropertyName("max_per_stream")]
    public CommunityPointsMaxPerStream MaxPerStreams { get; init; }
        
    [JsonPropertyName("max_per_user_per_stream")]
    public CommunityPointsMaxPerUserPerStream MaxPerUserPerStreams { get; init; }
        
    [JsonPropertyName("global_cooldown")]
    public CommunityPointsGlobalCooldown GlobalCooldowns { get; init; }
}

public class CommunityPointsImage
{
    public string Url1X { get; init; }
    public string Url2X { get; init; }
    public string Url4X { get; init; }
}

public class CommunityPointsMaxPerStream
{
    [JsonPropertyName("is_enabled")]
    public bool Enabled { get; init; }
        
    [JsonPropertyName("max_per_stream")]
    public long MaxPerStream { get; init; }
}
    
public class CommunityPointsMaxPerUserPerStream
{
    [JsonPropertyName("is_enabled")]
    public bool Enabled { get; init; }
        
    [JsonPropertyName("max_per_user_per_stream")]
    public long MaxPerStream { get; init; }
}
    
public class CommunityPointsGlobalCooldown
{
    [JsonPropertyName("is_enabled")]
    public bool Enabled { get; init; }
        
    [JsonPropertyName("global_cooldown_seconds")]
    public long CooldownSeconds { get; init; }
}

public class CommunityPointsProgress
{
    [JsonPropertyName("id")]
    public string Id { get; init; }
        
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; init; }
        
    [JsonPropertyName("reward_id")]
    public string RewardId { get; init; }
        
    [JsonPropertyName("method")]
    public string Method { get; init; }
        
    [JsonPropertyName("new_status")]
    public string Status { get; init; }
        
    [JsonPropertyName("processed")]
    public long Processed { get; init; }
        
    [JsonPropertyName("total")]
    public long Total { get; init; }
}

public enum CommunityPointsEventType
{
    Redeem,
    CustomCreated,
    CustomUpdated,
    CustomDeleted,
    RedemptionUpdate,
    AutomaticUpdated,
    Unknown,
    InProgress,
    ProgressFinished
}
    
public enum CommunityPointsRedeemState
{
    Invalid,
    ActionNeeded,
    ActionTaken,
    Approved
}