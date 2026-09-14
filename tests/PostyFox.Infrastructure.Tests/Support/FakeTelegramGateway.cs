using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Tests.Support;

public sealed class FakeTelegramGateway : ITelegramGateway
{
    public bool Authenticated { get; set; } = true;
    public DeliveryResult SendResult { get; set; } = DeliveryResult.Ok("msg-1");
    public bool DeleteResult { get; set; } = true;
    public (string userId, string phone, string chatId, string body)? LastSend { get; private set; }
    public (string userId, string phone, string chatId, int messageId)? LastDelete { get; private set; }
    public List<ConnectorTarget> Chats { get; } = new() { new ConnectorTarget("100", "General") };
    public Queue<TelegramLoginStep> LoginSteps { get; } = new();

    public Task<bool> IsAuthenticatedAsync(string userId, string phoneNumber, CancellationToken ct = default) => Task.FromResult(Authenticated);
    public Task<IReadOnlyList<ConnectorTarget>> ListChatsAsync(string userId, string phoneNumber, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ConnectorTarget>>(Chats);
    public int LastMediaCount { get; private set; }
    public MediaSpec? LastMediaSpec { get; private set; }
    public Task<DeliveryResult> SendAsync(string userId, string phoneNumber, string chatId, string body, IReadOnlyList<MediaRef> media, MediaSpec mediaSpec, CancellationToken ct = default)
    { LastSend = (userId, phoneNumber, chatId, body); LastMediaCount = media.Count; LastMediaSpec = mediaSpec; return Task.FromResult(SendResult); }
    public Task<bool> DeleteMessageAsync(string userId, string phoneNumber, string chatId, int messageId, CancellationToken ct = default)
    { LastDelete = (userId, phoneNumber, chatId, messageId); return Task.FromResult(DeleteResult); }
    public Task<TelegramLoginStep> LoginAsync(string userId, string phoneNumber, string? value, CancellationToken ct = default)
        => Task.FromResult(LoginSteps.Count > 0 ? LoginSteps.Dequeue() : new TelegramLoginStep(TelegramLoginStep.Complete));
}
