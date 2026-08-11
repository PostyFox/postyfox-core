using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Neillans.Adapters.Secrets.Core;
using PostyFox.Api.Core.Tests.Support;
using PostyFox.Application.Services;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class AdminEndpointsTests
{
    [Fact]
    public async Task Operational_secrets_require_the_admin_role()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();

        var access = await client.GetAsync("/api/admin/access");
        var response = await client.GetAsync("/api/admin/operational-secrets");

        Assert.Equal(HttpStatusCode.Forbidden, access.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_can_set_list_and_delete_without_values_being_returned()
    {
        using var factory = new CustomWebApplicationFactory { DevAdmin = true };
        using var client = factory.CreateClient();

        var put = await client.PutAsJsonAsync(
            $"/api/admin/operational-secrets/{OperationalSecretService.TelegramApiId}",
            new { value = "123456" });
        var responseBody = await put.Content.ReadAsStringAsync();
        var list = await client.GetFromJsonAsync<List<OperationalSecretStatus>>(
            "/api/admin/operational-secrets");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.DoesNotContain("123456", responseBody);
        Assert.True(list!.Single(item => item.Key == OperationalSecretService.TelegramApiId).Configured);

        using (var scope = factory.Services.CreateScope())
            Assert.Equal("123456", await scope.ServiceProvider.GetRequiredService<ISecretsProvider>()
                .GetSecretAsync(OperationalSecretService.TelegramApiId));

        var delete = await client.DeleteAsync(
            $"/api/admin/operational-secrets/{OperationalSecretService.TelegramApiId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

}
