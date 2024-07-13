namespace Twitch.Net.PubSub.Events;

public class UnknownMessageEvent : EventArgs
{
    public Dictionary<string, object> Data { get; init; } = new();
    public string Raw { get; init; } = string.Empty;
}