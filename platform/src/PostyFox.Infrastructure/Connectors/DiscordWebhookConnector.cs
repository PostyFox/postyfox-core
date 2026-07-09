using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>
/// Delivers posts to a Discord channel via an incoming webhook. Config JSON: { "Webhook": "&lt;url&gt;" }.
/// </summary>
public sealed class DiscordWebhookConnector(IHttpClientFactory httpFactory, ILogger<DiscordWebhookConnector> logger) : IConnector
{
    public const string PlatformKey = "DiscordWH";
    private const int MaxContentLength = 2000;

    public ConnectorDescriptor Describe() =>
        new(PlatformKey, "Discord Web Hook", SupportsTitle: true, SupportsMedia: false, SupportsThreads: false, MaxContentLength);

    public Task<AuthState> IsAuthenticatedAsync(ConnectorContext context, CancellationToken ct = default) =>
        Task.FromResult(new AuthState(!string.IsNullOrWhiteSpace(GetWebhook(context.ConfigJson))));

    public Task<IReadOnlyList<ConnectorTarget>> ListTargetsAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var webhook = GetWebhook(context.ConfigJson);
        IReadOnlyList<ConnectorTarget> targets = string.IsNullOrWhiteSpace(webhook)
            ? []
            : [new ConnectorTarget(webhook, "Discord Webhook")];
        return Task.FromResult(targets);
    }

    public async Task<DeliveryResult> DeliverAsync(ConnectorContext context, RenderedPost post, CancellationToken ct = default)
    {
        var webhook = GetWebhook(context.ConfigJson);
        if (string.IsNullOrWhiteSpace(webhook))
            return DeliveryResult.Fail("No Discord webhook configured");

        var content = string.IsNullOrEmpty(post.Title) ? post.Body : $"**{post.Title}**\n{post.Body}";
        if (content.Length > MaxContentLength) content = content[..MaxContentLength];

        var client = httpFactory.CreateClient(nameof(DiscordWebhookConnector));
        // wait=true asks Discord to return the created message so we can capture its id.
        var url = webhook.Contains('?') ? $"{webhook}&wait=true" : $"{webhook}?wait=true";
        var response = await client.PostAsJsonAsync(url, new { content }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Discord webhook returned {Status}: {Detail}", response.StatusCode, detail);
            return DeliveryResult.Fail($"Discord webhook HTTP {(int)response.StatusCode}");
        }

        string? id = null;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("id", out var idEl)) id = idEl.GetString();
        }
        catch (JsonException) { /* empty body (204) — no id available */ }

        return DeliveryResult.Ok(id);
    }

    private static string? GetWebhook(string configJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            return doc.RootElement.TryGetProperty("Webhook", out var w) ? w.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
