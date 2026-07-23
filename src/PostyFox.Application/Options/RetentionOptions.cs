namespace PostyFox.Application.Options;

/// <summary>Tunables for post history retention (background purge of old posts).</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>Master switch for the background retention sweeper.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How many days of post history to keep. Posts (and their targets + stored payloads) older than
    /// this are hard-deleted by the sweeper. This is also the upper bound the list endpoint reports.
    /// </summary>
    public int PostRetentionDays { get; set; } = 30;

    /// <summary>How often the sweeper runs.</summary>
    public int SweepIntervalHours { get; set; } = 6;

    /// <summary>Max posts deleted per sweep pass (bounds a single transaction / object-store fan-out).</summary>
    public int SweepBatchSize { get; set; } = 500;
}
