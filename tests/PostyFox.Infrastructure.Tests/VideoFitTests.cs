using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Media;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class VideoFitTests
{
    private static VideoSpec Spec(int? w, int? h, long? bytes, int? dur, params string[] mimes) =>
        new(w, h, bytes, dur, mimes);

    [Fact]
    public void Within_all_limits_and_allowed_format_passes_through()
    {
        var d = VideoFit.Decide(new VideoProbeResult(1280, 720, 30, 5_000_000), Spec(1920, 1080, 50_000_000, 60, "video/mp4"), "video/mp4");
        Assert.Equal(VideoAction.Passthrough, d.Action);
    }

    [Fact]
    public void Oversized_dimensions_trigger_a_transcode_preserving_aspect_and_even_dims()
    {
        var d = VideoFit.Decide(new VideoProbeResult(3840, 2160, 30, 5_000_000), Spec(1920, 1080, null, null, "video/mp4"), "video/mp4");
        Assert.Equal(VideoAction.Transcode, d.Action);
        Assert.Equal(1920, d.TargetWidth);
        Assert.Equal(1080, d.TargetHeight);
        Assert.Equal(0, d.TargetWidth % 2);
        Assert.Equal(0, d.TargetHeight % 2);
    }

    [Fact]
    public void Over_duration_is_a_hard_fail()
    {
        var d = VideoFit.Decide(new VideoProbeResult(1280, 720, 120, 5_000_000), Spec(1920, 1080, null, 60, "video/mp4"), "video/mp4");
        Assert.Equal(VideoAction.Fail, d.Action);
        Assert.NotNull(d.Reason);
    }

    [Fact]
    public void Disallowed_format_is_converted_to_mp4()
    {
        var d = VideoFit.Decide(new VideoProbeResult(640, 480, 10, 1_000_000), Spec(1920, 1080, null, null, "video/mp4"), "video/x-matroska");
        Assert.Equal(VideoAction.Transcode, d.Action);
        Assert.Equal("video/mp4", d.TargetMime);
    }

    [Fact]
    public void Oversized_bytes_trigger_a_transcode()
    {
        var d = VideoFit.Decide(new VideoProbeResult(1280, 720, 30, 80_000_000), Spec(1920, 1080, 50_000_000, null, "video/mp4"), "video/mp4");
        Assert.Equal(VideoAction.Transcode, d.Action);
    }

    [Fact]
    public void Animated_gif_is_treated_as_mp4_video()
    {
        // A GIF that is within pixel bounds still needs converting away from image/gif.
        var d = VideoFit.Decide(new VideoProbeResult(500, 500, 3, 400_000), Spec(1920, 1080, null, null, "video/mp4"), "image/gif");
        Assert.Equal(VideoAction.Transcode, d.Action);
        Assert.Equal("video/mp4", d.TargetMime);
    }
}
