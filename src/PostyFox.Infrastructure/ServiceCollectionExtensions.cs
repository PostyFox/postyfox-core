using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Neillans.Adapters.Secrets.AzureKeyVault;
using Neillans.Adapters.Secrets.BitWarden;
using Neillans.Adapters.Secrets.Core;
using Neillans.Adapters.Secrets.HashiCorpVault;
using Neillans.Adapters.Secrets.Infisical;
using Neillans.Adapters.Secrets.InMemory;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Application.Posting;
using PostyFox.Infrastructure.Connectors;
using PostyFox.Infrastructure.Media;
using PostyFox.Infrastructure.Messaging;
using PostyFox.Infrastructure.Persistence;
using PostyFox.Infrastructure.Storage;

namespace PostyFox.Infrastructure;

public static class ServiceCollectionExtensions
{
    // Same field-descriptor format as ServiceDefinition.ConfigSchema (see ServiceDefinitionSeeder).
    // A blank/omitted value means "no content warning": the connector never falls back to the post
    // title or anything else authored elsewhere.
    private const string FediversePostOptionsSchema = """
        { "ContentWarning": {
            "label": "Content warning",
            "placeholder": "Leave blank to post without one",
            "help": "Hides the post body behind a click-to-reveal warning showing this text. Independent of the post title.",
            "maxLength": 500
        } }
        """;

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<S3Options>(config.GetSection(S3Options.SectionName));
        services.Configure<RabbitMqOptions>(config.GetSection(RabbitMqOptions.SectionName));
        services.Configure<PipelineOptions>(config.GetSection(PipelineOptions.SectionName));
        services.Configure<RetentionOptions>(config.GetSection(RetentionOptions.SectionName));
        services.Configure<ConnectorRefreshOptions>(config.GetSection(ConnectorRefreshOptions.SectionName));
        services.Configure<MediaOptions>(config.GetSection(MediaOptions.SectionName));

        var conn = config.GetConnectionString("Postgres")
                   ?? "Host=localhost;Port=5432;Database=postyfox;Username=postyfox;Password=postyfox";
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql(conn, npg => npg.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IObjectStore, S3ObjectStore>();
        AddSecretsProvider(services, config);

        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IMessageBus, RabbitMqMessageBus>();

        // --- Media normalization (core, shared by every in-process connector) ---
        // Images/video are resized/transcoded to each platform's limits before upload, so nothing is
        // ever sent at the wrong size/format. Node-delivered platforms normalize in connectors-node.
        services.AddSingleton<ImageSharpImageProcessor>();
        services.AddSingleton<FfmpegVideoProcessor>();
        services.AddSingleton<IMediaProcessor, MediaProcessor>();
        services.AddSingleton<IMediaResolver, MediaResolver>();

        // --- Connectors ---
        // Discord webhook (in-process HTTP)
        services.AddHttpClient(nameof(DiscordWebhookConnector));
        services.AddSingleton<IConnector, DiscordWebhookConnector>();

        // Telegram: MTProto user account via WTelegramClient (behind a gateway seam)
        services.AddSingleton<ITelegramGateway, WTelegramGateway>();
        services.AddSingleton<IConnector, TelegramConnector>();

        // Bluesky + Tumblr: delegated to the Node connectors service over HTTP
        services.Configure<NodeConnectorsOptions>(config.GetSection(NodeConnectorsOptions.SectionName));
        services.AddHttpClient(nameof(HttpConnector));
        services.AddSingleton<IConnector>(sp => new HttpConnector(
            "BlueSky",
            new ConnectorDescriptor(
                "BlueSky",
                "Bluesky",
                SupportsTitle: false,
                SupportsMedia: true,
                SupportsThreads: true,
                MaxContentLength: 300,
                SupportsRating: true,
                SupportsTags: false,
                // Bluesky's atproto agent supports both natively: see HttpConnector's IRepostConnector/
                // IDeleteConnector implementation and bluesky.ts in connectors-node.
                SupportsRepost: true,
                SupportsDelete: true),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpConnector>>(),
            sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IConnector>(sp => new HttpConnector(
            "Tumblr",
            new ConnectorDescriptor("Tumblr", "Tumblr", SupportsTitle: true, SupportsMedia: true, SupportsThreads: false, MaxContentLength: null, SupportsOAuth: true, SupportsTags: true,
                // Tumblr has no native "reblog your own post" concept worth automating; deleting a
                // post is a normal API call (tumblr.js deletePost).
                SupportsDelete: true),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpConnector>>(),
            sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IConnector>(sp => new HttpConnector(
            "FurAffinity",
            new ConnectorDescriptor(
                "FurAffinity",
                "FurAffinity",
                SupportsTitle: true,
                SupportsMedia: true,
                SupportsThreads: false,
                MaxContentLength: null,
                // FurAffinity has no API: delivery reuses the user's browser session, handed over by
                // the PostyFox Connect extension. `a`/`b` are its session cookie pair, always present
                // once logged in. `cf_clearance` is Cloudflare's own challenge-passed cookie, bound to
                // the browser that solved it (UA and IP): optional because it only exists after a
                // recent challenge, so requiring it would break pairing for anyone who hasn't hit one
                // lately. Carrying it over when present (alongside the paired UA, see
                // ConnectorCookiePairingService) avoids a guaranteed re-challenge on the very first
                // server-side request, though a differing egress IP can still invalidate it regardless.
                CookiePairing: new CookiePairingSpec(
                    SiteUrl: "https://www.furaffinity.net/",
                    LoginUrl: "https://www.furaffinity.net/login",
                    CookieNames: ["a", "b"],
                    OptionalCookieNames: ["cf_clearance"]),
                SupportsRating: true,
                RequiresRating: true,
                SupportsTags: true,
                RequiresTags: true,
                // Category/theme/species/gender/folders are chosen per submission on FurAffinity's own
                // form, so they belong to the post rather than the account. The lists run to ~500
                // entries, see Persistence/Schemas/README.md for provenance and regeneration.
                PostOptionsSchema: EmbeddedSchema.Load("furaffinity-post-options.schema.json"),
                // A post with no media becomes a journal, which takes no tags or rating.
                SupportsTextOnly: true
                // No SupportsRepost/SupportsDelete: FurAffinity has no API, so both would mean scripting
                // another multi-step, CSRF-guarded browser-session form flow. Deliberately left out of
                // this pass rather than shipped untested against the real site.
                ),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpConnector>>(),
            sp.GetRequiredService<IServiceScopeFactory>()));
        services.AddSingleton<IConnector>(sp => new HttpConnector(
            "Toyhouse",
            new ConnectorDescriptor(
                "Toyhouse",
                "Toyhouse",
                // Images are attached to character pages rather than posted with their own title, and
                // the site has no tags field either (see PostOptionsSchema for the actual per-upload
                // choices: caption maps from the post body, character IDs, artist credit, privacy).
                SupportsTitle: false,
                SupportsMedia: true,
                SupportsThreads: false,
                MaxContentLength: 255,
                // Toyhouse has no API either: same browser-session handoff as FurAffinity, fronted by
                // Cloudflare just as aggressively (even the anonymous homepage returns a JS challenge).
                // The required cookie is Laravel's own "remember me" cookie, not `laravel_session`:
                // that session cookie is short-lived (Laravel's default is 120 minutes of inactivity)
                // and gets purged from the jar once expired, so most of the time a browser that's
                // "logged in" from the user's perspective (auto re-authenticated via remember-me on
                // its next visit) simply won't have one to hand over. `remember_web_<hash>` is
                // long-lived (years) and alone is enough for Laravel's SessionGuard to re-authenticate
                // a request and mint a fresh session — the hash is `sha1('Illuminate\Auth\SessionGuard')`,
                // a fixed constant for any default-configuration Laravel app's "web" guard, not a
                // per-site or per-user secret. `laravel_session`/`cf_clearance` are collected as
                // optional extras when present, never required.
                CookiePairing: new CookiePairingSpec(
                    SiteUrl: "https://toyhou.se/",
                    LoginUrl: "https://toyhou.se/~account/login",
                    CookieNames: ["remember_web_59ba36addc2b2f9401580f014c7f58ea4e30989d"],
                    OptionalCookieNames: ["laravel_session", "cf_clearance"]),
                SupportsRating: true,
                RequiresRating: true,
                SupportsTags: false,
                RequiresTags: false,
                // An upload is an image on a character page; the connector rejects a post with none.
                RequiresMedia: true,
                // Character IDs, artist credit, and privacy/watermark choices are chosen per upload on
                // Toyhouse's own form, so they belong to the post rather than the account, the same
                // reasoning as FurAffinity's category/species/gender/folders.
                PostOptionsSchema: EmbeddedSchema.Load("toyhouse-post-options.schema.json")
                // No SupportsRepost/SupportsDelete: Toyhouse has no API, so both would mean scripting
                // another multi-step, CSRF-guarded browser-session form flow. Deliberately left out of
                // this pass rather than shipped untested against the real site.
                ),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpConnector>>(),
            sp.GetRequiredService<IServiceScopeFactory>()));

        // Fediverse platforms: all delivered by the megalodon connector in the Node service, all via
        // an instance-scoped OAuth/MiAuth connect flow. They differ only in display name and the
        // default max content length (a UI hint; instances configure their own limit). MaxContentLength
        // null means "no client-side cap".
        void AddFediverse(string platform, string displayName, int? maxContentLength) =>
            services.AddSingleton<IConnector>(sp => new HttpConnector(
                platform,
                new ConnectorDescriptor(
                    platform, displayName, SupportsTitle: false, SupportsMedia: true, SupportsThreads: false,
                    MaxContentLength: maxContentLength, SupportsOAuth: true, SupportsTags: false,
                    // The content warning is authored per submission (like FurAffinity's category etc.)
                    // rather than assumed from the post title, see megalodon.ts's use of this field.
                    PostOptionsSchema: FediversePostOptionsSchema,
                    SupportsContentWarning: true,
                    // megalodon's client exposes reblogStatus/deleteStatus for every driver this app
                    // uses (see megalodon.ts).
                    SupportsRepost: true,
                    SupportsDelete: true),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpConnector>>(),
                sp.GetRequiredService<IServiceScopeFactory>()));

        // Instagram: Business Login for Instagram (OAuth2), delegated to the Node connectors
        // service. Requires at least one image/video (RequiresMedia — Instagram has no text-only
        // post type); has no native tags field (hashtags are woven into the caption body); no
        // rating/repost/delete support in this pass. Caption cap is Instagram's documented 2200
        // characters.
        services.AddSingleton<IConnector>(sp => new HttpConnector(
            "Instagram",
            new ConnectorDescriptor(
                "Instagram", "Instagram", SupportsTitle: false, SupportsMedia: true, SupportsThreads: false,
                MaxContentLength: 2200, SupportsOAuth: true, SupportsTags: false, RequiresMedia: true),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NodeConnectorsOptions>>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpConnector>>(),
            sp.GetRequiredService<IServiceScopeFactory>()));

        AddFediverse("Mastodon", "Mastodon", 500);
        AddFediverse("Pleroma", "Pleroma", 5000);
        AddFediverse("Akkoma", "Akkoma", 5000);
        AddFediverse("Friendica", "Friendica", null);
        AddFediverse("Iceshrimp", "Iceshrimp", 3000);
        AddFediverse("GoToSocial", "GoToSocial", 5000);
        AddFediverse("Hometown", "Hometown", 500);
        AddFediverse("Pixelfed", "Pixelfed", 500);

        return services;
    }

    /// <summary>
    /// Registers an <see cref="ISecretsProvider"/> from the Neillans.Adapters.Secrets library based on
    /// the <c>Secrets:Provider</c> configuration value (<c>InMemory</c>, <c>BitWarden</c>,
    /// <c>AzureKeyVault</c>, <c>HashiCorpVault</c> or <c>Infisical</c>). Provider-specific options are
    /// bound from the matching <c>Secrets:{Provider}</c> sub-section. Defaults to <c>InMemory</c> when
    /// unset so local dev works out of the box; deployed stacks select a persistent store (the docker
    /// dev/prod stacks default to <c>Secrets:Provider=HashiCorpVault</c>).
    /// </summary>
    private static void AddSecretsProvider(IServiceCollection services, IConfiguration config)
    {
        var section = config.GetSection("Secrets");
        var providerName = section["Provider"];
        var provider = string.IsNullOrWhiteSpace(providerName)
            ? SecretsProviderType.InMemory
            : Enum.Parse<SecretsProviderType>(providerName, ignoreCase: true);

        switch (provider)
        {
            case SecretsProviderType.InMemory:
                services.AddInMemorySecretsProvider();
                break;

            case SecretsProviderType.BitWarden:
                services.AddBitWardenSecretsProvider(o => section.GetSection("BitWarden").Bind(o));
                break;

            case SecretsProviderType.AzureKeyVault:
                services.AddAzureKeyVaultSecretsProvider(o => section.GetSection("AzureKeyVault").Bind(o));
                break;

            case SecretsProviderType.HashiCorpVault:
                services.AddHashiCorpVaultSecretsProvider(o => section.GetSection("HashiCorpVault").Bind(o));
                break;

            case SecretsProviderType.Infisical:
                services.AddInfisicalSecretsProvider(o => section.GetSection("Infisical").Bind(o));
                break;

            default:
                throw new NotSupportedException($"Unknown secrets provider '{provider}'.");
        }
    }

    /// <summary>Registers the queue consumers for the posting worker.</summary>
    public static IServiceCollection AddPostingConsumers(this IServiceCollection services)
    {
        services.AddHostedService<RabbitMqSubscriber<GenerateTargetCommand>>();
        services.AddHostedService<RabbitMqSubscriber<DeliverTargetCommand>>();
        services.AddHostedService<RabbitMqSubscriber<ExecuteAutomationCommand>>();
        services.AddHostedService<PostRetentionSweeper>();
        services.AddHostedService<ConnectorTokenRefreshSweeper>();
        services.AddScoped<PostSchedulerService>();
        services.AddHostedService<PostSchedulerSweeper>();
        services.AddHostedService<PostAutomationSweeper>();
        return services;
    }
}
