using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Media;

/// <summary>
/// <see cref="IMediaProcessor"/> that dispatches by content type: still raster images
/// (JPEG/PNG/WebP) go to the ImageSharp processor, video and animated images to the ffmpeg
/// processor, and anything else (documents, audio, unknown types) passes through unchanged.
/// </summary>
public sealed class MediaProcessor(
    ImageSharpImageProcessor imageProcessor,
    FfmpegVideoProcessor videoProcessor) : IMediaProcessor
{
    public Task<MediaContent> NormalizeAsync(MediaContent source, MediaSpec spec, CancellationToken ct = default)
    {
        var type = (source.ContentType ?? string.Empty).ToLowerInvariant();

        // Still raster images. (Animated WebP is detected and re-routed inside the image processor.)
        if (type is "image/jpeg" or "image/jpg" or "image/png" or "image/webp")
            return imageProcessor.NormalizeAsync(source, spec, ct);

        // Video and animated GIF are handled by ffmpeg.
        if (type == "image/gif" || type.StartsWith("video/", StringComparison.Ordinal))
            return videoProcessor.NormalizeAsync(source, spec, ct);

        // Documents, audio, unknown — nothing to normalize.
        return Task.FromResult(source);
    }
}
