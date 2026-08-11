using System.Text.Json;
using Neillans.Adapters.Secrets.Core;

namespace PostyFox.Application.Services;

public sealed record OperationalSecretStatus(
    string Key,
    string Component,
    string DisplayName,
    string Description,
    bool Configured);

public sealed class OperationalSecretService(ISecretsProvider secrets)
{
    public const string TelegramApiId = "TelegramApiID";
    public const string TelegramApiHash = "TelegramApiHash";
    public const string TumblrConsumerKey = "TumblrConsumerKey";
    public const string TumblrConsumerSecret = "TumblrConsumerSecret";

    private static readonly IReadOnlyList<Definition> Definitions =
    [
        new(TelegramApiId, "Telegram", "API ID", "Telegram application API ID used by MTProto."),
        new(TelegramApiHash, "Telegram", "API hash", "Telegram application API hash used by MTProto."),
        new(TumblrConsumerKey, "Tumblr", "Consumer key", "Tumblr OAuth application consumer key."),
        new(TumblrConsumerSecret, "Tumblr", "Consumer secret", "Tumblr OAuth application consumer secret.")
    ];

    public async Task<IReadOnlyList<OperationalSecretStatus>> ListAsync(CancellationToken ct = default)
    {
        var values = await secrets.GetSecretsAsync(Definitions.Select(definition => definition.Key), ct);
        return Definitions.Select(definition => new OperationalSecretStatus(
            definition.Key,
            definition.Component,
            definition.DisplayName,
            definition.Description,
            values.TryGetValue(definition.Key, out var value) &&
            !string.IsNullOrWhiteSpace(value))).ToList();
    }

    public async Task<OperationalSecretStatus?> SetAsync(string key, string value, CancellationToken ct = default)
    {
        var definition = Find(key);
        if (definition is null) return null;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Secret value cannot be empty.", nameof(value));

        await secrets.SetSecretAsync(definition.Key, value, ct);
        return new OperationalSecretStatus(
            definition.Key, definition.Component, definition.DisplayName, definition.Description, true);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        var definition = Find(key);
        if (definition is null) return false;
        await secrets.DeleteSecretAsync(definition.Key, ct);
        return true;
    }

    public async Task<string?> ConnectorCredentialsJsonAsync(string platform, CancellationToken ct = default)
    {
        if (!platform.Equals("Tumblr", StringComparison.OrdinalIgnoreCase)) return null;

        var values = await secrets.GetSecretsAsync([TumblrConsumerKey, TumblrConsumerSecret], ct);
        values.TryGetValue(TumblrConsumerKey, out var key);
        values.TryGetValue(TumblrConsumerSecret, out var secret);
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret)) return null;
        return JsonSerializer.Serialize(new { consumerKey = key, consumerSecret = secret });
    }

    private static Definition? Find(string key) =>
        Definitions.FirstOrDefault(definition =>
            definition.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    private sealed record Definition(string Key, string Component, string DisplayName, string Description);
}
