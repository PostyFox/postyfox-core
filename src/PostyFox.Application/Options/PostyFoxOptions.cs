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
}
