using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Dtos;
using Xunit;

namespace PostyFox.Api.Core.Tests;

/// <summary>Issue #420: opt-in Gravatar, proxied through /api/profile/avatar.</summary>
public class AvatarEndpointsTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private FakeAvatarProvider Avatars =>
        (FakeAvatarProvider)factory.Services.GetRequiredService<IAvatarProvider>();

    private HttpClient ClientWithEmail(string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Auth-Request-Email", email);
        return client;
    }

    [Fact]
    public async Task Avatar_is_opt_in_and_404s_when_none_registered()
    {
        using var client = ClientWithEmail(FakeAvatarProvider.EmailWithAvatar);

        var initial = await client.GetFromJsonAsync<UserSettingsDto>("/api/profile/settings");
        Assert.False(initial!.UseGravatar);

        var disabled = await client.GetAsync("/api/profile/avatar");
        Assert.Equal(HttpStatusCode.NotFound, disabled.StatusCode);
        Assert.Empty(Avatars.Requests);

        var put = await client.PutAsJsonAsync("/api/profile/settings",
            new { includeAdvertisingLine = false, useGravatar = true });
        Assert.True((await put.Content.ReadFromJsonAsync<UserSettingsDto>())!.UseGravatar);

        var enabled = await client.GetAsync("/api/profile/avatar?size=1000");
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        Assert.Equal("image/png", enabled.Content.Headers.ContentType?.MediaType);
        Assert.Equal(FakeAvatarProvider.Png, await enabled.Content.ReadAsByteArrayAsync());
        Assert.Contains("private", enabled.Headers.CacheControl!.ToString());
        Assert.Contains(Avatars.Requests, r => r is { Email: FakeAvatarProvider.EmailWithAvatar, Size: 512 });

        using var noGravatar = ClientWithEmail("nobody@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await noGravatar.GetAsync("/api/profile/avatar")).StatusCode);

        using var noEmail = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await noEmail.GetAsync("/api/profile/avatar")).StatusCode);
    }
}
