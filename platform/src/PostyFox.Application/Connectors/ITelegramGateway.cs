namespace PostyFox.Application.Connectors;

/// <summary>A step in the interactive MTProto login flow (phone → code → optional 2FA).</summary>
public sealed record TelegramLoginStep(string Status, string? Input = null, string? Label = null)
{
    public const string NeedsCode = "verification_code";
    public const string NeedsPassword = "password";
    public const string Complete = "complete";
}

/// <summary>
/// Abstraction over the Telegram MTProto user client (WTelegramClient). Kept as a seam so the
/// connector and login flow are unit-testable without a live Telegram connection.
/// </summary>
public interface ITelegramGateway
{
    Task<bool> IsAuthenticatedAsync(string userId, string phoneNumber, CancellationToken ct = default);
    Task<IReadOnlyList<ConnectorTarget>> ListChatsAsync(string userId, string phoneNumber, CancellationToken ct = default);
    Task<DeliveryResult> SendAsync(string userId, string phoneNumber, string chatId, string body, CancellationToken ct = default);

    /// <summary>Advances the login flow; pass the requested <paramref name="value"/> (code/password) on subsequent calls.</summary>
    Task<TelegramLoginStep> LoginAsync(string userId, string phoneNumber, string? value, CancellationToken ct = default);
}
