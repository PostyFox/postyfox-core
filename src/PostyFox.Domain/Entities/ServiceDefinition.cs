namespace PostyFox.Domain.Entities;

/// <summary>
/// Catalogue entry describing a platform a user can connect (the "AvailableServices"
/// concept). <see cref="Id"/> is the connector/platform key (e.g. "Telegram", "DiscordWH").
/// </summary>
public class ServiceDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;

    /// <summary>JSON object describing the non-secret configuration fields required.</summary>
    public string ConfigSchema { get; set; } = "{}";

    /// <summary>JSON object describing secret configuration fields (stored encrypted).</summary>
    public string? SecureConfigSchema { get; set; }

    /// <summary>Connector implementation key used to route delivery/auth operations.</summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Admin-controlled, cookie-paired platforms only. True: connector requests replay the User-Agent
    /// of the browser that paired the session. False: they always use PostyFox's default User-Agent.
    /// </summary>
    public bool UsePairedUserAgent { get; set; } = true;
}
