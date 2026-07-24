using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Media;

/// <summary>What a probed video needs to satisfy a <see cref="VideoSpec"/>.</summary>
public enum VideoAction { Passthrough, Transcode, Fail }

/// <summary>Probe facts about a source video, independent of the probing tool (testable).</summary>
public readonly record struct VideoProbeResult(int Width, int Height, double DurationSeconds, long Bytes);

/// <summary>The decision + target parameters for normalizing a video.</summary>
public readonly record struct VideoDecision(
    VideoAction Action, int TargetWidth, int TargetHeight, string TargetMime, string? Reason);

/// <summary>
/// Pure fit logic for video, factored out so it can be unit-tested without ffmpeg. Downscale-only
/// (aspect preserved); over-duration is a hard fail (we do not silently truncate); format is
/// converted to an accepted container when the source isn't allowed.
/// </summary>
public static class VideoFit
{
    public static VideoDecision Decide(VideoProbeResult probe, VideoSpec spec, string? sourceMime)
    {
        if (spec.MaxDurationSeconds is { } maxDur && probe.DurationSeconds > maxDur + 0.5)
            return new(VideoAction.Fail, probe.Width, probe.Height, NormalizeMime(sourceMime) ?? "video/mp4",
                $"video is {probe.DurationSeconds:0}s but this platform allows at most {maxDur}s");

        var (tw, th) = FitWithin(probe.Width, probe.Height, spec.MaxWidth, spec.MaxHeight);
        var needsResize = tw != probe.Width || th != probe.Height;

        var targetMime = ChooseMime(sourceMime, spec.AllowedMimeTypes);
        var needsConvert = !string.Equals(targetMime, NormalizeMime(sourceMime), StringComparison.OrdinalIgnoreCase);
        var overBytes = spec.MaxBytes is { } mb && probe.Bytes > mb;

        if (!needsResize && !needsConvert && !overBytes)
            return new(VideoAction.Passthrough, probe.Width, probe.Height, targetMime, null);

        return new(VideoAction.Transcode, tw, th, targetMime, null);
    }

    /// <summary>Scales (w,h) down to fit within (maxW,maxH) preserving aspect; never enlarges. Even dims (H.264).</summary>
    public static (int Width, int Height) FitWithin(int w, int h, int? maxW, int? maxH)
    {
        if (w <= 0 || h <= 0) return (w, h);
        double scale = 1.0;
        if (maxW is { } mw && w > mw) scale = Math.Min(scale, (double)mw / w);
        if (maxH is { } mh && h > mh) scale = Math.Min(scale, (double)mh / h);
        if (scale >= 1.0) return (w, h);
        var nw = Math.Max(2, (int)Math.Round(w * scale));
        var nh = Math.Max(2, (int)Math.Round(h * scale));
        return (nw - (nw % 2), nh - (nh % 2));
    }

    private static string ChooseMime(string? sourceMime, IReadOnlyList<string> allowed)
    {
        var src = NormalizeMime(sourceMime);
        if (allowed is null || allowed.Count == 0) return src ?? "video/mp4";
        if (src is not null)
            foreach (var m in allowed)
                if (string.Equals(NormalizeMime(m), src, StringComparison.OrdinalIgnoreCase)) return src;
        // Prefer mp4 (H.264/AAC) — the most broadly accepted.
        foreach (var m in allowed)
            if (string.Equals(NormalizeMime(m), "video/mp4", StringComparison.OrdinalIgnoreCase)) return "video/mp4";
        return NormalizeMime(allowed[0]) ?? "video/mp4";
    }

    private static string? NormalizeMime(string? mime)
    {
        var m = mime?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(m) ? null : m;
    }
}
