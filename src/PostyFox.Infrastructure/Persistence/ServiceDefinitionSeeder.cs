using Microsoft.EntityFrameworkCore;
using PostyFox.Domain.Entities;

namespace PostyFox.Infrastructure.Persistence;

/// <summary>
/// Seeds the platform catalogue (equivalent to the legacy AvailableServices table). Only
/// DiscordWH has a working connector in Phase 2; the others are catalogue entries whose
/// connectors land in Phase 3.
/// </summary>
public static class ServiceDefinitionSeeder
{
    // Config/secret schemas are JSON objects keyed by field name; each value is a *field descriptor*
    // carrying both presentation (label/help/placeholder/type/link) and validation (required/pattern/
    // message/min-maxLength) metadata. The frontend renders + pre-validates from these; the server
    // enforces the validation keys authoritatively (see ConfigSchemaValidator). Adding/changing a
    // field's behaviour is a server-only change: the client needs no edits.

    private const string DiscordSchema = """
        { "Webhook": {
            "label": "Webhook URL", "type": "url", "required": true,
            "placeholder": "https://discord.com/api/webhooks/…",
            "help": "Server Settings → Integrations → Webhooks → New Webhook → Copy URL.",
            "link": { "href": "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks", "text": "How to create a webhook" }
        } }
        """;

    private const string TelegramSchema = """
        { "PhoneNumber": {
            "label": "Phone number", "type": "tel", "required": true,
            "placeholder": "+1234567890",
            "help": "The phone number of the Telegram account to post as."
          } 
        }
        """;

    // Bluesky handles must NOT carry a leading "@": the AT Protocol handle resolver rejects it.
    private const string BlueSkyConfigSchema = """
        { "Handle": {
            "label": "Handle", "required": true,
            "placeholder": "yourname.bsky.social",
            "help": "Your Bluesky handle.",
            "pattern": "^[^@]",
            "message": "Enter your handle without a leading “@” (e.g. yourname.bsky.social)."
        } }
        """;

    private const string BlueSkySecureSchema = """
        { "AppPassword": {
            "label": "App password", "type": "password", "required": true,
            "help": "Create a dedicated app password — never use your main password.",
            "link": { "href": "https://bsky.app/settings/app-passwords", "text": "bsky.app/settings/app-passwords" }
        } }
        """;

    private const string TumblrSchema = """
        { "Username": {
            "label": "Blog username", "required": true,
            "placeholder": "yourblog",
            "help": "The Tumblr blog to post to."
        } }
        """;

    // Business Login for Instagram carries no per-account config (no instance URL/username to
    // enter): the connect (OAuth) flow itself determines which Instagram business/creator account
    // is linked, so there is nothing for the user to fill in here.
    private const string InstagramConfigSchema = "{}";

    // FurAffinity's connector holds nothing but the account itself: it authenticates from a browser
    // session handed over by PostyFox Connect, and its category/species/gender/folder choices belong
    // to an individual submission, not the account. Those live on the connector descriptor's
    // PostOptionsSchema and are chosen in the compose form (see ConnectorDescriptor.PostOptionsSchema).
    private const string FurAffinityConfigSchema = "{}";

    // Same reasoning as FurAffinity: authenticates from a handed-over browser session, and its
    // character/artist/privacy choices belong to an individual upload, not the account. Those live on
    // the connector descriptor's PostOptionsSchema.
    private const string ToyhouseConfigSchema = "{}";

    // X authenticates from a handed-over browser session and has nothing to configure per account.
    private const string XConfigSchema = "{}";

    // Shared by every Fediverse platform (Mastodon, Pleroma, Pixelfed, …). The connect (OAuth/MiAuth)
    // flow yields the access token, so there is no user-facing secure schema. https:// is added
    // automatically when the scheme is omitted.
    private const string FediverseSchema = """
        { "InstanceUrl": {
            "label": "Instance URL", "type": "url", "required": true,
            "placeholder": "https://your.instance",
            "help": "The URL of the server your account is on."
        } }
        """;

    public static readonly ServiceDefinition[] Definitions =
    [
        new() { Id = "DiscordWH", Name = "Discord Web Hook", Platform = "DiscordWH", Enabled = true,
                ConfigSchema = DiscordSchema },
        new() { Id = "Telegram", Name = "Telegram", Platform = "Telegram", Enabled = true,
                ConfigSchema = TelegramSchema },
        new() { Id = "BlueSky", Name = "BlueSky", Platform = "BlueSky", Enabled = true,
                ConfigSchema = BlueSkyConfigSchema, SecureConfigSchema = BlueSkySecureSchema },
        // Tumblr credentials are obtained via the OAuth "connect" flow (SupportsOAuth), not entered
        // by hand, so there is no user-facing secure config schema.
        new() { Id = "Tumblr", Name = "Tumblr", Platform = "Tumblr", Enabled = true,
                ConfigSchema = TumblrSchema, SecureConfigSchema = null },
        new() { Id = "FurAffinity", Name = "FurAffinity", Platform = "FurAffinity", Enabled = true,
                ConfigSchema = FurAffinityConfigSchema, SecureConfigSchema = null },
        new() { Id = "Toyhouse", Name = "Toyhouse", Platform = "Toyhouse", Enabled = true,
                ConfigSchema = ToyhouseConfigSchema, SecureConfigSchema = null },
        new() { Id = "X", Name = "X", Platform = "X", Enabled = true,
                ConfigSchema = XConfigSchema, SecureConfigSchema = null },
        // Instagram credentials come from the "Business Login for Instagram" OAuth flow
        // (SupportsOAuth), not entered by hand, so there is no user-facing secure config schema.
        new() { Id = "Instagram", Name = "Instagram", Platform = "Instagram", Enabled = true,
                ConfigSchema = InstagramConfigSchema, SecureConfigSchema = null },
        // Fediverse platforms: credentials come from the OAuth/MiAuth "connect" flow (SupportsOAuth),
        // not entered by hand, so there is no user-facing secure config schema. All share one config
        // schema (just the instance URL); the connector auto-detects the server software at connect.
        new() { Id = "Mastodon", Name = "Mastodon", Platform = "Mastodon", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Pleroma", Name = "Pleroma", Platform = "Pleroma", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Akkoma", Name = "Akkoma", Platform = "Akkoma", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Friendica", Name = "Friendica", Platform = "Friendica", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        // Firefish is dead upstream and has no backing connector any more (see
        // ServiceCollectionExtensions.AddInfrastructure) - kept as a disabled catalogue entry rather
        // than deleted outright so existing deployments that already seeded this row get it flipped
        // off cleanly (SeedAsync updates rows already present) instead of left dangling.
        new() { Id = "Firefish", Name = "Firefish", Platform = "Firefish", Enabled = false,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Iceshrimp", Name = "Iceshrimp", Platform = "Iceshrimp", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "GoToSocial", Name = "GoToSocial", Platform = "GoToSocial", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Hometown", Name = "Hometown", Platform = "Hometown", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
        new() { Id = "Pixelfed", Name = "Pixelfed", Platform = "Pixelfed", Enabled = true,
                ConfigSchema = FediverseSchema, SecureConfigSchema = null },
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        foreach (var def in Definitions)
        {
            var existing = await db.ServiceDefinitions.FirstOrDefaultAsync(s => s.Id == def.Id, ct);
            if (existing is null)
            {
                db.ServiceDefinitions.Add(def);
            }
            else
            {
                existing.Name = def.Name;
                existing.Platform = def.Platform;
                existing.Enabled = def.Enabled;
                existing.ConfigSchema = def.ConfigSchema;
                existing.SecureConfigSchema = def.SecureConfigSchema;
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
