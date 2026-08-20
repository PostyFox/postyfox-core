namespace PostyFox.Domain.Entities;

/// <summary>A reusable, named set of post tags owned by a user, for quick reuse in the compose form.</summary>
public class TagPreset
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TagsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
