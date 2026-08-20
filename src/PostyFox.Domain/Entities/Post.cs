using PostyFox.Domain.Enums;

namespace PostyFox.Domain.Entities;

/// <summary>Root post aggregate. Fans out into one <see cref="PostTarget"/> per platform.</summary>
public class Post
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string HtmlDescription { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public string MediaManifestJson { get; set; } = "[]";
    public string VariablesJson { get; set; } = "{}";
    public ContentRating? Rating { get; set; }
    public Guid? TemplateId { get; set; }
    public DateTimeOffset? PostAt { get; set; }
    public PostRootStatus RootStatus { get; set; } = PostRootStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Raw target selection (connector/destination ids, same shape as <c>CreatePostRequest.Targets</c>)
    /// for a post still in <see cref="Enums.PostRootStatus.Draft"/>. Drafts have no <see cref="Targets"/>
    /// rows yet — those are only created once the draft is published — so this is where the author's
    /// in-progress selection lives in the meantime. Null once published.
    /// </summary>
    public string? DraftTargetsJson { get; set; }

    /// <summary>The draft's per-submission platform choices, keyed by the same raw selection id as <see cref="DraftTargetsJson"/>.</summary>
    public string? DraftTargetOptionsJson { get; set; }

    /// <summary>
    /// The draft's per-target "include tags" choices (see <see cref="PostTarget.IncludeTags"/>), keyed
    /// by the same raw selection id as <see cref="DraftTargetsJson"/>. Null/absent entries default to
    /// true (include tags) once published.
    /// </summary>
    public string? DraftTargetIncludeTagsJson { get; set; }

    public List<PostTarget> Targets { get; set; } = new();
}
