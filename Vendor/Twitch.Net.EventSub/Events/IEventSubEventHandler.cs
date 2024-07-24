using Twitch.Net.EventSub.Notifications;

namespace Twitch.Net.EventSub.Events;

public interface IEventSubEventHandler
{
    #region Notifications
    event Func<NotificationEvent<ChannelFollowNotificationEvent>, Task> OnFollowed;
    event Func<NotificationEvent<ChannelUpdateNotificationEvent>, Task> OnChannelUpdate;
    event Func<NotificationEvent<StreamOnlineNotificationEvent>, Task> OnStreamOnline;
    event Func<NotificationEvent<StreamOfflineNotificationEvent>, Task> OnStreamOffline;
    event Func<NotificationEvent<ChannelSubscribeNotificationEvent>, Task> OnChannelSubscription;
    event Func<NotificationEvent<ChannelSubscribeEndNotificationEvent>, Task> OnChannelSubscriptionEnded;
    event Func<NotificationEvent<ChannelSubscribeGiftNotificationEvent>, Task> OnChannelSubscriptionGifted;
    event Func<NotificationEvent<ChannelSubscribeMessageNotificationEvent>, Task> OnChannelSubscriptionMessage;
    event Func<NotificationEvent<ChannelCheerNotificationEvent>, Task> OnChannelCheered;
    event Func<NotificationEvent<ChannelRaidNotificationEvent>, Task> OnChannelRaided;
    event Func<NotificationEvent<ChannelBanNotificationEvent>, Task> OnChannelBan;
    event Func<NotificationEvent<ChannelUnbanNotificationEvent>, Task> OnChannelUnbanned;
    event Func<NotificationEvent<ChannelModeratorAddNotificationEvent>, Task> OnChannelModeratorAdded;
    event Func<NotificationEvent<ChannelModeratorRemoveNotificationEvent>, Task> OnChannelModeratorRemoved;
    event Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task> OnChannelRedeemAdded;
    event Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task> OnChannelRedeemUpdated;
    event Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task> OnChannelRedeemRemoved;
    event Func<NotificationEvent<ChannelRedeemRedemptionNotificationEvent>, Task> OnChannelRedeemRedemptionAdded;
    event Func<NotificationEvent<ChannelRedeemRedemptionNotificationEvent>, Task> OnChannelRedeemRedemptionUpdated;
    event Func<NotificationEvent<ChannelPollBeginNotificationEvent>, Task> OnChannelPollBegin;
    event Func<NotificationEvent<ChannelPollProgressNotificationEvent>, Task> OnChannelPollProgress;
    event Func<NotificationEvent<ChannelPollEndNotificationEvent>, Task> OnChannelPollEnded;
    event Func<NotificationEvent<ChannelPredictBeginNotificationEvent>, Task> OnChannelPredictBegin;
    event Func<NotificationEvent<ChannelPredictProgressNotificationEvent>, Task> OnChannelPredictProgress;
    event Func<NotificationEvent<ChannelPredictLockNotificationEvent>, Task> OnChannelPredictLock;
    event Func<NotificationEvent<ChannelPredictEndNotificationEvent>, Task> OnChannelPredictEnded;
    event Func<NotificationEvent<ExtensionBitTransactionNotificationEvent>, Task> OnExtensionBitTransaction;
    event Func<NotificationEvent<ChannelHypeTrainBeginNotificationEvent>, Task> OnChannelHypeTrainBegin;
    event Func<NotificationEvent<ChannelHypeTrainProgressNotificationEvent>, Task> OnChannelHypeTrainProgress;
    event Func<NotificationEvent<ChannelHypeTrainEndNotificationEvent>, Task> OnChannelHypeTrainEnded;
    event Func<NotificationEvent<UserAuthRevokeNotificationEvent>, Task> OnUserAuthRevoked;
    event Func<NotificationEvent<UserUpdateNotificationEvent>, Task> OnUserUpdated;
    #endregion
}