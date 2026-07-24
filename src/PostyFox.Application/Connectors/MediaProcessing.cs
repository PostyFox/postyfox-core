namespace PostyFox.Application.Connectors;

/// <summary>
/// Per-platform constraints for still images. A null field means "no constraint on that axis".
/// <see cref="MaxBytes"/> is in bytes; <see cref="AllowedMimeTypes"/> is the set of output formats
/// the platform accepts (empty = no restriction).
/// </summary>
public sealed record ImageSpec(
    int? MaxWidth,
    int? MaxHeight,
    long? MaxBytes,
    IReadOnlyList<string> AllowedMimeTypes);

/// <summary>
/// Per-platform constraints for video (and animated) media. A null field means "no constraint".
/// <see cref="MaxBytes"/> is in bytes, <see cref="MaxBitrate"/> in bits/second.
/// </summary>
public sealed record VideoSpec(
    int? MaxWidth,
    int? MaxHeight,
    long? MaxBytes,
    int? MaxDurationSeconds,
    IReadOnlyList<string> AllowedMimeTypes,
    long? MaxBitrate = null);

/// <summary>
/// The complete media constraints for a platform, spanning still images and video. A connector
/// declares one on its <see cref="ConnectorDescriptor"/>; the media resolver enforces it (resize /
/// transcode / format-convert) before any bytes are uploaded, so images are always correctly sized
/// for the target platform. This is the core, shared contract every connector must honour.
/// </summary>
public sealed record MediaSpec(
    ImageSpec Image,
    VideoSpec Video,
    int? MaxAttachments = null)
{
    private static readonly IReadOnlyList<string> None = Array.Empty<string>();

    /// <summary>An unconstrained spec — every item passes through untouched.</summary>
    public static MediaSpec Unconstrained { get; } = new(
        new ImageSpec(null, null, null, None),
        new VideoSpec(null, null, null, null, None),
        null);
}

/// <summary>
/// Normalizes a single media item so it satisfies a platform's <see cref="MediaSpec"/> before
/// upload: downscales and re-encodes still images, transcodes video / animated media, and converts
/// to an accepted format. Items it cannot process (documents, unknown types) are returned unchanged.
/// Throws when an item cannot be brought within the platform's limits.
/// </summary>
public interface IMediaProcessor
{
    Task<MediaContent> NormalizeAsync(MediaContent source, MediaSpec spec, CancellationToken ct = default);
}

