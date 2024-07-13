namespace Twitch.Net.Communication.Events;

public class ClientDisconnected
{
    public ClientDisconnected(string message)
    {
        Message = message;
    }
        
    public string Message { get; init; }
}