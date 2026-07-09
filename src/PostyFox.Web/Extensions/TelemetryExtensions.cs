using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace PostyFox.Web.Extensions;

public static class TelemetryExtensions
{
    /// <summary>
    /// Wires OpenTelemetry traces + metrics with OTLP export (endpoint from
    /// OTEL_EXPORTER_OTLP_ENDPOINT). Cloud-agnostic — points at any OTLP collector/backend.
    /// </summary>
    public static IServiceCollection AddPostyFoxTelemetry(this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter());
        return services;
    }
}
