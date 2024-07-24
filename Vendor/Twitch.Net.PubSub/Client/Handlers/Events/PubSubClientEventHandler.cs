using Twitch.Net.Communication.Events;
using Twitch.Net.PubSub.Events;
using Twitch.Net.Shared.Extensions;

namespace Twitch.Net.PubSub.Client.Handlers.Events;

/**
     * We are doing this it make "PubSubClient" not overflowed with a bunch of logic & methods
     * So we can make it clearer of what is what, instead of nesting it all in a single class
     */
public class PubSubClientEventHandler : IPubSubClientEventHandler, IPubSubClientEventInvoker
{
    #region Listeners
        
    // Connections
    private readonly AsyncEvent<Func<Task>> _connectedEvents = new();
    public event Func<Task> OnPubSubConnected 
    {
        add => _connectedEvents.Add(value);
        remove => _connectedEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<Task>> _reconnectEvents = new();
    public event Func<Task> OnPubSubReconnect 
    {
        add => _reconnectEvents.Add(value);
        remove => _reconnectEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<ClientDisconnected, Task>> _disconnectEvents = new();
    public event Func<ClientDisconnected, Task> OnPubSubDisconnect 
    {
        add => _disconnectEvents.Add(value);
        remove => _disconnectEvents.Remove(value);
    }
        
    // Response message
    private readonly AsyncEvent<Func<MessageResponse, Task>> _responseMessageEvents = new();
    public event Func<MessageResponse, Task> OnResponseMessageEvent 
    {
        add => _responseMessageEvents.Add(value);
        remove => _responseMessageEvents.Remove(value);
    }
        
    // Unhandled
    private readonly AsyncEvent<Func<UnknownMessageEvent, Task>> _unknownMessageEvents = new();
    public event Func<UnknownMessageEvent, Task> OnUnknownMessageEvent 
    {
        add => _unknownMessageEvents.Add(value);
        remove => _unknownMessageEvents.Remove(value);
    }
        
    // Redeems
    private readonly AsyncEvent<Func<CommunityPointsEvent, Task>> _customRedeemEvents = new();
    public event Func<CommunityPointsEvent, Task> OnCustomRedeemEvent 
    {
        add => _customRedeemEvents.Add(value);
        remove => _customRedeemEvents.Remove(value);
    }
                
    private readonly AsyncEvent<Func<CommunityPointsEvent, Task>> _customRedeemCreatedEvents = new();
    public event Func<CommunityPointsEvent, Task> OnCustomRedeemCreated 
    {
        add => _customRedeemCreatedEvents.Add(value);
        remove => _customRedeemCreatedEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<CommunityPointsEvent, Task>> _customRedeemUpdatedEvents = new();
    public event Func<CommunityPointsEvent, Task> OnCustomRedeemUpdated 
    {
        add => _customRedeemUpdatedEvents.Add(value);
        remove => _customRedeemUpdatedEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<CommunityPointsEvent, Task>> _customRedeemDeletedEvents = new();
    public event Func<CommunityPointsEvent, Task> OnCustomRedeemDeleted 
    {
        add => _customRedeemDeletedEvents.Add(value);
        remove => _customRedeemDeletedEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<CommunityPointsEvent, Task>> _customRedeemStatusUpdateEvents = new();
    public event Func<CommunityPointsEvent, Task> OnCustomRedeemStatusUpdate 
    {
        add => _customRedeemStatusUpdateEvents.Add(value);
        remove => _customRedeemStatusUpdateEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<CommunityPointsEvent, Task>> _customProgressStartedEvents = new();
    public event Func<CommunityPointsEvent, Task> OnCustomRedeemProgressStarted 
    {
        add => _customProgressStartedEvents.Add(value);
        remove => _customProgressStartedEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<CommunityPointsEvent, Task>> _customProgressFinishedEvents = new();
    public event Func<CommunityPointsEvent, Task> OnCustomRedeemProgressFinished 
    {
        add => _customProgressFinishedEvents.Add(value);
        remove => _customProgressFinishedEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<CommunityPointsEvent, Task>> _customAutomaticUpdatedEvents = new();
    public event Func<CommunityPointsEvent, Task> OnAutomaticRedeemUpdated 
    {
        add => _customAutomaticUpdatedEvents.Add(value);
        remove => _customAutomaticUpdatedEvents.Remove(value);
    }
        
        
    private readonly AsyncEvent<Func<CheerEvent, Task>> _cheerEvents = new();
    public event Func<CheerEvent, Task> OnCheerEvent
    {
        add => _cheerEvents.Add(value);
        remove => _cheerEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<SubscribeEvent, Task>> _subscriptionEvents = new();
    public event Func<SubscribeEvent, Task> OnSubscriptionEvent
    {
        add => _subscriptionEvents.Add(value);
        remove => _subscriptionEvents.Remove(value);
    }
        
    private readonly AsyncEvent<Func<SubscribeEvent, Task>> _giftedSubscriptionEvents = new();
    public event Func<SubscribeEvent, Task> OnGiftedSubscriptionEvent
    {
        add => _giftedSubscriptionEvents.Add(value);
        remove => _giftedSubscriptionEvents.Remove(value);
    }
        
    #endregion

    #region Invokers
        
    // Connections
    public void InvokeOnPubSubConnected() =>
        _connectedEvents.Invoke();

    public void InvokeOnPubSubReconnect() =>
        _reconnectEvents.Invoke();

    public void InvokeOnPubSubDisconnect(ClientDisconnected clientDisconnected) =>
        _disconnectEvents.Invoke(clientDisconnected);
        
    // Message
    public void InvokeResponseMessage(MessageResponse arg) =>
        _responseMessageEvents.Invoke(arg);

    // Unhandled
    public void InvokeUnknownMessage(UnknownMessageEvent arg) =>
        _unknownMessageEvents.Invoke(arg);
        
    // Redeems
    public void InvokeRedeemTopic(CommunityPointsEvent arg) =>
        _customRedeemEvents.Invoke(arg);
        
    public void InvokeCustomRedeemCreatedTopic(CommunityPointsEvent arg) =>
        _customRedeemCreatedEvents.Invoke(arg);

    public void InvokeCustomRedeemUpdatedTopic(CommunityPointsEvent arg) =>
        _customRedeemUpdatedEvents.Invoke(arg);

    public void InvokeCustomRedeemDeletedTopic(CommunityPointsEvent arg) =>
        _customRedeemDeletedEvents.Invoke(arg);

    public void InvokeCustomRedeemStatusUpdateTopic(CommunityPointsEvent arg) =>
        _customRedeemStatusUpdateEvents.Invoke(arg);

    public void InvokeCustomRedeemInProgressTopic(CommunityPointsEvent arg) =>
        _customProgressStartedEvents.Invoke(arg);

    public void InvokeCustomRedeemFinishedProgressTopic(CommunityPointsEvent arg) =>
        _customProgressFinishedEvents.Invoke(arg);

    public void InvokeAutomaticRedeemUpdatedTopic(CommunityPointsEvent arg) =>
        _customAutomaticUpdatedEvents.Invoke(arg);

    public void InvokeCheerTopic(CheerEvent arg) =>
        _cheerEvents.Invoke(arg);

    public void InvokeSubscriptionEventTopic(SubscribeEvent arg) =>
        _subscriptionEvents.Invoke(arg);

    public void InvokeGiftedSubscriptionEventTopic(SubscribeEvent arg) =>
        _giftedSubscriptionEvents.Invoke(arg);

    #endregion
}