using System.Text;
using System.Text.RegularExpressions;
using PostyFox.Application.Abstractions;
using PostyFox.Application.Connectors;

namespace PostyFox.Application.Templating;

/// <summary>
/// Template engine supporting <c>{variable}</c> substitution, <c>{if var}...{/if}</c>
/// (and optional <c>{else}</c>) conditionals, and per-platform formatting of a
/// markdown body (Telegram HTML, Discord/plain markdown).
/// </summary>
public sealed partial class TemplateEngine : ITemplateEngine
{
    public string Substitute(string body, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;

        // Conditionals first: {if name}...{else}...{/if}
        var conditioned = ConditionalRegex().Replace(body, m =>
        {
            var name = m.Groups["name"].Value.Trim();
            var whenTrue = m.Groups["true"].Value;
            var whenFalse = m.Groups["false"].Success ? m.Groups["false"].Value : string.Empty;
            var present = variables.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v);
            return present ? whenTrue : whenFalse;
        });

        // Then variable substitution: {name}
        var result = VariableRegex().Replace(conditioned, m =>
        {
            var name = m.Groups["name"].Value.Trim();
            return variables.TryGetValue(name, out var v) ? v : string.Empty;
        });

        return result;
    }

    public RenderedPost Render(RenderRequest request)
    {
        var title = string.IsNullOrEmpty(request.Title) ? null : Substitute(request.Title, request.Variables);
        var tags = request.IncludeTags ? request.Tags : [];

        int tagsOmitted;
        IReadOnlyList<string> deliveredTags;
        string bodyWithTags;
        if (request.SupportsTags)
        {
            // Native tags field: the platform takes tags separately, so a stray {tags} token in the
            // body is just removed rather than left as literal text.
            deliveredTags = tags;
            tagsOmitted = 0;
            var vars = WithTagsVariable(request.Variables, string.Empty);
            bodyWithTags = Substitute(request.MarkdownBody, vars);
        }
        else
        {
            // No native tags field: tags can only reach this platform woven into the text, at an
            // author-placed {tags} token or, failing that, appended to the end.
            deliveredTags = [];
            (bodyWithTags, tagsOmitted) = InterpolateTags(request.MarkdownBody, request.Variables, tags, request.MaxContentLength);
        }

        var body = FormatForPlatform(request.Platform, bodyWithTags);
        return new RenderedPost(title, body, deliveredTags, request.Media, request.Rating, tagsOmitted);
    }

    /// <summary>
    /// Weaves formatted <c>#hashtag</c>s into a body for a platform with no native tags field. If the
    /// template contains a <c>{tags}</c> token, the hashtag line replaces it in place; otherwise it is
    /// appended after a blank line (matching the historical Fediverse behaviour). Tags are dropped
    /// from the end, one at a time, until the result fits <paramref name="maxContentLength"/> (no
    /// trimming when null — most platforms only report a cap live, at delivery time).
    /// </summary>
    private (string Body, int TagsOmitted) InterpolateTags(
        string markdownBody,
        IReadOnlyDictionary<string, string> variables,
        IReadOnlyList<string> tags,
        int? maxContentLength)
    {
        var hasPlaceholder = TagsPlaceholderRegex().IsMatch(markdownBody);
        var baseVars = WithTagsVariable(variables, string.Empty);
        var baseBody = Substitute(markdownBody, baseVars);

        var hashtags = tags
            .Select(FormatHashtag)
            .Where(t => t.Length > 0)
            .ToList();
        if (hashtags.Count == 0) return (baseBody, 0);

        // Room available for the hashtag line: the whole budget minus whatever the body already
        // takes (the {tags} token contributes nothing to baseBody, having been substituted away; the
        // fallback append costs an extra blank-line separator).
        var separatorLength = hasPlaceholder ? 0 : "\n\n".Length;
        int? available = maxContentLength is { } max ? Math.Max(0, max - baseBody.Length - separatorLength) : null;

        var included = new List<string>();
        var length = 0;
        foreach (var tag in hashtags)
        {
            var addedLength = tag.Length + (included.Count > 0 ? 1 : 0); // leading space between tags
            if (available is { } room && length + addedLength > room) break;
            included.Add(tag);
            length += addedLength;
        }
        var tagsOmitted = hashtags.Count - included.Count;
        if (included.Count == 0) return (baseBody, tagsOmitted);

        var tagLine = string.Join(' ', included);
        var body = hasPlaceholder
            ? Substitute(markdownBody, WithTagsVariable(variables, tagLine))
            : $"{baseBody}\n\n{tagLine}";
        return (body, tagsOmitted);
    }

    /// <summary>Formats a single author tag as <c>#tag</c>, replacing internal whitespace with `_`.</summary>
    private static string FormatHashtag(string tag)
    {
        var trimmed = WhitespaceRegex().Replace(tag.Trim(), "_");
        if (trimmed.Length == 0) return string.Empty;
        return trimmed.StartsWith('#') ? trimmed : $"#{trimmed}";
    }

    private static Dictionary<string, string> WithTagsVariable(IReadOnlyDictionary<string, string> variables, string tags)
    {
        var vars = new Dictionary<string, string>(variables) { ["tags"] = tags };
        return vars;
    }

    private static string FormatForPlatform(string platform, string markdown) => platform.ToLowerInvariant() switch
    {
        "telegram" => MarkdownToTelegramHtml(markdown),
        _ => markdown // Discord/webhook and default consume markdown natively
    };

    private static string MarkdownToTelegramHtml(string markdown)
    {
        var sb = new StringBuilder(markdown);
        // Links [text](url) -> <a href="url">text</a>
        var linked = LinkRegex().Replace(sb.ToString(), "<a href=\"${url}\">${text}</a>");
        // Bold **text** -> <b>text</b>
        linked = BoldRegex().Replace(linked, "<b>${t}</b>");
        // Italic *text* -> <i>text</i>
        linked = ItalicRegex().Replace(linked, "<i>${t}</i>");
        return linked;
    }

    [GeneratedRegex(@"\{if\s+(?<name>[A-Za-z0-9_]+)\}(?<true>.*?)(?:\{else\}(?<false>.*?))?\{/if\}", RegexOptions.Singleline)]
    private static partial Regex ConditionalRegex();

    [GeneratedRegex(@"\{(?<name>[A-Za-z0-9_]+)\}")]
    private static partial Regex VariableRegex();

    [GeneratedRegex(@"\[(?<text>[^\]]+)\]\((?<url>[^)]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\*\*(?<t>[^*]+)\*\*")]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"(?<!\*)\*(?<t>[^*]+)\*(?!\*)")]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"\{tags\}")]
    private static partial Regex TagsPlaceholderRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
