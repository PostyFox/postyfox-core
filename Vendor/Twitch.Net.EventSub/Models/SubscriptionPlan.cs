namespace Twitch.Net.EventSub.Models;

public enum SubscriptionPlan
{
    Unknown,
    Tier1,
    Tier2,
    Tier3,
    Prime
}

public static class SubscriptionPlanExtension
{
    public static SubscriptionPlan ToSubscriptionPlan(this string value) => value switch
    {
        "1000" => SubscriptionPlan.Tier1,
        "2000" => SubscriptionPlan.Tier2,
        "3000" => SubscriptionPlan.Tier3,
        "prime" => SubscriptionPlan.Prime,
        _ => SubscriptionPlan.Unknown
    };
}