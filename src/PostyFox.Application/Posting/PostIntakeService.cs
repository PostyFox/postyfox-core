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
    /// One resolved delivery destination for a post: either a whole connector (the legacy 1:1
    /// behaviour) or one of its exposed <see cref="ConnectorDestination"/>s. <see cref="SelectionId"/>
    /// is whichever id the client sent in <see cref="CreatePostRequest.Targets"/>, used to look up
    /// per-submission <see cref="CreatePostRequest.TargetOptions"/>.
    /// </summary>
    private sealed record ResolvedDestination(
        Guid SelectionId, Guid ConnectorId, string DisplayName, string Platform, string? TargetId, string? TargetName);

    /// <summary>
    /// Persists a post + one target per selected destination, stores the payload, and enqueues
    /// generation for each target (delayed if scheduled). Returns null if no valid targets.
    /// </summary>
    /// <exception cref="ConnectorValidationException">
    /// A target's per-submission options fail its platform's <c>PostOptionsSchema</c>.
    /// </exception>
    public async Task<CreatePostResponse?> CreateAsync(string userId, CreatePostRequest request, CancellationToken ct = default)
    {
        if (request.IsDraft) return await SaveDraftAsync(userId, request, ct);

        var targetIds = (request.Targets ?? []).Distinct().ToList();
        var resolved = await ResolveDestinationsAsync(userId, targetIds, ct);
        if (resolved.Count == 0) return null;

        var now = clock.UtcNow;
        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RootStatus = PostRootStatus.Queued,
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyContent(post, request);
        // Throws ConnectorValidationException before anything is persisted if a target's options fail,
        // or if a target's platform requires tags and none are being sent.
        post.Targets = BuildTargets(post.Id, resolved, request.TargetOptions, request.TargetIncludeTags, request.Tags, now);

        // From here on, every log in this request carries the PostId (see PostIdLogEnricher), so a
        // user can hand a dev the post id from the UI and the dev finds the intake telemetry too.
        PostTelemetry.SetBusinessBaggage(post.Id);

        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        await PersistPayloadAsync(post, ct);

        var delay = request.PostAt is { } at && at > now ? at - now : (TimeSpan?)null;
        foreach (var target in post.Targets)
            await bus.PublishAsync(new GenerateTargetCommand { PostId = post.Id, TargetId = target.Id }, delay, ct);

        return new CreatePostResponse(post.Id, post.RootStatus);
    }

    /// <summary>
    /// Saves a post as a draft: the authored content is persisted as-is, but the target selection is
    /// kept raw (unresolved/unvalidated) on the post itself rather than as real <see cref="PostTarget"/>
    /// rows, and nothing is enqueued. Unlike <see cref="CreateAsync"/>, an empty or currently-invalid
    /// target selection is fine — the draft just isn't postable yet.
    /// </summary>
    public async Task<CreatePostResponse> SaveDraftAsync(string userId, CreatePostRequest request, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var post = new Post
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RootStatus = PostRootStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyContent(post, request);
        ApplyDraftTargets(post, request);

        db.Posts.Add(post);
        await db.SaveChangesAsync(ct);
        await PersistPayloadAsync(post, ct);

        return new CreatePostResponse(post.Id, post.RootStatus);
    }

    /// <summary>
    /// Overwrites a draft's authored content and target selection in place. Only valid while the post
    /// is still a draft — once published, edit "post again" (duplicate) instead.
    /// </summary>
    public async Task<DraftActionOutcome> UpdateDraftAsync(string userId, Guid postId, CreatePostRequest request, CancellationToken ct = default)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId, ct);
        if (post is null) return DraftActionOutcome.NotFound;
        if (post.RootStatus != PostRootStatus.Draft) return DraftActionOutcome.NotADraft;

        ApplyContent(post, request);
        ApplyDraftTargets(post, request);
        post.UpdatedAt = clock.UtcNow;

        await db.SaveChangesAsync(ct);
        await PersistPayloadAsync(post, ct);
        return DraftActionOutcome.Success;
    }

    /// <summary>
    /// Publishes a draft: resolves its stored target selection against the user's current connectors
    /// (a connector disabled since the draft was saved is simply dropped, same as a fresh create),
    /// builds real <see cref="PostTarget"/> rows, and enqueues generation exactly like <see cref="CreateAsync"/>.
    /// </summary>
    /// <exception cref="ConnectorValidationException">A target's stored options fail its platform's schema.</exception>
    public async Task<PublishDraftResult> PublishDraftAsync(string userId, Guid postId, CancellationToken ct = default)
    {
        var post = await db.Posts.Include(p => p.Targets)
            .FirstOrDefaultAsync(p => p.Id == postId && p.UserId == userId, ct);
        if (post is null) return new PublishDraftResult(DraftActionOutcome.NotFound, null);
        if (post.RootStatus != PostRootStatus.Draft) return new PublishDraftResult(DraftActionOutcome.NotADraft, null);

        var targetIds = Json.Deserialize<List<Guid>>(post.DraftTargetsJson ?? "[]") ?? [];
        var resolved = await ResolveDestinationsAsync(userId, targetIds, ct);
        if (resolved.Count == 0) return new PublishDraftResult(DraftActionOutcome.NoValidTargets, null);

        var targetOptions = Json.Deserialize<Dictionary<Guid, IReadOnlyDictionary<string, string>>>(post.DraftTargetOptionsJson ?? "{}");
        var targetIncludeTags = Json.Deserialize<Dictionary<Guid, bool>>(post.DraftTargetIncludeTagsJson ?? "{}");
        var tags = Json.Deserialize<List<string>>(post.TagsJson) ?? [];

        var now = clock.UtcNow;
        var targets = BuildTargets(post.Id, resolved, targetOptions, targetIncludeTags, tags, now);
        // Explicit Add rather than post.Targets.Add(...): post is already tracked (loaded above), so
        // navigation fixup alone leaves these client-keyed entities Modified instead of Added — EF has
        // no other way to tell a manually-assigned Guid key apart from an existing row's.
        db.PostTargets.AddRange(targets);
        post.DraftTargetsJson = null;
        post.DraftTargetOptionsJson = null;
        post.RootStatus = PostRootStatus.Queued;
        post.UpdatedAt = now;

        PostTelemetry.SetBusinessBaggage(post.Id);
        await db.SaveChangesAsync(ct);

        var delay = post.PostAt is { } at && at > now ? at - now : (TimeSpan?)null;
        foreach (var target in targets)
            await bus.PublishAsync(new GenerateTargetCommand { PostId = post.Id, TargetId = target.Id }, delay, ct);

        return new PublishDraftResult(DraftActionOutcome.Success, new CreatePostResponse(post.Id, post.RootStatus));
    }

    /// <summary>Copies the request's authored fields onto the post. Shared by create, save-draft and update-draft.</summary>
    private static void ApplyContent(Post post, CreatePostRequest request)
    {
        post.Title = request.Title ?? string.Empty;
        post.Description = request.Description ?? string.Empty;
        post.HtmlDescription = request.HtmlDescription ?? string.Empty;
        post.TagsJson = Json.Serialize(request.Tags ?? []);
        post.MediaManifestJson = Json.Serialize(request.Media ?? []);
        post.VariablesJson = Json.Serialize(request.Variables ?? new Dictionary<string, string>());
        post.Rating = request.Rating;
        post.TemplateId = request.TemplateId;
        post.PostAt = request.PostAt;
    }

    /// <summary>Stores the request's raw (unresolved) target selection on a draft post.</summary>
    private static void ApplyDraftTargets(Post post, CreatePostRequest request)
    {
        var targetIds = (request.Targets ?? []).Distinct().ToList();
        post.DraftTargetsJson = Json.Serialize(targetIds);
        post.DraftTargetOptionsJson = Json.Serialize(
            request.TargetOptions ?? new Dictionary<Guid, IReadOnlyDictionary<string, string>>());
        post.DraftTargetIncludeTagsJson = Json.Serialize(request.TargetIncludeTags ?? new Dictionary<Guid, bool>());
    }

    /// <summary>Persists the human-authored payload alongside the record (mirrors media storage).</summary>
    private async Task PersistPayloadAsync(Post post, CancellationToken ct)
    {
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/title", post.Title, ct: ct);
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/description", post.Description, ct: ct);
        await objectStore.PutTextAsync(_options.PostContainer, $"{post.Id}/description-html", post.HtmlDescription, ct: ct);
    }

    /// <summary>
    /// Resolves requested target ids to their destinations. A requested id is either a whole connector
    /// (single-destination platforms, and the legacy behaviour every platform used before
    /// per-destination selection existed) or one of that connector's exposed ConnectorDestinations
    /// (multi-target platforms like Telegram — see ConnectorDescriptor.SupportsMultipleTargets). Both
    /// id spaces are plain Guids from different tables, so a requested id can only ever match one of
    /// them. Ids that don't resolve (unknown, disabled, or belonging to another user) are silently
    /// dropped.
    /// </summary>
    private async Task<List<ResolvedDestination>> ResolveDestinationsAsync(string userId, IReadOnlyList<Guid> targetIds, CancellationToken ct)
    {
        if (targetIds.Count == 0) return [];

        var connectors = await db.UserConnectors
            .Include(c => c.ServiceDefinition)
            .Where(c => c.UserId == userId && c.Enabled && targetIds.Contains(c.Id))
            .ToListAsync(ct);

        var destinations = await db.ConnectorDestinations
            .Include(d => d.Connector!.ServiceDefinition)
            .Where(d => d.Connector!.UserId == userId && d.Connector!.Enabled && targetIds.Contains(d.Id))
            .ToListAsync(ct);

        var resolved = new List<ResolvedDestination>();
        resolved.AddRange(connectors.Select(c =>
            new ResolvedDestination(c.Id, c.Id, c.DisplayName, c.ServiceDefinition!.Platform, null, null)));
        resolved.AddRange(destinations.Select(d =>
            new ResolvedDestination(d.Id, d.ConnectorId, d.Connector!.DisplayName, d.Connector!.ServiceDefinition!.Platform, d.ExternalId, d.Name)));
        return resolved;
    }

    /// <summary>Builds one <see cref="PostTarget"/> per resolved destination, validating its per-submission options.</summary>
    /// <exception cref="ConnectorValidationException">
    /// A target's options fail its platform's schema, or its platform requires tags and none are supplied.
    /// </exception>
    private List<PostTarget> BuildTargets(
        Guid postId,
        List<ResolvedDestination> resolved,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, string>>? targetOptions,
        IReadOnlyDictionary<Guid, bool>? targetIncludeTags,
        IReadOnlyList<string>? tags,
        DateTimeOffset now)
    {
        var hasTags = (tags ?? []).Count > 0;
        var targets = new List<PostTarget>(resolved.Count);
        foreach (var destination in resolved)
        {
            registry.TryGet(destination.Platform, out var connector);
            var descriptor = connector?.Describe();
            var requiresTags = descriptor?.RequiresTags ?? false;
            if (requiresTags && !hasTags)
                throw new ConnectorValidationException($"{destination.DisplayName}: at least one tag is required for this platform.");

            // RequiresTags forces the toggle on regardless of what the client sent; otherwise the
            // author's per-target choice applies (defaulting to on when absent).
            var includeTags = requiresTags
                || !(targetIncludeTags?.TryGetValue(destination.SelectionId, out var chosen) == true && !chosen);

            targets.Add(new PostTarget
            {
                Id = Guid.NewGuid(),
                PostId = postId,
                ConnectorId = destination.ConnectorId,
                Platform = destination.Platform,
                TargetId = destination.TargetId,
                TargetName = destination.TargetName,
                OptionsJson = TargetOptionsFor(destination.Platform, destination.DisplayName,
                    targetOptions?.GetValueOrDefault(destination.SelectionId)),
                IncludeTags = includeTags,
                Status = TargetStatus.Queued,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        return targets;
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
