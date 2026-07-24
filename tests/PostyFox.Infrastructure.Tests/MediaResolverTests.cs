using PostyFox.Application.Connectors;
using PostyFox.Infrastructure.Media;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class MediaResolverTests
{
    private sealed class RecordingProcessor : IMediaProcessor
    {
        public List<(string File, MediaSpec Spec)> Calls { get; } = new();
        public Task<MediaContent> NormalizeAsync(MediaContent source, MediaSpec spec, CancellationToken ct = default)
        {
            Calls.Add((source.FileName, spec));
            return Task.FromResult(source with { ContentType = "normalized" });
        }
    }

    private static MediaSpec SpecWithCap(int? cap) =>
        new(new ImageSpec(null, null, null, []), new VideoSpec(null, null, null, null, []), cap);

    [Fact]
    public async Task Enforces_attachment_cap_and_normalizes_each_item()
    {
        var store = new FakeObjectStore();
        store.Seed("media", "u1/a/one.png", [1]);
        store.Seed("media", "u1/a/two.png", [2]);
        store.Seed("media", "u1/a/three.png", [3]);
        var processor = new RecordingProcessor();
        var resolver = new MediaResolver(store, processor);
        var spec = SpecWithCap(2);

        var result = await resolver.ResolveAsync(
        [
            new MediaRef("media", "u1/a/one.png", "image/png"),
            new MediaRef("media", "u1/a/two.png", "image/png"),
            new MediaRef("media", "u1/a/three.png", "image/png"),
        ], spec);

        Assert.Equal(2, result.Count);                 // capped
        Assert.Equal(2, processor.Calls.Count);        // each surviving item normalized
        Assert.All(processor.Calls, c => Assert.Same(spec, c.Spec));
        Assert.All(result, r => Assert.Equal("normalized", r.ContentType));
        Assert.Equal(["one.png", "two.png"], processor.Calls.Select(c => c.File));
    }

    [Fact]
    public async Task Empty_refs_returns_empty_without_touching_the_processor()
    {
        var processor = new RecordingProcessor();
        var resolver = new MediaResolver(new FakeObjectStore(), processor);

        var result = await resolver.ResolveAsync([], SpecWithCap(null));

        Assert.Empty(result);
        Assert.Empty(processor.Calls);
    }
}
