using Twitch.Net.Api.Apis.Helix;

namespace Twitch.Net.Api.Client;

public interface IApiClient
{
    IApiHelix Helix { get; }
}