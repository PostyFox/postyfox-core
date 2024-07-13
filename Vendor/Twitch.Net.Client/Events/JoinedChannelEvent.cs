using Twitch.Net.Client.Models;

namespace Twitch.Net.Client.Events;

public class JoinedChannelEvent
{
    public string Username { get; init; }
    public ChatChannel Channel { get; init; }
}