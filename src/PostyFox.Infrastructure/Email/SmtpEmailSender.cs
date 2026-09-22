using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Options;

namespace PostyFox.Infrastructure.Email;

/// <summary>Plain SMTP sender (BCL <see cref="SmtpClient"/>) configured from <see cref="EmailOptions"/>.</summary>
public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string bodyText, CancellationToken ct = default)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.EnableSsl,
            Credentials = string.IsNullOrEmpty(o.User) ? null : new NetworkCredential(o.User, o.Password)
        };
        var from = string.IsNullOrWhiteSpace(o.From) ? o.User : o.From;
        using var message = new MailMessage(from, to, subject, bodyText);
        await client.SendMailAsync(message, ct);
    }
}
