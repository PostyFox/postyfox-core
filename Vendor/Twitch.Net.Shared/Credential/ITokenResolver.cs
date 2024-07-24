namespace Twitch.Net.Shared.Credential;

public interface ITokenResolver
{
    bool IsTokenExpired();
    Task<string> GetToken();
    Task<string> GetTokenType();
}