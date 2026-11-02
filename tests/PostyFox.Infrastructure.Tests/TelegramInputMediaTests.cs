using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Connectors;
using TL;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class TelegramInputMediaTests
{
    private static readonly InputFile File = new() { id = 1, parts = 1, name = "f" };

    [Fact]
    public void Images_are_sent_as_photos()
    {
        var media = WTelegramGateway.ToInputMedia(new MediaContent("a.png", "image/png", []), File);

        Assert.IsType<InputMediaUploadedPhoto>(media);
    }

    [Fact]
    public void A_probed_video_is_sent_as_a_streamable_video()
    {
        var media = WTelegramGateway.ToInputMedia(
            new MediaContent("a.mp4", "video/mp4", [], Width: 1280, Height: 720, DurationSeconds: 6.5), File);

        var document = Assert.IsType<InputMediaUploadedDocument>(media);
        Assert.Equal("video/mp4", document.mime_type);
        var video = Assert.IsType<DocumentAttributeVideo>(Assert.Single(document.attributes));
        Assert.Equal(1280, video.w);
        Assert.Equal(720, video.h);
        Assert.Equal(6.5, video.duration);
        Assert.True(video.flags.HasFlag(DocumentAttributeVideo.Flags.supports_streaming));
    }

    [Fact]
    public void Unprobed_video_and_other_files_are_plain_documents()
    {
        foreach (var item in new[] { new MediaContent("a.mp4", "video/mp4", []), new MediaContent("a.pdf", "application/pdf", []) })
        {
            var document = Assert.IsType<InputMediaUploadedDocument>(WTelegramGateway.ToInputMedia(item, File));
            Assert.Null(document.attributes);
        }
    }
}
