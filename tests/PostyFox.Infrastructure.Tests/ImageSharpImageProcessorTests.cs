using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Media;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class ImageSharpImageProcessorTests
{
    private static readonly ImageSharpImageProcessor Processor = new();

    private static byte[] MakeImage(int w, int h, string format, bool noisy = false)
    {
        using var img = new Image<Rgba32>(w, h);
        if (noisy)
        {
            // Deterministic pseudo-random pixels so JPEG can't trivially compress to a few bytes.
            img.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < row.Length; x++)
                    {
                        var v = (byte)((x * 31 + y * 17) ^ (x * y));
                        row[x] = new Rgba32(v, (byte)(v * 3), (byte)(v * 7), 255);
                    }
                }
            });
        }
        using var ms = new MemoryStream();
        if (format == "png") img.Save(ms, new PngEncoder());
        else img.Save(ms, new JpegEncoder { Quality = 95 });
        return ms.ToArray();
    }

    private static (int Width, int Height) Dimensions(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var info = Image.Identify(ms);
        return (info.Width, info.Height);
    }

    private static MediaSpec ImageOnly(int? maxW, int? maxH, long? maxBytes, params string[] allowed) =>
        new(new ImageSpec(maxW, maxH, maxBytes, allowed), new VideoSpec(null, null, null, null, []), null);

    [Fact]
    public async Task Oversized_image_is_downscaled_within_dimension_limits()
    {
        var source = new MediaContent("big.jpg", "image/jpeg", MakeImage(4000, 3000, "jpg", noisy: true));
        var spec = ImageOnly(2000, 2000, null, "image/jpeg");

        var result = await Processor.NormalizeAsync(source, spec);

        var (w, h) = Dimensions(result.Data);
        Assert.True(w <= 2000 && h <= 2000, $"got {w}x{h}");
        // Aspect ratio preserved (4:3).
        Assert.Equal(2000, w);
        Assert.Equal(1500, h);
    }

    [Fact]
    public async Task Small_image_is_not_enlarged()
    {
        var source = new MediaContent("small.png", "image/png", MakeImage(100, 80, "png"));
        var spec = ImageOnly(2000, 2000, null, "image/png");

        var result = await Processor.NormalizeAsync(source, spec);

        var (w, h) = Dimensions(result.Data);
        Assert.Equal(100, w);
        Assert.Equal(80, h);
    }

    [Fact]
    public async Task Oversized_bytes_are_reduced_under_the_budget()
    {
        var source = new MediaContent("big.jpg", "image/jpeg", MakeImage(2000, 2000, "jpg", noisy: true));
        var spec = ImageOnly(2000, 2000, 150_000, "image/jpeg");

        var result = await Processor.NormalizeAsync(source, spec);

        Assert.True(result.Data.LongLength <= 150_000, $"got {result.Data.LongLength} bytes");
    }

    [Fact]
    public async Task Unsupported_source_format_is_converted_to_an_allowed_one()
    {
        var source = new MediaContent("pic.png", "image/png", MakeImage(200, 200, "png"));
        var spec = ImageOnly(2000, 2000, null, "image/jpeg");

        var result = await Processor.NormalizeAsync(source, spec);

        Assert.Equal("image/jpeg", result.ContentType);
        Assert.EndsWith(".jpg", result.FileName);
    }

    [Fact]
    public async Task Non_image_content_passes_through_unchanged()
    {
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2d }; // "%PDF-"
        var source = new MediaContent("doc.pdf", "application/pdf", bytes);
        var spec = ImageOnly(100, 100, 10, "image/jpeg");

        var result = await Processor.NormalizeAsync(source, spec);

        Assert.Same(source, result);
    }

    [Fact]
    public async Task Image_that_cannot_fit_the_budget_throws()
    {
        var source = new MediaContent("big.jpg", "image/jpeg", MakeImage(2000, 2000, "jpg", noisy: true));
        var spec = ImageOnly(2000, 2000, 1, "image/jpeg"); // impossible budget

        await Assert.ThrowsAsync<InvalidOperationException>(() => Processor.NormalizeAsync(source, spec));
    }
}
