using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Infrastructure.Connectors;
using PostyFox.Infrastructure.Messaging;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Infrastructure.Secrets;
using PostyFox.Infrastructure.Storage;

namespace PostyFox.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<S3Options>(config.GetSection(S3Options.SectionName));
        services.Configure<SecretStoreOptions>(config.GetSection(SecretStoreOptions.SectionName));
        services.Configure<RabbitMqOptions>(config.GetSection(RabbitMqOptions.SectionName));
        services.Configure<PipelineOptions>(config.GetSection(PipelineOptions.SectionName));

        var conn = config.GetConnectionString("Postgres")
                   ?? "Host=localhost;Port=5432;Database=postyfox;Username=postyfox;Password=postyfox";
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(conn, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IObjectStore, S3ObjectStore>();
        services.AddScoped<ISecretStore, EncryptedDbSecretStore>();

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
            new ConnectorDescriptor("Tumblr", "Tumblr", SupportsTitle: true, SupportsMedia: true, SupportsThreads: false, MaxContentLength: null),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>()));

        return services;
    }

    /// <summary>Registers the queue consumers for the posting worker.</summary>
    public static IServiceCollection AddPostingConsumers(this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqSubscriber<GenerateTargetCommand>>();
        services.AddHostedService<RabbitMqSubscriber<DeliverTargetCommand>>();
        return services;
    }
}
