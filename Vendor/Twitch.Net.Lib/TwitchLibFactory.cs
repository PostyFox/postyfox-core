using Microsoft.Extensions.DependencyInjection;
using Twitch.Net.Api;
using Twitch.Net.Client;
using Twitch.Net.EventSub;
using Twitch.Net.PubSub;

namespace Twitch.Net.Lib;

public static class TwitchLibFactory
{
    /**
     * This will add all the TwitchLib services and clients to the DI
     */
    public static IServiceCollection AddTwitchLib(
        this IServiceCollection service,
        Action<TwitchLibConfig> config = null
        )
    {
        var configuration = new TwitchLibConfig();

        // pass the pre-created config to action so it can modified when being added with DI
        config?.Invoke(configuration);
            
        AddService(service, configuration);

        return service;
    }

    /**
     * This will add all the TwitchLib services and clients to the DI
     */
    public static IServiceCollection AddTwitchLib(
        this IServiceCollection service,
        TwitchLibConfig config = null
        )
    {
        AddService(service, config ?? new TwitchLibConfig());

        return service;
    }

    private static void AddService(
        IServiceCollection serviceCollection,
        TwitchLibConfig config
        )
    {
        serviceCollection.AddTwitchIrcClient(cfg =>
            {
                cfg.Username = config.Username;
                cfg.OAuth = config.OAuth;
            },
            cfg =>
            {
                cfg.ClientId = config.ClientId;
                cfg.ClientSecret = config.ClientSecret;
            });

        serviceCollection.AddTwitchPubSubClient(cfg =>
        {
            cfg.OAuth = config.OAuth;
        });

        serviceCollection.AddTwitchApiClient(cfg =>
        {
            cfg.ClientId = config.ClientId;
            cfg.ClientSecret = config.ClientSecret;
        });

        serviceCollection.AddTwitchEventSubService(cfg =>
        {
            cfg.ClientId = config.ClientId;
            cfg.ClientSecret = config.ClientSecret;
            cfg.CallbackUrl = config.CallbackUrl;
            cfg.SignatureSecret = config.SignatureSecret;
        });
    }
}