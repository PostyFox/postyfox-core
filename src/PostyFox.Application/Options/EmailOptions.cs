namespace PostyFox.Application.Options;

/// <summary>SMTP settings for outbound transactional email (currently: account-invite delivery).</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>SMTP relay host.</summary>
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool EnableSsl { get; set; } = true;

    /// <summary>"From" address on outbound mail. Falls back to <see cref="User"/> when blank.</summary>
    public string From { get; set; } = string.Empty;
}
