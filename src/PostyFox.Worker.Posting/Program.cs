using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PostyFox.Application;
using PostyFox.Infrastructure;
using PostyFox.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPostingConsumers();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("postyfox-posting-worker"))
    .WithTracing(t => t.AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddHttpClientInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter())
    .WithLogging(
        l => l.AddOtlpExporter(),
        o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

var app = builder.Build();

// Liveness – always returns ok if the process is running
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Readiness – confirms the worker can reach the database
app.MapGet("/readyz", async (AppDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(503)).AllowAnonymous();

app.Run();
