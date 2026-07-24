using FFMpegCore;
using Microsoft.Extensions.Logging;
using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Media;

/// <summary>
/// Normalizes video (and animated GIF) to a platform's <see cref="VideoSpec"/> using ffmpeg (via
/// FFMpegCore): probes the source, and either passes it through when already within limits or
/// downscales / bitrate-caps / transcodes it to an accepted container. Static GIFs and
/// non-probeable input pass through. Throws when the media cannot be brought within the limits
/// (per the "fail the target cleanly" policy). Requires the <c>ffmpeg</c>/<c>ffprobe</c> binaries.
/// </summary>
public sealed class FfmpegVideoProcessor(ILogger<FfmpegVideoProcessor> logger)
{
    public async Task<MediaContent> NormalizeAsync(MediaContent source, MediaSpec spec, CancellationToken ct = default)
    {
        var v = spec.Video;
        var dir = Directory.CreateTempSubdirectory("postyfox-video-");
        var input = Path.Combine(dir.FullName, SafeName(source.FileName, ".bin"));
        try
        {
            await File.WriteAllBytesAsync(input, source.Data, ct);

            IMediaAnalysis analysis;
            try
            {
                analysis = await FFProbe.AnalyseAsync(input);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "ffprobe failed for {File}; passing media through unchanged", source.FileName);
                return source;
            }

            var vs = analysis.PrimaryVideoStream;
            if (vs is null) return source; // no video stream — nothing to do

            var duration = analysis.Duration.TotalSeconds;

            // A single-frame GIF is effectively a still image — leave it untouched.
            var isGif = (source.ContentType ?? string.Empty).Contains("gif", StringComparison.OrdinalIgnoreCase);
            if (isGif && duration <= 0.1) return source;

            var probe = new VideoProbeResult(vs.Width, vs.Height, duration, source.Data.LongLength);
            var decision = VideoFit.Decide(probe, v, source.ContentType);

            if (decision.Action == VideoAction.Fail)
                throw new InvalidOperationException(decision.Reason ?? "video does not meet this platform's limits");
            if (decision.Action == VideoAction.Passthrough)
                return source;

            var ext = ExtensionFor(decision.TargetMime);
            var output = Path.Combine(dir.FullName, "out" + ext);

            var bitrateKbps = 0;
            if (v.MaxBytes is { } maxBytes && duration > 0.5)
                // Leave ~15% headroom for audio + container overhead.
                bitrateKbps = (int)(maxBytes * 8L / duration / 1000 * 0.85);
            if (v.MaxBitrate is { } mbr)
            {
                var cap = (int)(mbr / 1000);
                bitrateKbps = bitrateKbps > 0 ? Math.Min(bitrateKbps, cap) : cap;
            }

            var needsResize = decision.TargetWidth != probe.Width || decision.TargetHeight != probe.Height;
            var webm = decision.TargetMime.Contains("webm", StringComparison.OrdinalIgnoreCase);

            var ok = await FFMpegArguments
                .FromFileInput(input)
                .OutputToFile(output, overwrite: true, o =>
                {
                    if (webm) o.WithVideoCodec("libvpx-vp9").WithAudioCodec("libopus");
                    else o.WithVideoCodec("libx264").WithAudioCodec("aac").WithFastStart();
                    if (needsResize) o.WithVideoFilters(f => f.Scale(decision.TargetWidth, decision.TargetHeight));
                    if (bitrateKbps > 0) o.WithVideoBitrate(bitrateKbps);
                })
                .CancellableThrough(ct)
                .ProcessAsynchronously();

            if (!ok)
                throw new InvalidOperationException($"ffmpeg failed to transcode '{source.FileName}'");

            var bytes = await File.ReadAllBytesAsync(output, ct);
            if (v.MaxBytes is { } budget && bytes.LongLength > budget)
                throw new InvalidOperationException(
                    $"video '{source.FileName}' is {bytes.LongLength} bytes after transcoding, above this platform's {budget}-byte limit");

            return source with
            {
                Data = bytes,
                ContentType = decision.TargetMime,
                FileName = WithExtension(source.FileName, ext),
            };
        }
        finally
        {
            try { dir.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    private static string SafeName(string fileName, string fallbackExt)
    {
        var name = Path.GetFileName(fileName);
        return string.IsNullOrWhiteSpace(name) ? "input" + fallbackExt : name;
    }

    private static string ExtensionFor(string mime) => mime.ToLowerInvariant() switch
    {
        "video/webm" => ".webm",
        _ => ".mp4",
    };

    private static string WithExtension(string fileName, string ext)
    {
        var dot = fileName.LastIndexOf('.');
        var stem = dot >= 0 ? fileName[..dot] : fileName;
        return stem + ext;
    }
}
