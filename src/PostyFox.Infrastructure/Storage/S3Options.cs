namespace PostyFox.Infrastructure.Storage;

public sealed class S3Options
{
    public const string SectionName = "ObjectStore";
    public string ServiceUrl { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string Bucket { get; set; } = "postyfox";
    /// <summary>Bucket for Telegram MTProto session blobs — kept separate from <see cref="Bucket"/>.</summary>
    public string? TelegramBucket { get; set; }
    public bool ForcePathStyle { get; set; } = true;
}
