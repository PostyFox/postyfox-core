namespace PostyFox.Application.Abstractions;

/// <summary>Sends transactional email (currently: account-invite delivery, see <c>AccountAccessService</c>).</summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string bodyText, CancellationToken ct = default);
}
