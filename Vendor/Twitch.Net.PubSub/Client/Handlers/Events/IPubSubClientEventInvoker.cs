using Twitch.Net.Communication.Events;
using Twitch.Net.PubSub.Events;

namespace Twitch.Net.PubSub.Client.Handlers.Events;

public interface IPubSubClientEventInvoker
{
    // Connection
    void InvokeOnPubSubConnected();
    void InvokeOnPubSubReconnect();
    void InvokeOnPubSubDisconnect(ClientDisconnected clientDisconnected);
        
    // Response message
    void InvokeResponseMessage(MessageResponse arg);
        
    // Unhandled
    void InvokeUnknownMessage(UnknownMessageEvent arg);

    // Redeems - possible to improve by having interfaces behind the CommunityPointsEvent for better type-safe of each event
    void InvokeRedeemTopic(CommunityPointsEvent arg);
    void InvokeCustomRedeemCreatedTopic(CommunityPointsEvent arg);
    void InvokeCustomRedeemUpdatedTopic(CommunityPointsEvent arg);
    void InvokeCustomRedeemDeletedTopic(CommunityPointsEvent arg);
    void InvokeCustomRedeemStatusUpdateTopic(CommunityPointsEvent arg);
    void InvokeCustomRedeemInProgressTopic(CommunityPointsEvent arg);
    void InvokeCustomRedeemFinishedProgressTopic(CommunityPointsEvent arg);
    void InvokeAutomaticRedeemUpdatedTopic(CommunityPointsEvent arg);
        
    // Cheer
    void InvokeCheerTopic(CheerEvent arg);
        
    // Subevents
    void InvokeSubscriptionEventTopic(SubscribeEvent arg);
    void InvokeGiftedSubscriptionEventTopic(SubscribeEvent arg);
}