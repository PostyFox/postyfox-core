using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Twitch.Net.Communication;
using Twitch.Net.Communication.Clients;
using Twitch.Net.PubSub.Client;
using Twitch.Net.PubSub.Configurations;

namespace Twitch.Net.PubSub;

public static class PubSubClientFactory
{
    public static IServiceCollection AddTwitchPubSubClient(
        this IServiceCollection service,
        Action<PubSubCredentialConfig> config = null
        )
    {
        var configuration = new PubSubCredentialConfig();

        // pass the pre-created config to action so it can modified when being added with DI
        config?.Invoke(configuration);
            
        AddService(service, configuration);

        return service;
    }

    public static IServiceCollection AddTwitchPubSubClient(
        this IServiceCollection service,
        PubSubCredentialConfig config = null
        )
    {
        AddService(service, config ?? new PubSubCredentialConfig());

        return service;
    }
        
    private static void AddService(IServiceCollection serviceCollection, PubSubCredentialConfig config)
    {
        serviceCollection.Configure<PubSubCredentialConfig>(cfg =>
        {
            cfg.OAuth = config.OAuth;
        });
            
        serviceCollection.TryAddSingleton<IClientFactory, WebSocketClientFactory>();
        serviceCollection.TryAddSingleton<IPubSubClient, PubSubClient>();
    }
}