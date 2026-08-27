namespace PostyFox.Application.Options;

/// <summary>Tunables for the posting pipeline.</summary>
public sealed class PipelineOptions
{
    public const string SectionName = "Pipeline";

    /// <summary>Maximum delivery attempts per target before dead-lettering.</summary>
    public int MaxDeliveryAttempts { get; set; } = 3;

    /// <summary>Base backoff (seconds) for retry; grows exponentially per attempt.</summary>
    public int RetryBaseSeconds { get; set; } = 10;

    /// <summary>Blob container used to store post payloads/media.</summary>
    public string PostContainer { get; set; } = "post";

    /// <summary>How often the scheduler polls for due scheduled posts (see <c>PostSchedulerService</c>).</summary>
    public int SchedulerPollSeconds { get; set; } = 15;

    /// <summary>Max due targets claimed and enqueued per scheduler pass (bounds a single poll tick).</summary>
    public int SchedulerBatchSize { get; set; } = 200;
}
