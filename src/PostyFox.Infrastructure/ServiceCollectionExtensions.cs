using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neillans.Adapters.Secrets.AzureKeyVault;
using Neillans.Adapters.Secrets.BitWarden;
using Neillans.Adapters.Secrets.Core;
using Neillans.Adapters.Secrets.HashiCorpVault;
using Neillans.Adapters.Secrets.Infisical;
using Neillans.Adapters.Secrets.InMemory;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Infrastructure.Connectors;
using PostyFox.Infrastructure.Messaging;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Infrastructure.Storage;

namespace PostyFox.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<S3Options>(config.GetSection(S3Options.SectionName));
        services.Configure<RabbitMqOptions>(config.GetSection(RabbitMqOptions.SectionName));
        services.Configure<PipelineOptions>(config.GetSection(PipelineOptions.SectionName));

        var conn = config.GetConnectionString("Postgres")
                   ?? "Host=localhost;Port=5432;Database=postyfox;Username=postyfox;Password=postyfox";
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(conn, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IObjectStore, S3ObjectStore>();
        AddSecretsProvider(services, config);

        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessageBus, RabbitMqMessageBus>();

        // --- Connectors ---
        // Discord webhook (in-process HTTP)
        services.AddHttpClient(nameof(DiscordWebhookConnector));
        services.AddSingleton<IConnector, DiscordWebhookConnector>();

        // Telegram — MTProto user account via WTelegramClient (behind a gateway seam)
        services.AddSingleton<ITelegramGateway, WTelegramGateway>();
        services.AddSingleton<IConnector, TelegramConnector>();

        // Bluesky + Tumblr — delegated to the Node connectors service over HTTP
        services.Configure<NodeConnectorsOptions>(config.GetSection(NodeConnectorsOptions.SectionName));
        services.AddHttpClient(nameof(HttpConnector));
        services.AddSingleton<IConnector>(sp => new HttpConnector(
            "BlueSky",
            new ConnectorDescriptor("BlueSky", "Bluesky", SupportsTitle: false, SupportsMedia: true, SupportsThreads: true, MaxContentLength: 300),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>()));
        services.AddSingleton<IConnector>(sp => new HttpConnector(
            "Tumblr",
            new ConnectorDescriptor("Tumblr", "Tumblr", SupportsTitle: true, SupportsMedia: true, SupportsThreads: false, MaxContentLength: null, SupportsOAuth: true),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>()));

        // Fediverse platforms — all delivered by the megalodon connector in the Node service, all via
        // an instance-scoped OAuth/MiAuth connect flow. They differ only in display name and the
        // default max content length (a UI hint; instances configure their own limit). MaxContentLength
        // null means "no client-side cap".
        void AddFediverse(string platform, string displayName, int? maxContentLength) =>
            services.AddSingleton<IConnector>(sp => new HttpConnector(
                platform,
                new ConnectorDescriptor(platform, displayName, SupportsTitle: false, SupportsMedia: true, SupportsThreads: false, MaxContentLength: maxContentLength, SupportsOAuth: true),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>()));

        AddFediverse("Mastodon", "Mastodon", 500);
        AddFediverse("Pleroma", "Pleroma", 5000);
        AddFediverse("Akkoma", "Akkoma", 5000);
        AddFediverse("Friendica", "Friendica", null);
        AddFediverse("Firefish", "Firefish", 3000);
        AddFediverse("Iceshrimp", "Iceshrimp", 3000);
        AddFediverse("GoToSocial", "GoToSocial", 5000);
        AddFediverse("Hometown", "Hometown", 500);
        AddFediverse("Pixelfed", "Pixelfed", 500);

        return services;
    }

    /// <summary>
    /// Registers an <see cref="ISecretsProvider"/> from the Neillans.Adapters.Secrets library based on
    /// the <c>Secrets:Provider</c> configuration value (<c>InMemory</c>, <c>BitWarden</c>,
    /// <c>AzureKeyVault</c>, <c>HashiCorpVault</c> or <c>Infisical</c>). Provider-specific options are
    /// bound from the matching <c>Secrets:{Provider}</c> sub-section. Defaults to <c>InMemory</c> when
    /// unset so local dev works out of the box; deployed stacks select a persistent store (the docker
    /// dev/prod stacks default to <c>Secrets:Provider=HashiCorpVault</c>).
    /// </summary>
    private static void AddSecretsProvider(IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection("Secrets");
        var providerName = section["Provider"];
        var provider = string.IsNullOrWhiteSpace(providerName)
            ? SecretsProviderType.InMemory
            : Enum.Parse<SecretsProviderType>(providerName, ignoreCase: true);

        switch (provider)
        {
            case SecretsProviderType.InMemory:
                services.AddInMemorySecretsProvider();
                break;

            case SecretsProviderType.BitWarden:
                services.AddBitWardenSecretsProvider(o => section.GetSection("BitWarden").Bind(o));
                break;

            case SecretsProviderType.AzureKeyVault:
                services.AddAzureKeyVaultSecretsProvider(o => section.GetSection("AzureKeyVault").Bind(o));
                break;

            case SecretsProviderType.HashiCorpVault:
                services.AddHashiCorpVaultSecretsProvider(o => section.GetSection("HashiCorpVault").Bind(o));
                break;

            case SecretsProviderType.Infisical:
                services.AddInfisicalSecretsProvider(o => section.GetSection("Infisical").Bind(o));
                break;

            default:
                throw new NotSupportedException($"Unknown secrets provider '{provider}'.");
        }
    }

    /// <summary>Registers the queue consumers for the posting worker.</summary>
    public static IServiceCollection AddPostingConsumers(this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqSubscriber<GenerateTargetCommand>>();
        services.AddHostedService<RabbitMqSubscriber<DeliverTargetCommand>>();
        return services;
    }
}
