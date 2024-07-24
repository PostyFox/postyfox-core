using Twitch.Net.Client.Irc;

namespace Twitch.Net.Client.Events;

public class UnknownMessageEvent
{
    public IrcMessage Parsed { get; init; }
    public string Raw { get; init; }
}