namespace Twitch.Net.Lib;

public class TwitchLibConfig
{
    public string CallbackUrl { get; init; } = string.Empty;
    public string SignatureSecret { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string OAuth { get; init; } = string.Empty;
}