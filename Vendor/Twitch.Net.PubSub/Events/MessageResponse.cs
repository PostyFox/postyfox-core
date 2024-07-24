namespace Twitch.Net.PubSub.Events;

public class MessageResponse
{
    public string Error { get; init; }
    public string Nonce { get; init; }
    public bool Successful => string.IsNullOrEmpty(Error);
}