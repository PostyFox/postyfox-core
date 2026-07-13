using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PostyFox.Api.Core.Tests.Support;
using Xunit;

namespace PostyFox.Api.Core.Tests;

public class JwtAuthTests
{
    private const string Issuer = "http://kc.test/realms/PostyFox";
    private const string Audience = "oauth2-proxy";
    private const string Kid = "test-key-1";
    private static readonly RSA SigningRsa = RSA.Create(2048);

    private static string Jwks(RSA rsa)
    {
        var p = rsa.ExportParameters(false);
        var n = Base64UrlEncoder.Encode(p.Modulus);
        var e = Base64UrlEncoder.Encode(p.Exponent);
        return $"{{\"keys\":[{{\"kty\":\"RSA\",\"use\":\"sig\",\"alg\":\"RS256\",\"kid\":\"{Kid}\",\"n\":\"{n}\",\"e\":\"{e}\"}}]}}";
    }

    private static string Token(RSA signer, string iss = Issuer, string aud = Audience, int expMinutes = 5)
    {
        var key = new RsaSecurityKey(signer) { KeyId = Kid };
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = iss,
            Audience = aud,
            Expires = DateTime.UtcNow.AddMinutes(expMinutes),
            Claims = new Dictionary<string, object> { ["sub"] = "user-123" },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        });
    }

    private sealed class JwksStub(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    // JWKS advertises SigningRsa's public key; OIDC enabled; DevMode off.
    private static WebApplicationFactory<Program> BuildFactory() =>
        new CustomWebApplicationFactory { DevMode = false }.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Oidc:Enabled"] = "true",
                ["Auth:Oidc:Issuer"] = Issuer,
                ["Auth:Oidc:Audience"] = Audience,
                ["Auth:Oidc:JwksUrl"] = "http://jwks.test/certs"
            }));
            b.ConfigureTestServices(s =>
                s.AddHttpClient("jwks").ConfigurePrimaryHttpMessageHandler(() => new JwksStub(Jwks(SigningRsa))));
        });

    private static async Task<HttpStatusCode> CallTemplates(WebApplicationFactory<Program> f, string? bearer)
    {
        var client = f.CreateClient();
        if (bearer is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        return (await client.GetAsync("/api/templates")).StatusCode;
    }

    [Fact]
    public async Task Valid_bearer_token_is_accepted()
    {
        using var f = BuildFactory();
        Assert.Equal(HttpStatusCode.OK, await CallTemplates(f, Token(SigningRsa)));
    }

    [Fact]
    public async Task Token_signed_by_unknown_key_is_rejected()
    {
        using var f = BuildFactory();
        using var attacker = RSA.Create(2048); // not in the JWKS
        Assert.Equal(HttpStatusCode.Unauthorized, await CallTemplates(f, Token(attacker)));
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        using var f = BuildFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, await CallTemplates(f, Token(SigningRsa, expMinutes: -5)));
    }

    [Fact]
    public async Task Wrong_issuer_is_rejected()
    {
        using var f = BuildFactory();
        Assert.Equal(HttpStatusCode.Unauthorized, await CallTemplates(f, Token(SigningRsa, iss: "http://evil/realms/PostyFox")));
    }

    [Fact]
    public async Task No_credentials_is_unauthorized_and_header_is_not_trusted()
    {
        using var f = BuildFactory();
        var client = f.CreateClient();
        // A spoofed identity header must NOT authenticate outside DevMode.
        client.DefaultRequestHeaders.Add("X-Auth-Request-User", "victim");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/templates")).StatusCode);
    }
}
