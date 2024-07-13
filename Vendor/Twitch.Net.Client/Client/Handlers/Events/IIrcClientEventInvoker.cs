using Twitch.Net.Client.Events;
using Twitch.Net.Communication.Events;

namespace Twitch.Net.Client.Client.Handlers.Events;

public interface IIrcClientEventInvoker
{
    // Connection
    void InvokeOnIrcConnected();
    void InvokeOnIrcReconnect();
    void InvokeOnIrcDisconnect(ClientDisconnected clientDisconnected);
    void InvokeOnAuthenticated(TwitchAuthenticatedEvent authenticatedEvent);

    // Unknown
    void InvokeOnUnknownMessage(UnknownMessageEvent messageEvent);
        
    // Chat messages
    void InvokeOnMessage(ChatMessageEvent chatMessageEvent);
    void InvokeOnBeingHosted(BeingHostedEvent beingHostedEvent);
        
    // Channel join/leave/update
    void InvokeOnUserJoinedChannel(JoinedChannelEvent joinedChannelEvent);
    void InvokeOnUserLeftChannel(LeftChannelEvent leftChannelEvent);
    void InvokeOnChannelJoined(JoinedChannelEvent joinedChannelEvent);
    void InvokeOnFailedChannelJoined(FailedJoinedChannelEvent failedJoinedChannelEvent);
    void InvokeOnChannelLeft(LeftChannelEvent leftChannelEvent);
    void InvokeOnChannelStateUpdate(ChannelStateUpdateEvent channelStateUpdateEvent);
}