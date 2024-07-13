namespace Twitch.Net.Shared.RateLimits;

public interface IUserAccountStatusResolver
{
    Task<UserAccountStatus> ResolveUserAccountStatusAsync();
}