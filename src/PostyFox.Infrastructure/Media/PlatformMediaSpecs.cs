using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Media;

/// <summary>
/// Static per-platform media constraints for the connectors that normalize media in-process
/// (Discord, Telegram). Node-delivered platforms (Bluesky, Tumblr, Fediverse) declare their own
/// specs in the connectors-node service, where the bytes are fetched and normalized. Numbers are
/// conservative defaults chosen to sit safely inside each platform's documented caps.
/// </summary>
public static class PlatformMediaSpecs
{
    public static MediaSpec Discord { get; } = new(
        Image: new ImageSpec(4096, 4096, 10_485_760, ["image/jpeg", "image/png", "image/webp", "image/gif"]),
        Video: new VideoSpec(1920, 1080, 10_485_760, null, ["video/mp4", "video/webm"]),
        MaxAttachments: 10);

    public static MediaSpec Telegram { get; } = new(
        Image: new ImageSpec(2560, 2560, 10_485_760, ["image/jpeg", "image/png"]),
        Video: new VideoSpec(1920, 1080, 2_000_000_000, null, ["video/mp4"]),
        MaxAttachments: 10);
}
