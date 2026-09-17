namespace PostyFox.Application.Options;

/// <summary>
/// Tunables for the background sweeper that refreshes connector access tokens ahead of expiry
/// (currently just Instagram's long-lived token: refreshable once ≥24h old, must be refreshed
/// within 60 days or the connector stops working).
/// </summary>
public sealed class ConnectorRefreshOptions
{
    public const string SectionName = "ConnectorRefresh";

    /// <summary>Master switch for the background token-refresh sweeper.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often the sweeper runs.</summary>
    public int SweepIntervalHours { get; set; } = 12;

    /// <summary>
    /// A connector's token is refreshed once its stored expiry falls within this many days —
    /// comfortably inside Instagram's "refreshable after 24h, valid for 60 days" window so a missed
    /// sweep or two doesn't risk the token lapsing before the next attempt.
    /// </summary>
    public int RefreshWithinDays { get; set; } = 10;
}
