using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Tests.Support;

public sealed class FakeTelegramGateway : ITelegramGateway
{
    public bool Authenticated { get; set; } = true;
    public DeliveryResult SendResult { get; set; } = DeliveryResult.Ok("msg-1");
    public (string userId, string phone, string chatId, string body)? LastSend { get; private set; }
    public List<ConnectorTarget> Chats { get; } = new() { new ConnectorTarget("100", "General") };
    public Queue<TelegramLoginStep> LoginSteps { get; } = new();

    public Task<bool> IsAuthenticatedAsync(string userId, string phoneNumber, CancellationToken ct = default) => Task.FromResult(Authenticated);
    public Task<IReadOnlyList<ConnectorTarget>> ListChatsAsync(string userId, string phoneNumber, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ConnectorTarget>>(Chats);
    public int LastMediaCount { get; private set; }
    public Task<DeliveryResult> SendAsync(string userId, string phoneNumber, string chatId, string body, IReadOnlyList<MediaRef> media, CancellationToken ct = default)
    { LastSend = (userId, phoneNumber, chatId, body); LastMediaCount = media.Count; return Task.FromResult(SendResult); }
    public Task<TelegramLoginStep> LoginAsync(string userId, string phoneNumber, string? value, CancellationToken ct = default)
        => Task.FromResult(LoginSteps.Count > 0 ? LoginSteps.Dequeue() : new TelegramLoginStep(TelegramLoginStep.Complete));
}
