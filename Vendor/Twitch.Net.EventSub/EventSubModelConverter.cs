using System.Text.Json;
using Microsoft.Extensions.Logging;
using Optional;
using Twitch.Net.EventSub.Converter;
using Twitch.Net.EventSub.Models;
using Twitch.Net.EventSub.Notifications;

namespace Twitch.Net.EventSub;

public class EventSubModelConverter
{
    private readonly ILogger<EventSubModelConverter> _logger;

    public EventSubModelConverter(ILogger<EventSubModelConverter> logger)
    {
        _logger = logger;
    }
        
    private readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        IncludeFields = true,
        Converters =
        {
            new ConditionConverter()
        }
    };
        
    public Option<INotificationEvent> GetModel(
        string raw,
        string @event
        ) => @event switch
    {
        EventSubTypes.ChannelFollow => ConvertDataToModel<ChannelFollowNotificationEvent>(raw),
        EventSubTypes.ChannelUpdate => ConvertDataToModel<ChannelUpdateNotificationEvent>(raw),
        EventSubTypes.ChannelSubscription => ConvertDataToModel<ChannelSubscribeNotificationEvent>(raw),
        EventSubTypes.ChannelSubscriptionEnd => ConvertDataToModel<ChannelSubscribeEndNotificationEvent>(raw),
        EventSubTypes.ChannelSubscriptionGift => ConvertDataToModel<ChannelSubscribeGiftNotificationEvent>(raw),
        EventSubTypes.ChannelSubscriptionMessage => ConvertDataToModel<ChannelSubscribeMessageNotificationEvent>(raw),
        EventSubTypes.ChannelCheer => ConvertDataToModel<ChannelCheerNotificationEvent>(raw),
        EventSubTypes.ChannelRaid => ConvertDataToModel<ChannelRaidNotificationEvent>(raw),
        EventSubTypes.ChannelBan => ConvertDataToModel<ChannelBanNotificationEvent>(raw),
        EventSubTypes.ChannelUnban => ConvertDataToModel<ChannelUnbanNotificationEvent>(raw),
        EventSubTypes.ChannelModeratorAdd => ConvertDataToModel<ChannelModeratorAddNotificationEvent>(raw),
        EventSubTypes.ChannelModeratorRemove => ConvertDataToModel<ChannelModeratorRemoveNotificationEvent>(raw),
        EventSubTypes.ChannelCustomRewardAdd => ConvertDataToModel<ChannelRedeemChangeNotificationEvent>(raw),
        EventSubTypes.ChannelCustomRewardUpdate => ConvertDataToModel<ChannelRedeemChangeNotificationEvent>(raw),
        EventSubTypes.ChannelCustomRewardRemove => ConvertDataToModel<ChannelRedeemChangeNotificationEvent>(raw),
        EventSubTypes.ChannelRedemptionAdd => ConvertDataToModel<ChannelRedeemRedemptionNotificationEvent>(raw),
        EventSubTypes.ChannelRedemptionUpdate => ConvertDataToModel<ChannelRedeemRedemptionNotificationEvent>(raw),
        EventSubTypes.ChannelPollBegin => ConvertDataToModel<ChannelPollBeginNotificationEvent>(raw),
        EventSubTypes.ChannelPollProgress => ConvertDataToModel<ChannelPollProgressNotificationEvent>(raw),
        EventSubTypes.ChannelPollEnd => ConvertDataToModel<ChannelPollEndNotificationEvent>(raw),
        EventSubTypes.ChannelPredictBegin => ConvertDataToModel<ChannelPredictBeginNotificationEvent>(raw),
        EventSubTypes.ChannelPredictProgress => ConvertDataToModel<ChannelPredictProgressNotificationEvent>(raw),
        EventSubTypes.ChannelPredictLock => ConvertDataToModel<ChannelPredictLockNotificationEvent>(raw),
        EventSubTypes.ChannelPredictEnd => ConvertDataToModel<ChannelPredictEndNotificationEvent>(raw),
        EventSubTypes.ExtensionBitTransaction => ConvertDataToModel<ExtensionBitTransactionNotificationEvent>(raw),
        EventSubTypes.ChannelHypeTrainBegin => ConvertDataToModel<ChannelHypeTrainBeginNotificationEvent>(raw),
        EventSubTypes.ChannelHypeTrainProgress => ConvertDataToModel<ChannelHypeTrainProgressNotificationEvent>(raw),
        EventSubTypes.ChannelHypeTrainEnd => ConvertDataToModel<ChannelHypeTrainEndNotificationEvent>(raw),
        EventSubTypes.StreamOnline => ConvertDataToModel<StreamOnlineNotificationEvent>(raw),
        EventSubTypes.StreamOffline => ConvertDataToModel<StreamOfflineNotificationEvent>(raw),
        EventSubTypes.UserAuthRevoke => ConvertDataToModel<UserAuthRevokeNotificationEvent>(raw),
        EventSubTypes.UserUpdate => ConvertDataToModel<UserUpdateNotificationEvent>(raw),
        _ => Option.None<INotificationEvent>()
    };
        
    private Option<INotificationEvent> ConvertDataToModel<T>(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<NotificationEvent<T>>(raw, JsonSerializerOptions)!.Some<INotificationEvent>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert data to model");
            return Option.None<INotificationEvent>();
        }
    }
        
    public byte[] ConvertModelToBytes(SubscribeModel model) =>
        JsonSerializer.SerializeToUtf8Bytes(model, JsonSerializerOptions);
}