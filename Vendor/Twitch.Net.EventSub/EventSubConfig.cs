namespace Twitch.Net.EventSub;

public class EventSubConfig
{
    public string CallbackUrl { get; set; } = string.Empty;
    public string SignatureSecret { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}