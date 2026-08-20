using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Media;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>
/// Posts to Telegram as the user via MTProto (WTelegramClient), matching the legacy design.
/// Config: { "PhoneNumber": "..", "DefaultPostingTarget": "&lt;chatId&gt;" }. The session is
/// persisted per user in the object store; api id/hash come from the secret store. All MTProto
/// work is delegated to <see cref="ITelegramGateway"/>.
///
/// TARGETS: one Telegram login (phone number) can reach many chats/channels, so this connector
/// declares <see cref="ConnectorDescriptor.SupportsMultipleTargets"/>. Users expose the specific
/// chats they want selectable at compose time as <see cref="Domain.Entities.ConnectorDestination"/>
/// rows (populated from <see cref="ListTargetsAsync"/>); <see cref="ConnectorContext.TargetId"/>
/// then carries the chosen chat id per post-target, taking priority over the connector's
/// <c>DefaultPostingTarget</c> fallback (kept for backward compatibility with connectors configured
/// before per-post destination selection existed).
/// </summary>
public sealed class TelegramConnector(ITelegramGateway gateway) : IConnector
{
    public const string PlatformKey = "Telegram";

    public ConnectorDescriptor Describe() =>
        new(PlatformKey, "Telegram", SupportsTitle: true, SupportsMedia: true, SupportsThreads: false, 4096,
            MediaSpec: PlatformMediaSpecs.Telegram, SupportsMultipleTargets: true, SupportsTags: false);

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
        // An explicitly selected destination (one of the connector's exposed ConnectorDestinations)
        // always wins over the connector-wide default, so the same login can fan out to several chats.
        var chatId = context.TargetId ?? ConnectorJson.Field(context.ConfigJson, "DefaultPostingTarget");
        if (string.IsNullOrWhiteSpace(phone)) return DeliveryResult.Fail("No phone number configured");
        if (string.IsNullOrWhiteSpace(chatId)) return DeliveryResult.Fail("No target chat configured");

        var body = string.IsNullOrEmpty(post.Title) ? post.Body : $"<b>{post.Title}</b>\n{post.Body}";
        var mediaSpec = Describe().MediaSpec ?? MediaSpec.Unconstrained;
        return await gateway.SendAsync(context.UserId, phone!, chatId!, body, post.Media, mediaSpec, ct);
    }
}
