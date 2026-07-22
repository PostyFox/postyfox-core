using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PostyFox.Web.Extensions;

public static class TelemetryExtensions
{
    /// <summary>
    /// Wires OpenTelemetry traces + metrics + logs with OTLP export (endpoint from
    /// OTEL_EXPORTER_OTLP_ENDPOINT). Cloud-agnostic — points at any OTLP collector/backend.
    /// </summary>
    public static IServiceCollection AddPostyFoxTelemetry(this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => t
                .AddSource("PostyFox.Messaging")   // producer spans + trace context injected into RabbitMQ messages
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()   // GC/heap/threads/exceptions per service
                .AddOtlpExporter())
            .WithLogging(
                l => l.AddOtlpExporter(),
                // Emit the rendered message + scopes so log bodies are readable in OpenSearch,
                // not just structured state.
                o =>
                {
                    o.IncludeFormattedMessage = true;
                    // IncludeScopes stays OFF: ASP.NET Core emits the same key across nested scopes
                    // (e.g. HttpMethod/ConnectionId from the Kestrel + hosting scopes), which the OTLP
                    // exporter sends as duplicate log attributes — Data Prepper/OpenSearch then reject
                    // the whole record ("Duplicate key log.attributes.HttpMethod"). Trace↔log
                    // correlation is unaffected (it comes from the span context, not scopes).
                    o.IncludeScopes = false;
                });
        return services;
    }
}
