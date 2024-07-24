using Microsoft.Extensions.Logging;

namespace Twitch.Net.Communication.Clients;

public class WebSocketClientFactory : IClientFactory
{
    public IClient CreateClient<T>(ILoggerFactory factory, string address = null) =>
        new WebSocketClient<T>(factory.CreateLogger<WebSocketClient<T>>(), address);
}