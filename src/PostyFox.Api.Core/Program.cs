using Microsoft.EntityFrameworkCore;
using PostyFox.Api.Core.Endpoints;
using PostyFox.Application;
using PostyFox.Infrastructure;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPostyFoxAuth(builder.Configuration);
builder.Services.AddPostyFoxTelemetry("postyfox-core-api");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddPostyFoxRateLimiting();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("ApplyMigrations"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

if (builder.Configuration.GetValue<bool>("SeedServiceDefinitions"))
{
    using var scope = app.Services.CreateScope();
    await ServiceDefinitionSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
}

app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "PostyFox Core API");
    o.RoutePrefix = "swagger";
});
app.UsePostyFoxSecurityHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/readyz", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503)).AllowAnonymous();

app.MapProfileEndpoints();
app.MapServiceEndpoints();
app.MapTemplateEndpoints();
app.MapTriggerEndpoints();
app.MapMediaEndpoints();

app.Run();

/// <summary>Exposed for integration testing via WebApplicationFactory.</summary>
public partial class Program;
