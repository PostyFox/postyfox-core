using Optional.Collections;
using Twitch.Net.Client.Client;
using Twitch.Net.Client.Client.Handlers.Events;
using Twitch.Net.Client.Irc;

namespace Twitch.Net.Client.Events.Handlers;

public class UserJoinedEventHandler : IHandler
{
    private readonly IIrcClient _client;

    public UserJoinedEventHandler(IIrcClient client)
    {
        _client = client;
    }
        
    public bool Handle(IIrcClientEventInvoker eventInvoker, IrcMessage message)
    {
        if (!string.IsNullOrEmpty(message.Channel) && !string.IsNullOrEmpty(message.User))
        {
            var channel = _client.Channels.FirstOrNone(
                c => c.ChannelName.Equals(message.Channel, StringComparison.OrdinalIgnoreCase));
            channel.MatchSome(c =>
                eventInvoker.InvokeOnUserJoinedChannel(new JoinedChannelEvent
                {
                    Username = message.User,
                    Channel = c
                }));
        }
        else
            return false;
            
        return true;
    }
}