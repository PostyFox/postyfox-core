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
    public Guid? TemplateId { get; set; }
    public DateTimeOffset? PostAt { get; set; }
    public PostRootStatus RootStatus { get; set; } = PostRootStatus.Queued;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<PostTarget> Targets { get; set; } = new();
}
