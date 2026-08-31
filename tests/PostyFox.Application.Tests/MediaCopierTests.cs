using PostyFox.Application.Connectors;
using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using Xunit;

namespace PostyFox.Application.Tests;

public class MediaCopierTests
{
    [Fact]
    public async Task CopyAsync_copies_a_ref_the_caller_owns()
    {
        var store = new FakeObjectStore();
        store.Blobs["media/u1/original/a.png"] = "PNGDATA"u8.ToArray();
        var copier = new MediaCopier(store);

        var copy = await copier.CopyAsync("u1", new MediaRef("media", "u1/original/a.png", "image/png"));

        Assert.StartsWith("u1/", copy.Key);
        Assert.NotEqual("u1/original/a.png", copy.Key);
    }

    [Fact]
    public async Task CopyAsync_rejects_a_ref_owned_by_another_user()
    {
        var store = new FakeObjectStore();
        var copier = new MediaCopier(store);

        await Assert.ThrowsAsync<ConnectorValidationException>(() =>
            copier.CopyAsync("u1", new MediaRef("media", "u2/victim/a.png", "image/png")));
    }

    [Fact]
    public async Task CopyAsync_rejects_a_ref_outside_the_media_container()
    {
        var store = new FakeObjectStore();
        var copier = new MediaCopier(store);

        await Assert.ThrowsAsync<ConnectorValidationException>(() =>
            copier.CopyAsync("u1", new MediaRef("telegram", "u1", "application/octet-stream")));
    }
}
