using Microsoft.Extensions.Logging;
using Twitch.Net.Communication.Clients;

namespace Twitch.Net.Communication;

public interface IClientFactory
{
    IClient CreateClient<T>(ILoggerFactory factory, string address = null);
}