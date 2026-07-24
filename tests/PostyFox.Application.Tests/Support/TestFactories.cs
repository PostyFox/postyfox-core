using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;

namespace PostyFox.Application.Tests.Support;

/// <summary>Small helpers for wiring up services in tests with sensible defaults.</summary>
internal static class TestFactories
{
    public static PostPayloadCleaner PayloadCleaner(FakeObjectStore store, string postContainer = "post") =>
        new(store,
            Microsoft.Extensions.Options.Options.Create(new PipelineOptions { PostContainer = postContainer }),
            NullLogger<PostPayloadCleaner>.Instance);
}
