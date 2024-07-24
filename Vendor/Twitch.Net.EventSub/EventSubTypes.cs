namespace Twitch.Net.EventSub;

public static class EventSubTypes
{
    public const string ChannelFollow = "channel.follow";
    public const string ChannelUpdate = "channel.update";
    public const string ChannelSubscription = "channel.subscribe";
    public const string ChannelSubscriptionEnd = "channel.subscription.end";
    public const string ChannelSubscriptionGift = "channel.subscription.gift";
    public const string ChannelSubscriptionMessage = "channel.subscription.message";
    public const string ChannelCheer = "channel.cheer";
    public const string ChannelRaid = "channel.raid";
    public const string ChannelBan = "channel.ban";
    public const string ChannelUnban = "channel.unban";
    public const string ChannelModeratorAdd = "channel.moderator.add";
    public const string ChannelModeratorRemove = "channel.moderator.remove";
    public const string ChannelCustomRewardAdd = "channel.channel_points_custom_reward.add";
    public const string ChannelCustomRewardUpdate = "channel.channel_points_custom_reward.update";
    public const string ChannelCustomRewardRemove = "channel.channel_points_custom_reward.remove";
    public const string ChannelRedemptionAdd = "channel.channel_points_custom_reward_redemption.add";
    public const string ChannelRedemptionUpdate = "channel.channel_points_custom_reward_redemption.update";
    public const string ChannelPollBegin = "channel.poll.begin";
    public const string ChannelPollProgress = "channel.poll.progress";
    public const string ChannelPollEnd = "channel.poll.end";
    public const string ChannelPredictBegin = "channel.prediction.begin";
    public const string ChannelPredictProgress = "channel.prediction.progress";
    public const string ChannelPredictLock = "channel.prediction.lock";
    public const string ChannelPredictEnd = "channel.prediction.end";
    public const string ExtensionBitTransaction = "extension.bits_transaction.create";
    public const string ChannelHypeTrainBegin = "channel.hype_train.begin";
    public const string ChannelHypeTrainProgress = "channel.hype_train.progress";
    public const string ChannelHypeTrainEnd = "channel.hype_train.end";
    public const string StreamOnline = "stream.online";
    public const string StreamOffline = "stream.offline";
    public const string UserAuthRevoke = "user.authorization.revoke";
    public const string UserUpdate = "user.update";
}