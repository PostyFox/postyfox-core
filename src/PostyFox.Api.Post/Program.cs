using PostyFox.Api.Post.Endpoints;
using PostyFox.Application;
using PostyFox.Infrastructure;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Web.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddPostyFoxAuth(builder.Configuration);
builder.Services.AddPostyFoxTelemetry("postyfox-post-api");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddPostyFoxRateLimiting();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapOpenApi();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "PostyFox Post API");
    o.RoutePrefix = "swagger";
});
app.UsePostyFoxSecurityHeaders();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/readyz", async (AppDbContext db) =>
    await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503)).AllowAnonymous();

app.MapPostEndpoints();
app.MapWebhookEndpoints();

app.Run();

/// <summary>Exposed for integration testing via WebApplicationFactory.</summary>
public partial class Program;
