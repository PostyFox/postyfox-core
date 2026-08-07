using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;
using PostyFox.Application.Dtos;
using PostyFox.Application.Messaging;
using PostyFox.Application.Options;
using PostyFox.Application.Telemetry;
using PostyFox.Domain.Entities;
using PostyFox.Domain.Enums;

namespace PostyFox.Application.Posting;

public sealed class PostIntakeService(
    IAppDbContext db,
    IObjectStore objectStore,
    IMessageBus bus,
    IClock clock,
    IConnectorRegistry registry,
    IOptions<PipelineOptions> options)
{
    private readonly PipelineOptions _options = options.Value;

    /// <summary>
    /// Persists a post + one target per selected connector, stores the payload, and enqueues
    /// generation for each target (delayed if scheduled). Returns null if no valid targets.
    /// </summary>
    /// <exception cref="ConnectorValidationException">
    /// A target's per-submission options fail its platform's <c>PostOptionsSchema</c>.
    /// </exception>
    public async Task<CreatePostResponse?> CreateAsync(string userId, CreatePostRequest request, CancellationToken ct = default)
    {
        var targetIds = (request.Targets ?? []).Distinct().ToList();
        if (targetIds.Count == 0) return null;

        var connectors = await db.UserConnectors
            .Include(c => c.ServiceDefinition)
            .Where(c => c.UserId == userId && c.Enabled && targetIds.Contains(c.Id))
            .ToListAsync(ct);
        if (connectors.Count == 0) return null;

        var now = clock.UtcNow;
        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title ?? string.Empty,
            Description = request.Description ?? string.Empty,
            HtmlDescription = request.HtmlDescription ?? string.Empty,
            TagsJson = Json.Serialize(request.Tags ?? []),
            MediaManifestJson = Json.Serialize(request.Media ?? []),
            VariablesJson = Json.Serialize(request.Variables ?? new Dictionary<string, string>()),
            Rating = request.Rating,
            TemplateId = request.TemplateId,
            PostAt = request.PostAt,
            RootStatus = PostRootStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (var connector in connectors)
        {
            var platform = connector.ServiceDefinition!.Platform;
            post.Targets.Add(new PostTarget
            {
                Id = Guid.NewGuid(),
                PostId = post.Id,
                ConnectorId = connector.Id,
                Platform = platform,
                OptionsJson = TargetOptionsFor(platform, connector.DisplayName,
                    request.TargetOptions?.GetValueOrDefault(connector.Id)),
                Status = TargetStatus.Queued,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // From here on, every log in this request carries the PostId (see PostIdLogEnricher), so a
        // user can hand a dev the post id from the UI and the dev finds the intake telemetry too.
        PostTelemetry.SetBusinessBaggage(post.Id);

        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);

        // Persist the human-authored payload alongside the record (mirrors media storage).
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/title", post.Title, ct: ct);
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/description", post.Description, ct: ct);
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/description-html", post.HtmlDescription, ct: ct);

        var delay = request.PostAt is { } at && at > now ? at - now : (TimeSpan?)null;
        foreach (var target in post.Targets)
            await bus.PublishAsync(new GenerateTargetCommand { PostId = post.Id, TargetId = target.Id }, delay, ct);

        return new CreatePostResponse(post.Id, post.RootStatus);
    }

    /// <summary>
    /// Narrows the author's per-submission choices for one target to exactly the fields its platform
    /// declares, and enforces them. Undeclared keys are dropped rather than rejected: a client sending
    /// options for a platform that has since stopped taking them should still get its post away, and
    /// nothing unvetted should reach the connector.
    /// <para>
    /// Field names are matched case-insensitively and re-emitted under the schema's own casing. JSON
    /// dictionary keys are not touched by naming policies, so a client serialising with camelCase web
    /// defaults would otherwise send <c>category</c> against a declared <c>Category</c> and have its
    /// choice silently dropped — the connector would then apply the platform default instead.
    /// </para>
    /// </summary>
    private string TargetOptionsFor(
        string platform,
        string displayName,
        IReadOnlyDictionary<string, string>? supplied)
    {
        if (supplied is null || supplied.Count == 0) return "{}";
        if (!registry.TryGet(platform, out var connector)
            || connector.Describe().PostOptionsSchema is not { } schema)
            return "{}";

        var declared = DeclaredFields(schema);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in supplied)
            if (declared.TryGetValue(key, out var canonical)) options[canonical] = value;
        if (options.Count == 0) return "{}";

        var json = Json.Serialize(options);
        if (ConfigSchemaValidator.Validate(schema, json) is { } error)
            throw new ConnectorValidationException($"{displayName}: {error}");
        return json;
    }

    /// <summary>
    /// The fields a descriptor schema declares, as a case-insensitive lookup onto their declared
    /// spelling. <c>$</c>-prefixed keys are schema metadata, not fields.
    /// </summary>
    private static Dictionary<string, string> DeclaredFields(string schema)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(schema);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return names;
            foreach (var field in doc.RootElement.EnumerateObject())
                if (!field.Name.StartsWith('$')) names[field.Name] = field.Name;
        }
        catch (JsonException) { /* an operator-authored schema: a bad one imposes nothing. */ }
        return names;
    }
}
