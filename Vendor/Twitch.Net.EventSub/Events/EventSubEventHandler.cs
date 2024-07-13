using Microsoft.Extensions.Logging;
using Twitch.Net.EventSub.Notifications;
using Twitch.Net.Shared.Extensions;

namespace Twitch.Net.EventSub.Events;

public class EventSubEventHandler : IEventSubEventHandler, IEventSubEventInvoker
{
    private readonly ILogger<EventSubEventHandler> _logger;

    public EventSubEventHandler(ILogger<EventSubEventHandler> logger)
    {
        _logger = logger;
    }
        
    #region Listeners 

    private readonly AsyncEvent<Func<NotificationEvent<ChannelFollowNotificationEvent>, Task>> _followedEvents = new();
    public event Func<NotificationEvent<ChannelFollowNotificationEvent>, Task> OnFollowed
    {
        add => _followedEvents.Add(value);
        remove => _followedEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelUpdateNotificationEvent>, Task>> _channelUpdateEvents = new();
    public event Func<NotificationEvent<ChannelUpdateNotificationEvent>, Task> OnChannelUpdate
    {
        add => _channelUpdateEvents.Add(value);
        remove => _channelUpdateEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<NotificationEvent<StreamOnlineNotificationEvent>, Task>> _channelOnlineEvents = new();
    public event Func<NotificationEvent<StreamOnlineNotificationEvent>, Task> OnStreamOnline
    {
        add => _channelOnlineEvents.Add(value);
        remove => _channelOnlineEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<NotificationEvent<StreamOfflineNotificationEvent>, Task>> _channelOfflineEvents = new();
    public event Func<NotificationEvent<StreamOfflineNotificationEvent>, Task> OnStreamOffline
    {
        add => _channelOfflineEvents.Add(value);
        remove => _channelOfflineEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelSubscribeNotificationEvent>, Task>> _channelSubscriptionEvents = new();
    public event Func<NotificationEvent<ChannelSubscribeNotificationEvent>, Task> OnChannelSubscription
    {
        add => _channelSubscriptionEvents.Add(value);
        remove => _channelSubscriptionEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<NotificationEvent<ChannelSubscribeEndNotificationEvent>, Task>> _channelSubscriptionEndEvents = new();
    public event Func<NotificationEvent<ChannelSubscribeEndNotificationEvent>, Task> OnChannelSubscriptionEnded
    {
        add => _channelSubscriptionEndEvents.Add(value);
        remove => _channelSubscriptionEndEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<NotificationEvent<ChannelSubscribeGiftNotificationEvent>, Task>> _channelSubscriptionGiftedEvents = new();
    public event Func<NotificationEvent<ChannelSubscribeGiftNotificationEvent>, Task> OnChannelSubscriptionGifted
    {
        add => _channelSubscriptionGiftedEvents.Add(value);
        remove => _channelSubscriptionGiftedEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<NotificationEvent<ChannelSubscribeMessageNotificationEvent>, Task>> _channelSubscriptionMessageEvents = new();
    public event Func<NotificationEvent<ChannelSubscribeMessageNotificationEvent>, Task> OnChannelSubscriptionMessage
    {
        add => _channelSubscriptionMessageEvents.Add(value);
        remove => _channelSubscriptionMessageEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<NotificationEvent<ChannelCheerNotificationEvent>, Task>> _channelCheerEvents = new();
    public event Func<NotificationEvent<ChannelCheerNotificationEvent>, Task> OnChannelCheered
    {
        add => _channelCheerEvents.Add(value);
        remove => _channelCheerEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<NotificationEvent<ChannelRaidNotificationEvent>, Task>> _channelRaidEvents = new();
    public event Func<NotificationEvent<ChannelRaidNotificationEvent>, Task> OnChannelRaided
    {
        add => _channelRaidEvents.Add(value);
        remove => _channelRaidEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelBanNotificationEvent>, Task>> _channelBanEvents = new();
    public event Func<NotificationEvent<ChannelBanNotificationEvent>, Task> OnChannelBan
    {
        add => _channelBanEvents.Add(value);
        remove => _channelBanEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelUnbanNotificationEvent>, Task>> _channelUnbanEvents = new();
    public event Func<NotificationEvent<ChannelUnbanNotificationEvent>, Task> OnChannelUnbanned
    {
        add => _channelUnbanEvents.Add(value);
        remove => _channelUnbanEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelModeratorAddNotificationEvent>, Task>> _channelModeratorAddEvents = new();
    public event Func<NotificationEvent<ChannelModeratorAddNotificationEvent>, Task> OnChannelModeratorAdded
    {
        add => _channelModeratorAddEvents.Add(value);
        remove => _channelModeratorAddEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelModeratorRemoveNotificationEvent>, Task>> _channelModeratorRemovedEvents = new();
    public event Func<NotificationEvent<ChannelModeratorRemoveNotificationEvent>, Task> OnChannelModeratorRemoved
    {
        add => _channelModeratorRemovedEvents.Add(value);
        remove => _channelModeratorRemovedEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task>> _channelRedeemAddedEvents = new();
    public event Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task> OnChannelRedeemAdded
    {
        add => _channelRedeemAddedEvents.Add(value);
        remove => _channelRedeemAddedEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task>> _channelRedeemUpdatedEvents = new();
    public event Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task> OnChannelRedeemUpdated
    {
        add => _channelRedeemUpdatedEvents.Add(value);
        remove => _channelRedeemUpdatedEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task>> _channelRedeemRemovedEvents = new();
    public event Func<NotificationEvent<ChannelRedeemChangeNotificationEvent>, Task> OnChannelRedeemRemoved
    {
        add => _channelRedeemRemovedEvents.Add(value);
        remove => _channelRedeemRemovedEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelRedeemRedemptionNotificationEvent>, Task>> _channelRedemptionAddedEvents = new();
    public event Func<NotificationEvent<ChannelRedeemRedemptionNotificationEvent>, Task> OnChannelRedeemRedemptionAdded
    {
        add => _channelRedemptionAddedEvents.Add(value);
        remove => _channelRedemptionAddedEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelRedeemRedemptionNotificationEvent>, Task>> _channelRedemptionUpdatedEvents = new();
    public event Func<NotificationEvent<ChannelRedeemRedemptionNotificationEvent>, Task> OnChannelRedeemRedemptionUpdated
    {
        add => _channelRedemptionUpdatedEvents.Add(value);
        remove => _channelRedemptionUpdatedEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelPollBeginNotificationEvent>, Task>> _channelPollBeginEvents = new();
    public event Func<NotificationEvent<ChannelPollBeginNotificationEvent>, Task> OnChannelPollBegin
    {
        add => _channelPollBeginEvents.Add(value);
        remove => _channelPollBeginEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelPollProgressNotificationEvent>, Task>> _channelPollProgressEvents = new();
    public event Func<NotificationEvent<ChannelPollProgressNotificationEvent>, Task> OnChannelPollProgress
    {
        add => _channelPollProgressEvents.Add(value);
        remove => _channelPollProgressEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelPollEndNotificationEvent>, Task>> _channelPollEndEvents = new();
    public event Func<NotificationEvent<ChannelPollEndNotificationEvent>, Task> OnChannelPollEnded
    {
        add => _channelPollEndEvents.Add(value);
        remove => _channelPollEndEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelPredictBeginNotificationEvent>, Task>> _channelPredictBeginEvents = new();
    public event Func<NotificationEvent<ChannelPredictBeginNotificationEvent>, Task> OnChannelPredictBegin
    {
        add => _channelPredictBeginEvents.Add(value);
        remove => _channelPredictBeginEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelPredictProgressNotificationEvent>, Task>> _channelPredictProgressEvents = new();
    public event Func<NotificationEvent<ChannelPredictProgressNotificationEvent>, Task> OnChannelPredictProgress
    {
        add => _channelPredictProgressEvents.Add(value);
        remove => _channelPredictProgressEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelPredictLockNotificationEvent>, Task>> _channelPredictLockEvents = new();
    public event Func<NotificationEvent<ChannelPredictLockNotificationEvent>, Task> OnChannelPredictLock
    {
        add => _channelPredictLockEvents.Add(value);
        remove => _channelPredictLockEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelPredictEndNotificationEvent>, Task>> _channelPredictEndEvents = new();
    public event Func<NotificationEvent<ChannelPredictEndNotificationEvent>, Task> OnChannelPredictEnded
    {
        add => _channelPredictEndEvents.Add(value);
        remove => _channelPredictEndEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ExtensionBitTransactionNotificationEvent>, Task>> _extensionBitTransactionEvents = new();
    public event Func<NotificationEvent<ExtensionBitTransactionNotificationEvent>, Task> OnExtensionBitTransaction
    {
        add => _extensionBitTransactionEvents.Add(value);
        remove => _extensionBitTransactionEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelHypeTrainBeginNotificationEvent>, Task>> _channelHypeTrainBeginEvents = new();
    public event Func<NotificationEvent<ChannelHypeTrainBeginNotificationEvent>, Task> OnChannelHypeTrainBegin
    {
        add => _channelHypeTrainBeginEvents.Add(value);
        remove => _channelHypeTrainBeginEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelHypeTrainProgressNotificationEvent>, Task>> _channelHypeTrainProgressEvents = new();
    public event Func<NotificationEvent<ChannelHypeTrainProgressNotificationEvent>, Task> OnChannelHypeTrainProgress
    {
        add => _channelHypeTrainProgressEvents.Add(value);
        remove => _channelHypeTrainProgressEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<ChannelHypeTrainEndNotificationEvent>, Task>> _channelHypeTrainEndEvents = new();
    public event Func<NotificationEvent<ChannelHypeTrainEndNotificationEvent>, Task> OnChannelHypeTrainEnded
    {
        add => _channelHypeTrainEndEvents.Add(value);
        remove => _channelHypeTrainEndEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<UserAuthRevokeNotificationEvent>, Task>> _userAuthRevokedEvents = new();
    public event Func<NotificationEvent<UserAuthRevokeNotificationEvent>, Task> OnUserAuthRevoked
    {
        add => _userAuthRevokedEvents.Add(value);
        remove => _userAuthRevokedEvents.Remove(value);
    }

    private readonly AsyncEvent<Func<NotificationEvent<UserUpdateNotificationEvent>, Task>> _userUpdatedEvents = new();
    public event Func<NotificationEvent<UserUpdateNotificationEvent>, Task> OnUserUpdated
    {
        add => _userUpdatedEvents.Add(value);
        remove => _userUpdatedEvents.Remove(value);
    }

    #endregion

    #region Invokers

    public void InvokeNotification(INotificationEvent @event, string type)
    {
        if (@event is NotificationEvent<ChannelFollowNotificationEvent> follow)
            _followedEvents.Invoke(follow);
        else if (@event is NotificationEvent<ChannelUpdateNotificationEvent> chlUpdate)
            _channelUpdateEvents.Invoke(chlUpdate);
        else if (@event is NotificationEvent<StreamOnlineNotificationEvent> chlOnline)
            _channelOnlineEvents.Invoke(chlOnline);
        else if (@event is NotificationEvent<StreamOfflineNotificationEvent> chlOffline)
            _channelOfflineEvents.Invoke(chlOffline);
        else if (@event is NotificationEvent<ChannelSubscribeNotificationEvent> chlSub)
            _channelSubscriptionEvents.Invoke(chlSub);
        else if (@event is NotificationEvent<ChannelSubscribeEndNotificationEvent> chlSubEnd)
            _channelSubscriptionEndEvents.Invoke(chlSubEnd);
        else if (@event is NotificationEvent<ChannelSubscribeGiftNotificationEvent> chlSubGift)
            _channelSubscriptionGiftedEvents.Invoke(chlSubGift);
        else if (@event is NotificationEvent<ChannelSubscribeMessageNotificationEvent> chlSubMsg)
            _channelSubscriptionMessageEvents.Invoke(chlSubMsg);
        else if (@event is NotificationEvent<ChannelCheerNotificationEvent> chlCheer)
            _channelCheerEvents.Invoke(chlCheer);
        else if (@event is NotificationEvent<ChannelRaidNotificationEvent> chlRaid)
            _channelRaidEvents.Invoke(chlRaid);
        else if (@event is NotificationEvent<ChannelBanNotificationEvent> chlBan)
            _channelBanEvents.Invoke(chlBan);
        else if (@event is NotificationEvent<ChannelUnbanNotificationEvent> chlUnban)
            _channelUnbanEvents.Invoke(chlUnban);
        else if (@event is NotificationEvent<ChannelModeratorAddNotificationEvent> chlModAdd)
            _channelModeratorAddEvents.Invoke(chlModAdd);
        else if (@event is NotificationEvent<ChannelModeratorRemoveNotificationEvent> chlModRemove)
            _channelModeratorRemovedEvents.Invoke(chlModRemove);
        else if (@event is NotificationEvent<ChannelRedeemChangeNotificationEvent> chlRedeem)
        {
            if (type == EventSubTypes.ChannelCustomRewardAdd)
                _channelRedeemAddedEvents.Invoke(chlRedeem);
            else if (type == EventSubTypes.ChannelCustomRewardUpdate)
                _channelRedeemUpdatedEvents.Invoke(chlRedeem);
            else if (type == EventSubTypes.ChannelCustomRewardRemove)
                _channelRedeemRemovedEvents.Invoke(chlRedeem);
            else 
                _logger.LogError("Event of type {Event} does not have an invoker event implemented - {Type}", @event, type);
        }
        else if (@event is NotificationEvent<ChannelRedeemRedemptionNotificationEvent> chlRdmChan)
        {
            if (type == EventSubTypes.ChannelRedemptionAdd)
                _channelRedemptionAddedEvents.Invoke(chlRdmChan);
            else if (type == EventSubTypes.ChannelRedemptionUpdate)
                _channelRedemptionUpdatedEvents.Invoke(chlRdmChan);
            else 
                _logger.LogError("Event of type {Event} does not have an invoker event implemented - {Type}", @event, type);
        }
        else if (@event is NotificationEvent<ChannelPollBeginNotificationEvent> chlPollBegin)
            _channelPollBeginEvents.Invoke(chlPollBegin);
        else if (@event is NotificationEvent<ChannelPollProgressNotificationEvent> chlPollProgress)
            _channelPollProgressEvents.Invoke(chlPollProgress);
        else if (@event is NotificationEvent<ChannelPollEndNotificationEvent> chlPollEnd)
            _channelPollEndEvents.Invoke(chlPollEnd);
        else if (@event is NotificationEvent<ChannelPredictBeginNotificationEvent> chlPredBegin)
            _channelPredictBeginEvents.Invoke(chlPredBegin);
        else if (@event is NotificationEvent<ChannelPredictProgressNotificationEvent> chlPredProgress)
            _channelPredictProgressEvents.Invoke(chlPredProgress);
        else if (@event is NotificationEvent<ChannelPredictLockNotificationEvent> chlPredLock)
            _channelPredictLockEvents.Invoke(chlPredLock);
        else if (@event is NotificationEvent<ChannelPredictEndNotificationEvent> chlPredEnd)
            _channelPredictEndEvents.Invoke(chlPredEnd);
        else if (@event is NotificationEvent<ExtensionBitTransactionNotificationEvent> bitExh)
            _extensionBitTransactionEvents.Invoke(bitExh);
        else if (@event is NotificationEvent<ChannelHypeTrainBeginNotificationEvent> chlHypeBegin)
            _channelHypeTrainBeginEvents.Invoke(chlHypeBegin);
        else if (@event is NotificationEvent<ChannelHypeTrainProgressNotificationEvent> chlHypeProgress)
            _channelHypeTrainProgressEvents.Invoke(chlHypeProgress);
        else if (@event is NotificationEvent<ChannelHypeTrainEndNotificationEvent> chlHypeEnd)
            _channelHypeTrainEndEvents.Invoke(chlHypeEnd);
        else if (@event is NotificationEvent<UserAuthRevokeNotificationEvent> userAuthRevoke)
            _userAuthRevokedEvents.Invoke(userAuthRevoke);
        else if (@event is NotificationEvent<UserUpdateNotificationEvent> userUpdate)
            _userUpdatedEvents.Invoke(userUpdate);
        else
            _logger.LogError("Event of type {Event} does not have an invoker event implemented", @event);
    }

    #endregion
}