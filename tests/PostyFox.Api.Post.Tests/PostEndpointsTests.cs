using System.Net;
using System.Net.Http.Json;
using PostyFox.Api.Post.Tests.Support;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Domain.Enums;
using Xunit;

namespace PostyFox.Api.Post.Tests;

public class PostEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_post_accepts_and_enqueues_then_status_is_queryable()
    {
        var body = new
        {
            targets = new[] { factory.SeededConnectorId },
            title = "Hello",
            description = "World",
            tags = new[] { "t" }
        };

        var create = await _client.PostAsJsonAsync("/api/posts", body);
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.NotNull(created);

        Assert.Contains(factory.Bus.Messages, m => m is GenerateTargetCommand);

        var status = await _client.GetFromJsonAsync<PostStatusDto>($"/api/posts/{created!.PostId}");
        Assert.Equal(created.PostId, status!.PostId);
        Assert.Equal(PostRootStatus.Queued, status.RootStatus);
        Assert.Single(status.Targets);
        Assert.Equal("DiscordWH", status.Targets[0].Platform);
    }

    [Fact]
    public async Task Create_post_with_no_targets_is_bad_request()
    {
        var create = await _client.PostAsJsonAsync("/api/posts", new { targets = Array.Empty<Guid>(), title = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task Unknown_post_status_is_not_found()
    {
        var resp = await _client.GetAsync($"/api/posts/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Cancel_moves_queued_post_to_cancelled_then_second_cancel_conflicts()
    {
        var body = new { targets = new[] { factory.SeededConnectorId }, title = "ToCancel", description = "x" };
        var created = await (await _client.PostAsJsonAsync("/api/posts", body)).Content.ReadFromJsonAsync<CreatePostResponse>();

        var cancel = await _client.PostAsync($"/api/posts/{created!.PostId}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var status = await _client.GetFromJsonAsync<PostStatusDto>($"/api/posts/{created.PostId}");
        Assert.Equal(PostRootStatus.Cancelled, status!.RootStatus);
        Assert.All(status.Targets, t => Assert.Equal(TargetStatus.Cancelled, t.Status));

        // Nothing left to cancel now.
        var again = await _client.PostAsync($"/api/posts/{created.PostId}/cancel", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Cancel_unknown_post_is_not_found()
    {
        var resp = await _client.PostAsync($"/api/posts/{Guid.NewGuid()}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_post_then_status_is_not_found()
    {
        var body = new { targets = new[] { factory.SeededConnectorId }, title = "ToDelete", description = "x" };
        var created = await (await _client.PostAsJsonAsync("/api/posts", body)).Content.ReadFromJsonAsync<CreatePostResponse>();

        var del = await _client.DeleteAsync($"/api/posts/{created!.PostId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var status = await _client.GetAsync($"/api/posts/{created.PostId}");
        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);

        var delAgain = await _client.DeleteAsync($"/api/posts/{created.PostId}");
        Assert.Equal(HttpStatusCode.NotFound, delAgain.StatusCode);
    }

    [Fact]
    public async Task Duplicate_returns_authored_fields_for_recreate()
    {
        var body = new
        {
            targets = new[] { factory.SeededConnectorId },
            title = "Reusable",
            description = "Body text",
            tags = new[] { "a", "b" }
        };
        var created = await (await _client.PostAsJsonAsync("/api/posts", body)).Content.ReadFromJsonAsync<CreatePostResponse>();

        var dup = await _client.PostAsync($"/api/posts/{created!.PostId}/duplicate", null);
        Assert.Equal(HttpStatusCode.OK, dup.StatusCode);
        var content = await dup.Content.ReadFromJsonAsync<PostContentDto>();
        Assert.Equal("Reusable", content!.Title);
        Assert.Equal("Body text", content.Description);
        Assert.Equal(["a", "b"], content.Tags);
        Assert.Contains(factory.SeededConnectorId, content.ConnectorIds);
    }

    [Fact]
    public async Task Duplicate_unknown_post_is_not_found()
    {
        var resp = await _client.PostAsync($"/api/posts/{Guid.NewGuid()}/duplicate", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task List_returns_created_post_and_active_filter_reflects_status()
    {
        var body = new { targets = new[] { factory.SeededConnectorId }, title = "Listed", description = "x" };
        var create = await _client.PostAsJsonAsync("/api/posts", body);
        var created = await create.Content.ReadFromJsonAsync<CreatePostResponse>();

        var all = await _client.GetFromJsonAsync<List<PostSummaryDto>>("/api/posts");
        Assert.Contains(all!, p => p.PostId == created!.PostId && p.Title == "Listed");

        // A freshly-created post is Queued (active), so it appears under filter=active too.
        var active = await _client.GetFromJsonAsync<List<PostSummaryDto>>("/api/posts?filter=active");
        Assert.Contains(active!, p => p.PostId == created!.PostId);
    }
}
