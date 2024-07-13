using Microsoft.Extensions.Options;
using Optional;
using Optional.Unsafe;
using Twitch.Net.EventSub.Models;

namespace Twitch.Net.EventSub;

public class EventSubBuilder
{
    private readonly EventSubConfig _config;

    public EventSubBuilder(IOptions<EventSubConfig> config)
    {
        _config = config.Value;
    }

    /// <summary>
    /// Builds the subscribe model that will be sent to twitch (generic typed)
    /// </summary>
    /// <param name="type">Type of notification</param>
    /// <param name="value">Value for condition model</param>
    /// <param name="version">Default 1 (specify version if it changes)</param>
    /// <param name="method">What type of method shall it use (default webhook, others you'll have to handle yourself.)</param>
    /// <param name="rewardId">Optional if it is a reward notification model</param>
    public Option<SubscribeModel> Build(
        string type,
        string value,
        string version = "1",
        string method = "webhook",
        string? rewardId = null
        )
    {
        var condition = BuildConditionModel(type, value, rewardId);
        return condition.HasValue 
            ? BuildModel(type, version, method, condition.ValueOrFailure()).Some() 
            : Option.None<SubscribeModel>();
    }

    private static Option<IConditionModel> BuildConditionModel(
        string type,
        string value,
        string? rewardId = null
        )
    {
        switch (type)
        {
            case EventSubTypes.ChannelFollow:
            case EventSubTypes.ChannelUpdate:
            case EventSubTypes.ChannelSubscription:
            case EventSubTypes.ChannelSubscriptionEnd:
            case EventSubTypes.ChannelSubscriptionGift:
            case EventSubTypes.ChannelSubscriptionMessage:
            case EventSubTypes.ChannelCheer:
            case EventSubTypes.ChannelBan:
            case EventSubTypes.ChannelUnban:
            case EventSubTypes.ChannelModeratorAdd:
            case EventSubTypes.ChannelModeratorRemove:
            case EventSubTypes.ChannelRedemptionAdd:
            case EventSubTypes.ChannelRedemptionUpdate:
            case EventSubTypes.ChannelPollBegin:
            case EventSubTypes.ChannelPollProgress:
            case EventSubTypes.ChannelPollEnd:
            case EventSubTypes.ChannelPredictBegin:
            case EventSubTypes.ChannelPredictProgress:
            case EventSubTypes.ChannelPredictLock:
            case EventSubTypes.ChannelPredictEnd:
            case EventSubTypes.ChannelHypeTrainBegin:
            case EventSubTypes.ChannelHypeTrainProgress:
            case EventSubTypes.ChannelHypeTrainEnd:
            case EventSubTypes.StreamOnline:
            case EventSubTypes.StreamOffline:
                return BuildBroadcasterModel(value).Some();

            case EventSubTypes.ChannelRaid:
                return BuildToBroadcasterModel(value).Some();
                
            case EventSubTypes.ChannelCustomRewardAdd:
            case EventSubTypes.ChannelCustomRewardUpdate:
            case EventSubTypes.ChannelCustomRewardRemove:
                return BuildBroadcasterRewardModel(value, rewardId).Some();
                
            case EventSubTypes.ExtensionBitTransaction:
                return BuildExtensionModel(value).Some();
                
            case EventSubTypes.UserAuthRevoke:
                return BuildClientModel(value).Some();
                
            case EventSubTypes.UserUpdate:
                return BuildUserModel(value).Some();
                
            default:
                return Option.None<IConditionModel>();
        }
    }
        
    private SubscribeModel BuildModel(string type, string version, string method, IConditionModel condition) =>
        new()
        {
            Type = type,
            Version = version,
            Condition = condition,
            Transport = new SubscribeTransportModel
            {
                Method = method,
                Callback = _config.CallbackUrl,
                Secret = _config.SignatureSecret
            }
        };

    private static IConditionModel BuildBroadcasterModel(string broadcasterId) =>
        new BroadcasterConditionModel
        {
            BroadcasterId = broadcasterId
        };

    private static IConditionModel BuildToBroadcasterModel(string broadcasterId) =>
        new ToBroadcasterConditionModel
        {
            BroadcasterId = broadcasterId
        };

    private static IConditionModel BuildExtensionModel(string clientId) =>
        new ExtensionConditionModel
        {
            ClientId = clientId
        };

    private static IConditionModel BuildUserModel(string userId) =>
        new UserConditionModel
        {
            UserId = userId
        };

    private static IConditionModel BuildClientModel(string clientId) =>
        new ClientConditionModel
        {
            ClientId = clientId
        };

    private static IConditionModel BuildBroadcasterRewardModel(string broadcasterId, string? rewardId) =>
        new BroadcasterRewardModel
        {
            BroadcasterId = broadcasterId,
            RewardId = rewardId
        };
}