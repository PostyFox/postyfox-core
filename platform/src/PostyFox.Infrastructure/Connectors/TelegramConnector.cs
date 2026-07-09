using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>
/// Posts to Telegram as the user via MTProto (WTelegramClient), matching the legacy design.
/// Config: { "PhoneNumber": "..", "DefaultPostingTarget": "&lt;chatId&gt;" }. The session is
/// persisted per user in the object store; api id/hash come from the secret store. All MTProto
/// work is delegated to <see cref="ITelegramGateway"/>.
/// </summary>
public sealed class TelegramConnector(ITelegramGateway gateway) : IConnector
{
    public const string PlatformKey = "Telegram";

    public ConnectorDescriptor Describe() =>
        new(PlatformKey, "Telegram", SupportsTitle: true, SupportsMedia: false, SupportsThreads: false, 4096);

    public async Task<AuthState> IsAuthenticatedAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var phone = ConnectorJson.Field(context.ConfigJson, "PhoneNumber");
        if (string.IsNullOrWhiteSpace(phone)) return new AuthState(false, "No phone number configured");
        return new AuthState(await gateway.IsAuthenticatedAsync(context.UserId, phone!, ct));
    }

    public async Task<IReadOnlyList<ConnectorTarget>> ListTargetsAsync(ConnectorContext context, CancellationToken ct = default)
    {
        var phone = ConnectorJson.Field(context.ConfigJson, "PhoneNumber");
        if (string.IsNullOrWhiteSpace(phone)) return [];
        return await gateway.ListChatsAsync(context.UserId, phone!, ct);
    }

    public async Task<DeliveryResult> DeliverAsync(ConnectorContext context, RenderedPost post, CancellationToken ct = default)
    {
        var phone = ConnectorJson.Field(context.ConfigJson, "PhoneNumber");
        var chatId = ConnectorJson.Field(context.ConfigJson, "DefaultPostingTarget") ?? context.TargetId;
        if (string.IsNullOrWhiteSpace(phone)) return DeliveryResult.Fail("No phone number configured");
        if (string.IsNullOrWhiteSpace(chatId)) return DeliveryResult.Fail("No target chat configured");

        var body = string.IsNullOrEmpty(post.Title) ? post.Body : $"<b>{post.Title}</b>\n{post.Body}";
        return await gateway.SendAsync(context.UserId, phone!, chatId!, body, post.Media, ct);
    }
}
