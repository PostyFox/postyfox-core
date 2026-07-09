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
        var substitutedBody = Substitute(request.MarkdownBody, request.Variables);
        var body = FormatForPlatform(request.Platform, substitutedBody);
        return new RenderedPost(title, body, request.Tags, request.Media);
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
}
