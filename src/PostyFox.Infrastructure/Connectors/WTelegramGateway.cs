using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using TL;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>
/// MTProto gateway backed by WTelegramClient. Sessions are persisted per user in the object
/// store; interactive login clients are held in-process for the duration of the login flow.
///
/// STATEFULNESS: the login-flow client cache is per-instance. Route all Telegram operations for a
/// given user to a single instance (e.g. a dedicated telegram-worker with consistent hashing on
/// userId) so concurrent replicas do not corrupt an in-progress session — see the reimplementation
/// plan §4.5.
/// </summary>
public sealed class WTelegramGateway(
    IObjectStore objectStore,
    IServiceScopeFactory scopeFactory,
    ILogger<WTelegramGateway> logger) : ITelegramGateway
{
    private readonly ConcurrentDictionary<string, WTelegram.Client> _loginClients = new();
    private (int apiId, string apiHash)? _api;

    private async Task<(int apiId, string apiHash)> ApiAsync(CancellationToken ct)
    {
        if (_api is { } a) return a;
        await using var scope = scopeFactory.CreateAsyncScope();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretsProvider>();
        var id = await secrets.GetSecretAsync("TelegramApiID", ct);
        var hash = await secrets.GetSecretAsync("TelegramApiHash", ct);
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(hash))
            throw new InvalidOperationException("Telegram api id/hash not configured in the secret store");
        _api = (int.Parse(id!), hash!);
        return _api.Value;
    }

    private async Task<WTelegram.Client> CreateClientAsync(string userId, string phone, CancellationToken ct)
    {
        var (apiId, apiHash) = await ApiAsync(ct);
        var store = await BlobSessionStore.OpenAsync(objectStore, userId, ct);
        return new WTelegram.Client(what => what switch
        {
            "api_id" => apiId.ToString(),
            "api_hash" => apiHash,
            "phone_number" => phone,
            _ => null
        }, store);
    }

    public async Task<bool> IsAuthenticatedAsync(string userId, string phoneNumber, CancellationToken ct = default)
    {
        try
        {
            using var client = await CreateClientAsync(userId, phoneNumber, ct);
            await client.LoginUserIfNeeded();
            return client.UserId != 0;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Telegram session for {User} is not authenticated", userId);
            return false;
        }
    }

    public async Task<IReadOnlyList<ConnectorTarget>> ListChatsAsync(string userId, string phoneNumber, CancellationToken ct = default)
    {
        using var client = await CreateClientAsync(userId, phoneNumber, ct);
        await client.LoginUserIfNeeded();
        if (client.UserId == 0) return [];

        var chats = await client.Messages_GetAllChats();
        var result = new List<ConnectorTarget>();
        foreach (var (id, chat) in chats.chats)
            if (chat.IsActive)
                result.Add(new ConnectorTarget(id.ToString(), chat.Title));
        return result;
    }

    public async Task<DeliveryResult> SendAsync(string userId, string phoneNumber, string chatId, string body, IReadOnlyList<MediaRef> media, CancellationToken ct = default)
    {
        try
        {
            using var client = await CreateClientAsync(userId, phoneNumber, ct);
            await client.LoginUserIfNeeded();
            if (client.UserId == 0) return DeliveryResult.Fail("Telegram session not authenticated");
            if (!long.TryParse(chatId, out var id)) return DeliveryResult.Fail($"Invalid Telegram chat id '{chatId}'");

            var chats = await client.Messages_GetAllChats();
            if (!chats.chats.TryGetValue(id, out var chat))
                return DeliveryResult.Fail($"Chat {chatId} not accessible");

            var peer = chat.ToInputPeer();
            var text = body;
            var entities = client.HtmlToEntities(ref text);

            if (media.Count == 0)
            {
                var message = await client.SendMessageAsync(peer, text, entities: entities);
                return DeliveryResult.Ok(message.id.ToString());
            }

            var content = await MediaFetcher.FetchAsync(objectStore, media, ct);

            if (content.Count == 1)
            {
                using var ms = new MemoryStream(content[0].Data);
                var file = await client.UploadFileAsync(ms, content[0].FileName);
                var msg = await client.SendMediaAsync(peer, text, file, content[0].ContentType, entities: entities);
                return DeliveryResult.Ok(msg.id.ToString());
            }

            // Multiple media → a single grouped album; caption/formatting on the album.
            var album = new List<InputMedia>(content.Count);
            foreach (var m in content)
            {
                using var ms = new MemoryStream(m.Data);
                var file = await client.UploadFileAsync(ms, m.FileName);
                album.Add(m.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                    ? new InputMediaUploadedPhoto { file = file }
                    : new InputMediaUploadedDocument { file = file, mime_type = m.ContentType });
            }
            var messages = await client.SendAlbumAsync(peer, album, text, entities: entities);
            return DeliveryResult.Ok(messages.FirstOrDefault()?.id.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Telegram send failed for {User}", userId);
            return DeliveryResult.Fail(ex.Message);
        }
    }

    public async Task<TelegramLoginStep> LoginAsync(string userId, string phoneNumber, string? value, CancellationToken ct = default)
    {
        var client = _loginClients.GetOrAdd(userId, _ => CreateClientAsync(userId, phoneNumber, ct).GetAwaiter().GetResult());
        var next = await client.Login(value ?? phoneNumber);
        switch (next)
        {
            case "verification_code":
                return new TelegramLoginStep(TelegramLoginStep.NeedsCode, "value", "Verification Code");
            case "password":
                return new TelegramLoginStep(TelegramLoginStep.NeedsPassword, "value", "2FA Password");
            default:
                if (_loginClients.TryRemove(userId, out var done)) done.Dispose(); // Dispose flushes the session to the object store
                return new TelegramLoginStep(TelegramLoginStep.Complete);
        }
    }
}
