using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Application.Posting;
using PostyFox.Application.Services;
using PostyFox.Worker.Posting.Tests.Support;
using Xunit;

namespace PostyFox.Worker.Posting.Tests;

/// <summary>
/// End-to-end coverage for {{tt:name}} resolution: a post authored once reaches two different
/// connectors with different resolved text, proving generation resolves per-target rather than once
/// for the whole post (unlike {variable} substitution, which is the same for every target).
/// </summary>
public class TextTemplatePipelineTests
{
    [Fact]
    public async Task Text_template_resolves_per_connector_and_falls_back_to_the_default()
    {
        var a = new ProgrammableConnector("DiscordWH", succeed: true);
        var b = new ProgrammableConnector("Telegram", succeed: true);
        using var h = new PipelineHarness(a, b);
        var cidA = await h.SeedConnectorAsync("u1", "DiscordWH");
        var cidB = await h.SeedConnectorAsync("u1", "Telegram"); // no override -> falls back to the default

        using (var scope = h.Services.CreateScope())
        {
            var textTemplates = scope.ServiceProvider.GetRequiredService<TextTemplateService>();
            await textTemplates.UpsertAsync("u1", new TextTemplateUpsertRequest(
                null, "mention", "friend", new Dictionary<Guid, string> { [cidA] = "@alice" }));

            var intake = scope.ServiceProvider.GetRequiredService<PostIntakeService>();
            await intake.CreateAsync("u1", new CreatePostRequest(
                [cidA, cidB], null, "Hi {{tt:mention}}!", null, null, null, null, null, null));
        }

        var targets = await h.InScopeAsync(db => db.PostTargets.ToListAsync());
        string BodyFor(Guid connectorId) => JsonDocument
            .Parse(targets.First(t => t.ConnectorId == connectorId).RenderedContentJson!)
            .RootElement.GetProperty("body").GetString()!;

        Assert.Equal("Hi @alice!", BodyFor(cidA));
        Assert.Equal("Hi friend!", BodyFor(cidB));
    }

    [Fact]
    public async Task Editing_a_text_template_after_scheduling_is_picked_up_at_generation_time()
    {
        // Generation (not intake) resolves the token, so a value changed after the post was authored
        // but before it fires reflects the newer value — same principle as {variable} resolving fresh
        // per target, just scoped to the connector instead.
        var connector = new ProgrammableConnector("DiscordWH", succeed: true);
        using var h = new PipelineHarness(connector);
        var cid = await h.SeedConnectorAsync("u1", "DiscordWH");

        Guid textTemplateId;
        using (var scope = h.Services.CreateScope())
        {
            var textTemplates = scope.ServiceProvider.GetRequiredService<TextTemplateService>();
            var created = await textTemplates.UpsertAsync("u1", new TextTemplateUpsertRequest(
                null, "mention", "old-value", new Dictionary<Guid, string>()));
            textTemplateId = created.Id;
        }

        // Persist a draft post + target directly (bypassing intake's immediate Generate) so the
        // template can be edited before Generate ever runs.
        Guid postId = Guid.NewGuid(), targetId = Guid.NewGuid();
        await h.InScopeAsync(async db =>
        {
            db.Posts.Add(new PostyFox.Domain.Entities.Post
            {
                Id = postId, UserId = "u1", Description = "{{tt:mention}}",
                RootStatus = PostyFox.Domain.Enums.PostRootStatus.Queued,
                Targets =
                {
                    new PostyFox.Domain.Entities.PostTarget
                    {
                        Id = targetId, PostId = postId, ConnectorId = cid, Platform = "DiscordWH",
                        Status = PostyFox.Domain.Enums.TargetStatus.Queued
                    }
                }
            });
            return await db.SaveChangesAsync();
        });

        using (var scope = h.Services.CreateScope())
        {
            var textTemplates = scope.ServiceProvider.GetRequiredService<TextTemplateService>();
            await textTemplates.UpsertAsync("u1", new TextTemplateUpsertRequest(
                textTemplateId, "mention", "new-value", new Dictionary<Guid, string>()));
        }

        using (var scope = h.Services.CreateScope())
        {
            var generate = scope.ServiceProvider.GetRequiredService<IMessageHandler<GenerateTargetCommand>>();
            await generate.HandleAsync(new GenerateTargetCommand { PostId = postId, TargetId = targetId }, default);
        }

        var target = await h.InScopeAsync(db => db.PostTargets.FirstAsync());
        var body = JsonDocument.Parse(target.RenderedContentJson!).RootElement.GetProperty("body").GetString();
        Assert.Equal("new-value", body);
    }
}
