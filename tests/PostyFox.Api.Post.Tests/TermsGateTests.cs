using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Api.Post.Tests.Support;
using PostyFox.Domain.Entities;
using PostyFox.Infrastructure.Persistence;
using Xunit;

namespace PostyFox.Api.Post.Tests;

/// <summary>Issue #417: the post API enforces the same terms gate as core-api.</summary>
public class TermsGateTests
{
    [Fact]
    public async Task Post_api_is_blocked_until_the_current_terms_are_accepted()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TermsOfService.Add(new TermsOfService { Content = "# Terms", PublishedByUserId = "admin", PublishedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/posts")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TermsAcceptances.Add(new TermsAcceptance { UserId = "dev-user", TermsVersion = 1, AcceptedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/posts")).StatusCode);
    }
}
