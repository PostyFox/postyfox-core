using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PostyFox.Application;
using PostyFox.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPostingConsumers();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("postyfox-posting-worker"))
    .WithTracing(t => t.AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddHttpClientInstrumentation().AddOtlpExporter());

var host = builder.Build();
host.Run();
