using System.Security.Cryptography;
using System.Text;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Application.Tests.Support;
using PostyFox.Application.Triggers;
using PostyFox.Domain.Entities;
using Xunit;

namespace PostyFox.Application.Tests;

public class ExternalTriggerServiceTests
{
    private const string Secret = "topsecret";

    private sealed record Harness(
        TestDbContext Db, ExternalTriggerService Svc, FakeBus Bus, FixedClock Clock, FakeSecretStore Secrets);

    private static Harness Build(DateTimeOffset now)
    {
        var db = TestDbContext.Create();
        var bus = new FakeBus();
        var clock = new FixedClock(now);
        var secrets = new FakeSecretStore();
        secrets.Store["trigger-generic-signing"] = Secret;
        var intake = new PostIntakeService(db, new FakeObjectStore(), bus, clock,
            Microsoft.Extensions.Options.Options.Create(new PipelineOptions()));
        var sources = new TriggerSourceRegistry([new GenericHmacTriggerSource()]);
        return new Harness(db, new ExternalTriggerService(db, secrets, clock, sources, intake), bus, clock, secrets);
    }

    private static async Task<Guid> SeedConnectorAsync(TestDbContext db, string userId)
    {
        db.ServiceDefinitions.Add(new ServiceDefinition { Id = "DiscordWH", Name = "d", Platform = "DiscordWH", Enabled = true });
        var id = Guid.NewGuid();
        db.UserConnectors.Add(new UserConnector { Id = id, UserId = userId, ServiceDefinitionId = "DiscordWH", DisplayName = "d", Enabled = true });
        await db.SaveChangesAsync();
        return id;
    }

    private static Dictionary<string, string> Signed(string body, string messageId)
    {
        var sig = Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body)));
        return new(StringComparer.OrdinalIgnoreCase) { ["X-Signature"] = sig, ["X-Message-Id"] = messageId };
    }

    [Fact]
    public async Task Register_requires_valid_connector()
    {
        var h = Build(DateTimeOffset.UnixEpoch);
        Assert.Null(await h.Svc.RegisterAsync("u1", new TriggerRegistrationRequest("generic", "acme", null, Guid.NewGuid(), 24)));

        var cid = await SeedConnectorAsync(h.Db, "u1");
        Assert.NotNull(await h.Svc.RegisterAsync("u1", new TriggerRegistrationRequest("generic", "acme", null, cid, 24)));
    }

    [Fact]
    public async Task Unknown_source_and_bad_signature_are_rejected()
    {
        var h = Build(DateTimeOffset.UnixEpoch);
        Assert.Equal(WebhookOutcome.UnknownSource, (await h.Svc.HandleWebhookAsync("nope", new Dictionary<string, string>(), "{}")).Outcome);

        var badHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["X-Signature"] = "bad", ["X-Message-Id"] = "m" };
        Assert.Equal(WebhookOutcome.Unauthorized, (await h.Svc.HandleWebhookAsync("generic", badHeaders, "{\"account\":\"acme\"}")).Outcome);
    }

    [Fact]
    public async Task Valid_webhook_fans_out_and_creates_a_post()
    {
        var h = Build(DateTimeOffset.UnixEpoch);
        var cid = await SeedConnectorAsync(h.Db, "u1");
        await h.Svc.RegisterAsync("u1", new TriggerRegistrationRequest("generic", "acme", null, cid, 24));

        var body = "{\"account\":\"acme\",\"variables\":{}}";
        var result = await h.Svc.HandleWebhookAsync("generic", Signed(body, "m1"), body);

        Assert.Equal(WebhookOutcome.Processed, result.Outcome);
        Assert.Equal(1, result.FiredCount);
        Assert.Single(h.Db.Posts);
        Assert.Contains(h.Bus.Of<GenerateTargetCommand>(), _ => true);
        Assert.NotNull(h.Db.ExternalTriggers.Single().LastFiredAt);
    }

    [Fact]
    public async Task Duplicate_message_is_ignored()
    {
        var h = Build(DateTimeOffset.UnixEpoch);
        var cid = await SeedConnectorAsync(h.Db, "u1");
        await h.Svc.RegisterAsync("u1", new TriggerRegistrationRequest("generic", "acme", null, cid, 0));
        var body = "{\"account\":\"acme\"}";

        var first = await h.Svc.HandleWebhookAsync("generic", Signed(body, "dup"), body);
        var second = await h.Svc.HandleWebhookAsync("generic", Signed(body, "dup"), body);

        Assert.Equal(WebhookOutcome.Processed, first.Outcome);
        Assert.Equal(WebhookOutcome.AlreadyProcessed, second.Outcome);
        Assert.Single(h.Db.Posts);
    }

    [Fact]
    public async Task Frequency_throttle_skips_within_window()
    {
        var now = DateTimeOffset.UnixEpoch;
        var h = Build(now);
        var cid = await SeedConnectorAsync(h.Db, "u1");
        await h.Svc.RegisterAsync("u1", new TriggerRegistrationRequest("generic", "acme", null, cid, 24));

        var body = "{\"account\":\"acme\"}";
        await h.Svc.HandleWebhookAsync("generic", Signed(body, "m1"), body);
        h.Clock.UtcNow = now.AddHours(1); // within the 24h window
        var second = await h.Svc.HandleWebhookAsync("generic", Signed(body, "m2"), body);

        Assert.Equal(0, second.FiredCount);
        Assert.Single(h.Db.Posts);
    }

    [Fact]
    public async Task Challenge_body_is_echoed()
    {
        var h = Build(DateTimeOffset.UnixEpoch);
        var result = await h.Svc.HandleWebhookAsync("generic", new Dictionary<string, string>(), "{\"challenge\":\"echo-me\"}");
        Assert.Equal(WebhookOutcome.Challenge, result.Outcome);
        Assert.Equal("echo-me", result.Challenge);
    }
}
