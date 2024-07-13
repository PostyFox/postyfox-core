using Optional;
using Optional.Collections;
using Twitch.Net.Client.Client;
using Twitch.Net.Client.Client.Handlers.Events;
using Twitch.Net.Client.Irc;
using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Events.Handlers;

public class ChatChannelStateEventHandler : IHandler
{
    private readonly IIrcClient _client;

    public ChatChannelStateEventHandler(IIrcClient client)
    {
        _client = client;
    }

    public bool Handle(IIrcClientEventInvoker eventInvoker, IrcMessage message)
    {
        if (message.Tags.Count > 2) // full join
        {
            var channel = _client.Channels.FirstOrNone(c =>
                c.ChannelName.Equals(message.Channel, StringComparison.OrdinalIgnoreCase));
            channel.MatchSome(chl =>
                eventInvoker.InvokeOnChannelJoined(new JoinedChannelEvent
                {
                    Username = message.User,
                    Channel = chl
                        .SetChannelStates(new ChannelStates(message).Some())
                        .SetConnectionState(ChatChannelConnectionState.Connected)
                }));
        }
        else if (message.Tags.Count == 2)
        {
            var channel = _client.Channels.FirstOrNone(c =>
                c.ChannelName.Equals(message.Channel, StringComparison.OrdinalIgnoreCase));
            channel.MatchSome(chl =>
                eventInvoker.InvokeOnChannelStateUpdate(new ChannelStateUpdateEvent
                {
                    Channel = chl.UpdateTags(message.Tags)
                }));
        }
        else
            return false;
            
        return true;
    }
}