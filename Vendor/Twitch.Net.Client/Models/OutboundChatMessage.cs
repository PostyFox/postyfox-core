namespace Twitch.Net.Client.Models;

internal class OutboundChatMessage
{
    public string Channel { get; init; }
    public string Username { get; init; }
    public string Message { get; init; }

    public override string ToString()
    {
        var user = Username.ToLower();
        var channel = Channel.ToLower();
        return $":{user}!{user}@{user}.tmi.twitch.tv PRIVMSG #{channel} :{Message}";
    }
}