using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Twitch.Net.Client.Client;
using Twitch.Net.Client.Configurations;
using Twitch.Net.Communication;
using Twitch.Net.Communication.Clients;
using Twitch.Net.Shared.Configurations;
using Twitch.Net.Shared.Credential;
using Twitch.Net.Shared.RateLimits;

namespace Twitch.Net.Client;

public static class IrcClientFactory
{
    public static IServiceCollection AddTwitchIrcClient(
        this IServiceCollection service,
        Action<IrcCredentialConfig> ircConfig = null,
        Action<TokenCredentialConfiguration> tokenConfig = null
        )
    {
        var ircConfiguration = new IrcCredentialConfig();
        var tokenConfiguration = new TokenCredentialConfiguration();

        // pass the pre-created ircConfig to action so it can modified when being added with DI
        ircConfig?.Invoke(ircConfiguration);
        tokenConfig?.Invoke(tokenConfiguration);
            
        AddService(service, ircConfiguration, tokenConfiguration);

        return service;
    }

    public static IServiceCollection AddTwitchIrcClient(
        this IServiceCollection service,
        IrcCredentialConfig ircConfig = null,
        TokenCredentialConfiguration tokenConfig = null
        )
    {
        AddService(
            service,
            ircConfig ?? new IrcCredentialConfig(),
            tokenConfig ?? new TokenCredentialConfiguration()
        );

        return service;
    }

    private static void AddService(
        IServiceCollection serviceCollection,
        IrcCredentialConfig ircConfig,
        TokenCredentialConfiguration tokenConfig
        )
    {
        serviceCollection.AddHttpClient();

        serviceCollection.Configure<IrcCredentialConfig>(cfg =>
        {
            cfg.Username = ircConfig.Username;
            cfg.OAuth = ircConfig.OAuth;
        });

        serviceCollection.Configure<AccountCredentialConfiguration>(cfg =>
        {
            cfg.Username = ircConfig.Username;
        });

        serviceCollection.Configure<TokenCredentialConfiguration>(cfg =>
        {
            cfg.ClientId = tokenConfig.ClientId;
            cfg.ClientSecret = tokenConfig.ClientSecret;
        });

        serviceCollection.TryAddSingleton<IClientFactory, WebSocketClientFactory>();
        serviceCollection.TryAddSingleton<IUserAccountStatusResolver, UserAccountResolver>();
        serviceCollection.TryAddSingleton<ITokenResolver, ClientCredentialTokenResolver>();
        serviceCollection.TryAddSingleton<IIrcClient, IrcClient>();
    }
}