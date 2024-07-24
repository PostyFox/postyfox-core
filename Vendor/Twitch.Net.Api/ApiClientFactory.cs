using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Twitch.Net.Api.Client;
using Twitch.Net.Api.Configurations;
using Twitch.Net.Shared.Credential;

namespace Twitch.Net.Api;

public static class ApiClientFactory
{
    public static IServiceCollection AddTwitchApiClient(
        this IServiceCollection service,
        Action<ApiCredentialConfig> config = null
        )
    {
        var configuration = new ApiCredentialConfig();

        // pass the pre-created config to action so it can modified when being added with DI
        config?.Invoke(configuration);
            
        AddService(service, configuration);

        return service;
    }

    public static IServiceCollection AddTwitchApiClient(
        this IServiceCollection service,
        ApiCredentialConfig config = null
        )
    {
        AddService(service, config ?? new ApiCredentialConfig());

        return service;
    }

    private static void AddService(IServiceCollection serviceCollection, ApiCredentialConfig config)
    {
        serviceCollection.AddHttpClient();

        serviceCollection.Configure<ApiCredentialConfig>(cfg =>
        {
            cfg.ClientId = config.ClientId;
            cfg.ClientSecret = config.ClientSecret;
        });

        serviceCollection.Configure<TokenCredentialConfiguration>(cfg =>
        {
            cfg.ClientId = config.ClientId;
            cfg.ClientSecret = config.ClientSecret;
        });
            
        serviceCollection.TryAddSingleton<ITokenResolver, ClientCredentialTokenResolver>();
        serviceCollection.TryAddSingleton<IApiClient, ApiClient>();
    }
}