using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Twitch.Net.Shared.Credential;

namespace Twitch.Net.EventSub;

public static class EventSubServiceFactory
{
    public const string EventSubFactory = "Twitch-EventSub";
        
    public static IServiceCollection AddTwitchEventSubService(
        this IServiceCollection service,
        Action<EventSubConfig>? config = null
        )
    {
        var configuration = new EventSubConfig();

        // pass the pre-created config to action so it can modified when being added with DI
        config?.Invoke(configuration);
            
        AddService(service, configuration);

        return service;
    }

    public static IServiceCollection AddTwitchEventSubService(
        this IServiceCollection service,
        EventSubConfig? config = null
        )
    {
        AddService(service, config ?? new EventSubConfig());

        return service;
    }

    private static void AddService(IServiceCollection serviceCollection, EventSubConfig config)
    {
        serviceCollection.AddHttpClient(EventSubFactory, c =>
        {
            c.BaseAddress = new Uri("https://api.twitch.tv/");
            c.DefaultRequestHeaders.Add("Client-ID", config.ClientId);
        });

        serviceCollection.Configure<EventSubConfig>(cfg =>
        {
            cfg.CallbackUrl = config.CallbackUrl;
            cfg.SignatureSecret = config.SignatureSecret;
            cfg.ClientId = config.ClientId;
            cfg.ClientSecret = config.ClientSecret;
        });

        serviceCollection.Configure<TokenCredentialConfiguration>(cfg =>
        {
            cfg.ClientId = config.ClientId;
            cfg.ClientSecret = config.ClientSecret;
        });
            
        serviceCollection.TryAddSingleton<ITokenResolver, ClientCredentialTokenResolver>();
        serviceCollection.TryAddSingleton<EventSubModelConverter>();
        serviceCollection.TryAddSingleton<IEventSubService, EventSubService>();
    }
}