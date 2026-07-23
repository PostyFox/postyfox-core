using Microsoft.Extensions.DependencyInjection;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;
using PostyFox.Application.Posting;
using PostyFox.Application.Security;
using PostyFox.Application.Services;
using PostyFox.Application.Templating;

namespace PostyFox.Application;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers application services, engines and pipeline handlers.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IApiKeyHasher, ApiKeyHasher>();
        services.AddSingleton<ITemplateEngine, TemplateEngine>();
        services.AddSingleton<IConnectorRegistry, ConnectorRegistry>();

        services.AddScoped<ApiKeyService>();
        services.AddScoped<ServiceCatalogService>();
        services.AddScoped<TemplateService>();
        services.AddScoped<UserConnectorService>();
        services.AddScoped<ConnectorOperationsService>();
        services.AddScoped<PostIntakeService>();
        services.AddScoped<PostStatusService>();
        services.AddScoped<PostRetentionService>();

        services.AddSingleton<Triggers.ITriggerSource, Triggers.GenericHmacTriggerSource>();
        services.AddSingleton<Triggers.ITriggerSourceRegistry, Triggers.TriggerSourceRegistry>();
        services.AddScoped<Triggers.ExternalTriggerService>();

        services.AddScoped<IMessageHandler<GenerateTargetCommand>, GenerateTargetHandler>();
        services.AddScoped<IMessageHandler<DeliverTargetCommand>, DeliverTargetHandler>();

        return services;
    }
}
