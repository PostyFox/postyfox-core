using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using PostyFox.Application.Connectors;

namespace PostyFox.Infrastructure.Media;

/// <summary>
/// Normalizes still raster images (JPEG/PNG/WebP) to a platform's <see cref="ImageSpec"/> using
/// ImageSharp: EXIF auto-orient, metadata strip, downscale-only resize, format conversion to an
/// accepted type, and iterative quality/dimension reduction to fit the byte budget. Animated images
/// and undecodable input are returned unchanged. Throws when the image cannot be brought within the
/// limits (per the "fail the target cleanly" policy).
/// </summary>
public sealed class ImageSharpImageProcessor
{
    private const int MinQuality = 40;
    private const int MinEdge = 320;
    private const int StartQuality = 90;
    // Reject absurdly large inputs before decoding (guards worker memory). ~100 megapixels.
    private const long MaxInputPixels = 100_000_000L;

    public async Task<MediaContent> NormalizeAsync(MediaContent source, MediaSpec spec, CancellationToken ct = default)
    {
        var image = spec.Image;

        // Probe first: reject enormous inputs, and pass non-images straight through.
        try
        {
            using var probe = new MemoryStream(source.Data);
            var info = await Image.IdentifyAsync(probe, ct);
            if ((long)info.Width * info.Height > MaxInputPixels)
                throw new InvalidOperationException(
                    $"image '{source.FileName}' is {info.Width}x{info.Height}, larger than the maximum supported size");
        }
        catch (UnknownImageFormatException) { return source; }
        catch (InvalidImageContentException) { return source; }

        using var ms = new MemoryStream(source.Data);
        using var img = await Image.LoadAsync(ms, ct);

        // Animated images (multi-frame) belong to the video/ffmpeg path, not here.
        if (img.Frames.Count > 1) return source;

        img.Mutate(x => x.AutoOrient());
        StripMetadata(img);

        // Downscale only — never enlarge.
        if (image.MaxWidth is { } mw && image.MaxHeight is { } mh && (img.Width > mw || img.Height > mh))
            img.Mutate(x => x.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(mw, mh) }));

        var mayHaveAlpha = MayHaveAlpha(source.ContentType);
        var outputMime = ChooseOutputMime(source.ContentType, image, mayHaveAlpha);

        // JPEG has no alpha channel — flatten onto white so transparent areas don't turn black.
        if (outputMime == "image/jpeg" && mayHaveAlpha)
            img.Mutate(x => x.BackgroundColor(Color.White));

        var bytes = await EncodeToFitAsync(img, outputMime, image.MaxBytes, ct)
            ?? throw new InvalidOperationException(
                $"image '{source.FileName}' cannot be reduced below this platform's {image.MaxBytes}-byte limit");

        return source with
        {
            Data = bytes,
            ContentType = outputMime,
            FileName = WithExtension(source.FileName, outputMime),
        };
    }

    private static void StripMetadata(Image img)
    {
        img.Metadata.ExifProfile = null;
        img.Metadata.IccProfile = null;
        img.Metadata.IptcProfile = null;
        img.Metadata.XmpProfile = null;
    }

    /// <summary>Encodes to the target format, shrinking quality then dimensions until under budget.</summary>
    private static async Task<byte[]?> EncodeToFitAsync(Image img, string mime, long? maxBytes, CancellationToken ct)
    {
        var bytes = await EncodeAsync(img, mime, StartQuality, ct);
        if (maxBytes is not { } budget || bytes.LongLength <= budget) return bytes;

        // 1) Drop encoder quality (lossy formats only).
        if (SupportsQuality(mime))
            for (var q = StartQuality - 10; q >= MinQuality; q -= 10)
            {
                bytes = await EncodeAsync(img, mime, q, ct);
                if (bytes.LongLength <= budget) return bytes;
            }

        // 2) Step the dimensions down (~85% per pass) at the quality floor until it fits or bottoms out.
        var quality = SupportsQuality(mime) ? MinQuality : StartQuality;
        var working = img;
        var owned = false;
        try
        {
            while (Math.Min(working.Width, working.Height) > MinEdge)
            {
                var w = Math.Max(1, (int)(working.Width * 0.85));
                var h = Math.Max(1, (int)(working.Height * 0.85));
                var next = working.Clone(x => x.Resize(w, h));
                if (owned) working.Dispose();
                working = next;
                owned = true;

                bytes = await EncodeAsync(working, mime, quality, ct);
                if (bytes.LongLength <= budget) return bytes;
            }
            return null; // couldn't fit even at the floor
        }
        finally
        {
            if (owned) working.Dispose();
        }
    }

    private static async Task<byte[]> EncodeAsync(Image img, string mime, int quality, CancellationToken ct)
    {
        IImageEncoder encoder = mime switch
        {
            "image/jpeg" => new JpegEncoder { Quality = quality },
            "image/webp" => new WebpEncoder { Quality = quality },
            "image/png" => new PngEncoder(),
            _ => new JpegEncoder { Quality = quality },
        };
        using var ms = new MemoryStream();
        await img.SaveAsync(ms, encoder, ct);
        return ms.ToArray();
    }

    private static bool SupportsQuality(string mime) => mime is "image/jpeg" or "image/webp";

    private static bool MayHaveAlpha(string? mime)
    {
        var m = mime?.ToLowerInvariant() ?? string.Empty;
        return m.Contains("png") || m.Contains("webp") || m.Contains("gif");
    }

    /// <summary>Keeps the source format when the platform accepts it, else picks a preferred allowed one.</summary>
    private static string ChooseOutputMime(string? sourceMime, ImageSpec spec, bool mayHaveAlpha)
    {
        var src = NormalizeMime(sourceMime);
        var allowed = spec.AllowedMimeTypes;
        if (allowed is null || allowed.Count == 0)
            return src ?? "image/jpeg"; // unconstrained — keep source

        if (src is not null && Contains(allowed, src)) return src;

        // Prefer formats that preserve transparency when the source may have alpha.
        if (mayHaveAlpha)
        {
            if (Contains(allowed, "image/webp")) return "image/webp";
            if (Contains(allowed, "image/png")) return "image/png";
        }
        if (Contains(allowed, "image/jpeg")) return "image/jpeg";
        if (Contains(allowed, "image/webp")) return "image/webp";
        if (Contains(allowed, "image/png")) return "image/png";
        return NormalizeMime(allowed[0]) ?? "image/jpeg";
    }

    private static bool Contains(IReadOnlyList<string> mimes, string mime)
    {
        foreach (var m in mimes)
            if (string.Equals(NormalizeMime(m), mime, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string? NormalizeMime(string? mime)
    {
        var m = mime?.Trim().ToLowerInvariant();
        return m switch
        {
            null or "" => null,
            "image/jpg" => "image/jpeg",
            _ => m,
        };
    }

    private static string WithExtension(string fileName, string mime)
    {
        var ext = mime switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => null,
        };
        if (ext is null) return fileName;
        var dot = fileName.LastIndexOf('.');
        var stem = dot >= 0 ? fileName[..dot] : fileName;
        return stem + ext;
    }
}
