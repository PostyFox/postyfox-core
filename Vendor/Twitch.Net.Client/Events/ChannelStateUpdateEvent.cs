using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Events;

public class ChannelStateUpdateEvent
{
    public ChatChannel Channel { get; init; }
}