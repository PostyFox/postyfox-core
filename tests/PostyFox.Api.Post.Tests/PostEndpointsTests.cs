using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Api.Post.Tests.Support;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;
using PostyFox.Infrastructure.Persistence;
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
    public async Task Create_draft_is_created_with_no_targets_enqueued()
    {
        var create = await _client.PostAsJsonAsync("/api/posts", new
        {
            targets = new[] { factory.SeededConnectorId },
            title = "My draft",
            isDraft = true
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.Equal(PostRootStatus.Draft, created!.RootStatus);

        var status = await _client.GetFromJsonAsync<PostStatusDto>($"/api/posts/{created.PostId}");
        Assert.Equal(PostRootStatus.Draft, status!.RootStatus);
        Assert.Empty(status.Targets);
    }

    [Fact]
    public async Task Draft_can_be_saved_with_no_targets_at_all()
    {
        var create = await _client.PostAsJsonAsync("/api/posts", new
        {
            targets = Array.Empty<Guid>(),
            title = "Untargeted draft",
            isDraft = true
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
    }

    [Fact]
    public async Task Draft_content_can_be_fetched_updated_and_then_published()
    {
        var create = await _client.PostAsJsonAsync("/api/posts", new
        {
            targets = Array.Empty<Guid>(),
            title = "Draft v1",
            description = "first",
            isDraft = true
        });
        var created = await create.Content.ReadFromJsonAsync<CreatePostResponse>();

        var content = await _client.GetFromJsonAsync<PostContentDto>($"/api/posts/{created!.PostId}/content");
        Assert.Equal("Draft v1", content!.Title);

        var update = await _client.PutAsJsonAsync($"/api/posts/{created.PostId}", new
        {
            targets = new[] { factory.SeededConnectorId },
            title = "Draft v2",
            description = "second"
        });
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var updatedContent = await _client.GetFromJsonAsync<PostContentDto>($"/api/posts/{created.PostId}/content");
        Assert.Equal("Draft v2", updatedContent!.Title);
        Assert.Contains(factory.SeededConnectorId, updatedContent.ConnectorIds);

        var publish = await _client.PostAsync($"/api/posts/{created.PostId}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var published = await publish.Content.ReadFromJsonAsync<CreatePostResponse>();
        Assert.Equal(PostRootStatus.Queued, published!.RootStatus);

        var status = await _client.GetFromJsonAsync<PostStatusDto>($"/api/posts/{created.PostId}");
        Assert.Equal(PostRootStatus.Queued, status!.RootStatus);
        Assert.Single(status.Targets);
        Assert.Contains(factory.Bus.Messages, m => m is GenerateTargetCommand);
    }

    [Fact]
    public async Task Publishing_a_draft_with_no_valid_targets_is_bad_request()
    {
        var create = await _client.PostAsJsonAsync("/api/posts", new
        {
            targets = Array.Empty<Guid>(),
            title = "No targets",
            isDraft = true
        });
        var created = await create.Content.ReadFromJsonAsync<CreatePostResponse>();

        var publish = await _client.PostAsync($"/api/posts/{created!.PostId}/publish", null);
        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
    }

    [Fact]
    public async Task Updating_or_publishing_an_already_published_post_conflicts()
    {
        var body = new { targets = new[] { factory.SeededConnectorId }, title = "Already sent" };
        var created = await (await _client.PostAsJsonAsync("/api/posts", body)).Content.ReadFromJsonAsync<CreatePostResponse>();

        var update = await _client.PutAsJsonAsync($"/api/posts/{created!.PostId}", body);
        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);

        var publish = await _client.PostAsync($"/api/posts/{created.PostId}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
    }

    [Fact]
    public async Task Updating_or_publishing_an_unknown_post_is_not_found()
    {
        var id = Guid.NewGuid();
        var update = await _client.PutAsJsonAsync($"/api/posts/{id}", new { targets = Array.Empty<Guid>(), title = "x" });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var publish = await _client.PostAsync($"/api/posts/{id}/publish", null);
        Assert.Equal(HttpStatusCode.NotFound, publish.StatusCode);
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

    /// <summary>
    /// `targetOptions` is a Guid-keyed dictionary, so it is worth proving over real HTTP rather than
    /// only against the service: create with per-submission choices, then read them back off the
    /// duplicate endpoint that re-seeds the compose form.
    /// </summary>
    [Fact]
    public async Task Per_submission_platform_options_round_trip_through_the_api()
    {
        var connectorId = SeedFurAffinityConnector();

        var create = await _client.PostAsJsonAsync("/api/posts", new
        {
            targets = new[] { connectorId },
            title = "Filed",
            description = "x",
            tags = new[] { "t" },
            targetOptions = new Dictionary<string, Dictionary<string, string>>
            {
                [connectorId.ToString()] = new() { ["Category"] = "13", ["Species"] = "6016" }
            }
        });

        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<CreatePostResponse>();

        var duplicate = await _client.PostAsync($"/api/posts/{created!.PostId}/duplicate", null);
        var content = await duplicate.Content.ReadFromJsonAsync<PostContentDto>();
        var options = Assert.Single(content!.TargetOptions);
        Assert.Equal(connectorId, options.Key);
        Assert.Equal("13", options.Value["Category"]);
        Assert.Equal("6016", options.Value["Species"]);
    }

    [Fact]
    public async Task An_option_outside_the_platforms_choices_is_rejected()
    {
        var connectorId = SeedFurAffinityConnector();

        var create = await _client.PostAsJsonAsync("/api/posts", new
        {
            targets = new[] { connectorId },
            title = "Filed",
            tags = new[] { "t" },
            targetOptions = new Dictionary<string, Dictionary<string, string>>
            {
                [connectorId.ToString()] = new() { ["Category"] = "not-a-category" }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        var problem = await create.Content.ReadFromJsonAsync<JsonElement>();
        var error = problem.GetProperty("error").GetString()!;
        Assert.Contains("My FA", error);   // names the target, since a post can have several
        Assert.Contains("Category", error);
    }

    /// <summary>
    /// JSON dictionary keys are untouched by naming policies, so a client serialising with camelCase
    /// web defaults sends `category` for a declared `Category`. That must land on the field rather
    /// than being dropped — a dropped choice silently becomes the platform's default.
    /// </summary>
    [Fact]
    public async Task Option_field_names_are_matched_regardless_of_casing()
    {
        var connectorId = SeedFurAffinityConnector();

        var create = await _client.PostAsJsonAsync("/api/posts", new
        {
            targets = new[] { connectorId },
            title = "Filed",
            tags = new[] { "t" },
            targetOptions = new Dictionary<string, Dictionary<string, string>>
            {
                [connectorId.ToString()] = new() { ["category"] = "13", ["unknownField"] = "ignored" }
            }
        });
        var created = await create.Content.ReadFromJsonAsync<CreatePostResponse>();

        var duplicate = await _client.PostAsync($"/api/posts/{created!.PostId}/duplicate", null);
        var content = await duplicate.Content.ReadFromJsonAsync<PostContentDto>();
        var options = Assert.Single(content!.TargetOptions).Value;
        Assert.Equal("13", options["Category"]);          // stored under the schema's own spelling
        Assert.DoesNotContain("unknownField", options.Keys); // undeclared keys never reach a connector
    }

    /// <summary>
    /// FurAffinity is the platform that declares per-submission options; the fixture only seeds
    /// Discord, so add one per test (a fresh id keeps the shared fixture's rows independent).
    /// </summary>
    private Guid SeedFurAffinityConnector()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!db.ServiceDefinitions.Any(s => s.Id == "FurAffinity"))
            db.ServiceDefinitions.Add(new ServiceDefinition
            {
                Id = "FurAffinity", Name = "FurAffinity", Platform = "FurAffinity", Enabled = true,
                ConfigSchema = "{}"
            });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector
        {
            Id = id, UserId = "dev-user", ServiceDefinitionId = "FurAffinity",
            DisplayName = "My FA", ConfigJson = "{}", Enabled = true
        });
        db.SaveChanges();
        return id;
    }
}
