using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Dtos;
using PostyFox.Domain.Entities;
using PostyFox.Infrastructure.Persistence;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class TermsEndpointsTests
{
    private static async Task<string?> ProblemCode(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static async Task<TermsDto> Publish(HttpClient client, string content)
    {
        var put = await client.PutAsJsonAsync("/api/admin/terms", new { content });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        return (await put.Content.ReadFromJsonAsync<TermsDto>())!;
    }

    [Fact]
    public async Task Without_terms_requests_are_not_gated()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/templates")).StatusCode);
        var status = await client.GetFromJsonAsync<TermsStatusDto>("/api/terms");
        Assert.Null(status!.Current);
    }

    [Fact]
    public async Task Published_terms_block_access_until_accepted_and_again_after_an_update()
    {
        using var factory = new CustomWebApplicationFactory { DevAdmin = true };
        using var client = factory.CreateClient();

        var v1 = await Publish(client, "# Terms v1");

        var blocked = await client.GetAsync("/api/templates");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("terms_not_accepted", await ProblemCode(blocked));

        // Exempt: reading terms, admin detection and anonymous endpoints.
        var status = await client.GetFromJsonAsync<TermsStatusDto>("/api/terms");
        Assert.Equal("# Terms v1", status!.Current!.Content);
        Assert.False(status.Accepted);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/access")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/version")).StatusCode);

        var accept = await client.PostAsJsonAsync("/api/terms/accept", new { version = v1.Version });
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/templates")).StatusCode);

        var v2 = await Publish(client, "# Terms v2");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/templates")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/terms/accept", new { version = v1.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/terms/accept", new { version = v2.Version })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/templates")).StatusCode);
    }

    [Fact]
    public async Task Admin_can_replace_or_turn_off_terms_without_accepting_them()
    {
        using var factory = new CustomWebApplicationFactory { DevAdmin = true };
        using var client = factory.CreateClient();
        await Publish(client, "# Bad terms");

        var fixedTerms = await Publish(client, "# Fixed terms");
        Assert.Equal("# Fixed terms", (await client.GetFromJsonAsync<TermsStatusDto>("/api/terms"))!.Current!.Content);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/templates")).StatusCode);

        var off = await client.PutAsJsonAsync("/api/admin/terms", new { content = "  " });
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        Assert.Null((await client.GetFromJsonAsync<TermsStatusDto>("/api/terms"))!.Current);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/templates")).StatusCode);
        Assert.True(fixedTerms.Version > 1);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(scope.ServiceProvider.GetRequiredService<AppDbContext>().TermsAcceptances);
    }

    [Fact]
    public async Task Publishing_requires_the_admin_role()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var put = await client.PutAsJsonAsync("/api/admin/terms", new { content = "# Terms" });

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Api_key_access_is_blocked_until_the_key_owner_accepts()
    {
        using var factory = new CustomWebApplicationFactory { DevAdmin = true };
        using var client = factory.CreateClient();
        var key = await (await client.PostAsJsonAsync("/api/profile/keys", new { name = "script" }))
            .Content.ReadFromJsonAsync<ApiKeyCreatedDto>();
        var v1 = await Publish(client, "# Terms");

        using var external = factory.CreateClient();
        external.DefaultRequestHeaders.Add("X-API-Key", key!.ApiKey);
        var blocked = await external.GetAsync("/api/templates");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("terms_not_accepted", await ProblemCode(blocked));

        await client.PostAsJsonAsync("/api/terms/accept", new { version = v1.Version });
        Assert.Equal(HttpStatusCode.OK, (await external.GetAsync("/api/templates")).StatusCode);
    }

    [Fact]
    public async Task Acting_as_another_account_requires_the_owner_to_have_accepted_too()
    {
        using var factory = new CustomWebApplicationFactory { DevAdmin = true };
        using var client = factory.CreateClient();
        var v1 = await Publish(client, "# Terms");
        await client.PostAsJsonAsync("/api/terms/accept", new { version = v1.Version });
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AccountMembers.Add(new AccountMember
            {
                OwnerUserId = "owner-1",
                OwnerEmail = "owner@example.com",
                MemberUserId = "dev-user",
                MemberEmail = "dev-user@example.com",
                InviteId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        client.DefaultRequestHeaders.Add("X-Act-As", "owner-1");

        var blocked = await client.GetAsync("/api/templates");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("owner_terms_not_accepted", await ProblemCode(blocked));
        var status = await client.GetFromJsonAsync<TermsStatusDto>("/api/terms");
        Assert.True(status!.Accepted);
        Assert.False(status.OwnerAccepted);

        // Accepting while acting as the owner records the member's own acceptance, not the owner's.
        await client.PostAsJsonAsync("/api/terms/accept", new { version = v1.Version });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/templates")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TermsAcceptances.Add(new TermsAcceptance { UserId = "owner-1", TermsVersion = v1.Version, AcceptedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/templates")).StatusCode);
    }
}
