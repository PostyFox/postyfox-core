using PostyFox.Application.Abstractions;
using PostyFox.Application.Templating;
using Xunit;

namespace PostyFox.Application.Tests;

public class TemplateEngineTests
{
    private readonly TemplateEngine _engine = new();

    [Fact]
    public void Substitute_replaces_variables()
    {
        var result = _engine.Substitute("Hello {name}, playing {game}",
            new Dictionary<string, string> { ["name"] = "Sam", ["game"] = "Chess" });
        Assert.Equal("Hello Sam, playing Chess", result);
    }

    [Fact]
    public void Substitute_missing_variable_becomes_empty()
    {
        Assert.Equal("Hi ", _engine.Substitute("Hi {missing}", new Dictionary<string, string>()));
    }

    [Fact]
    public void Conditional_includes_block_when_present()
    {
        var vars = new Dictionary<string, string> { ["game"] = "Go" };
        Assert.Equal("Playing Go!", _engine.Substitute("Playing {if game}{game}{/if}!", vars));
    }

    [Fact]
    public void Conditional_drops_block_when_absent()
    {
        Assert.Equal("Playing !", _engine.Substitute("Playing {if game}{game}{/if}!", new Dictionary<string, string>()));
    }

    [Fact]
    public void Conditional_else_branch()
    {
        var present = _engine.Substitute("{if live}LIVE{else}offline{/if}", new Dictionary<string, string> { ["live"] = "1" });
        var absent = _engine.Substitute("{if live}LIVE{else}offline{/if}", new Dictionary<string, string>());
        Assert.Equal("LIVE", present);
        Assert.Equal("offline", absent);
    }

    [Fact]
    public void Render_telegram_converts_markdown_to_html()
    {
        var req = new RenderRequest("Telegram", null, "**bold** and *italic* and [link](http://x)",
            new Dictionary<string, string>(), [], []);
        var rendered = _engine.Render(req);
        Assert.Contains("<b>bold</b>", rendered.Body);
        Assert.Contains("<i>italic</i>", rendered.Body);
        Assert.Contains("<a href=\"http://x\">link</a>", rendered.Body);
    }

    [Fact]
    public void Render_discord_keeps_markdown_and_substitutes()
    {
        var req = new RenderRequest("DiscordWH", "Title {n}", "Body **{n}**",
            new Dictionary<string, string> { ["n"] = "42" }, [], []);
        var rendered = _engine.Render(req);
        Assert.Equal("Title 42", rendered.Title);
        Assert.Equal("Body **42**", rendered.Body);
    }

    [Fact]
    public void Render_native_tags_platform_passes_tags_through_unchanged()
    {
        var req = new RenderRequest("Tumblr", null, "Body", new Dictionary<string, string>(),
            ["one", "two"], [], SupportsTags: true);
        var rendered = _engine.Render(req);
        Assert.Equal(["one", "two"], rendered.Tags);
        Assert.Equal("Body", rendered.Body);
        Assert.Equal(0, rendered.TagsOmitted);
    }

    [Fact]
    public void Render_native_tags_platform_excludes_tags_when_not_included()
    {
        var req = new RenderRequest("Tumblr", null, "Body", new Dictionary<string, string>(),
            ["one", "two"], [], SupportsTags: true, IncludeTags: false);
        var rendered = _engine.Render(req);
        Assert.Empty(rendered.Tags);
    }

    [Fact]
    public void Render_native_tags_platform_strips_a_stray_tags_token()
    {
        var req = new RenderRequest("Tumblr", null, "Body {tags} end", new Dictionary<string, string>(),
            ["one"], [], SupportsTags: true);
        var rendered = _engine.Render(req);
        Assert.Equal("Body  end", rendered.Body);
        Assert.Equal(["one"], rendered.Tags);
    }

    [Fact]
    public void Render_non_native_tags_platform_interpolates_placeholder()
    {
        var req = new RenderRequest("BlueSky", null, "Check this out {tags}", new Dictionary<string, string>(),
            ["fox art", "cute"], [], SupportsTags: false);
        var rendered = _engine.Render(req);
        Assert.Equal("Check this out #fox_art #cute", rendered.Body);
        Assert.Empty(rendered.Tags); // carried in the body instead of a native field
        Assert.Equal(0, rendered.TagsOmitted);
    }

    [Fact]
    public void Render_non_native_tags_platform_appends_hashtags_without_a_placeholder()
    {
        var req = new RenderRequest("Mastodon", null, "Check this out", new Dictionary<string, string>(),
            ["fox", "cute"], [], SupportsTags: false);
        var rendered = _engine.Render(req);
        Assert.Equal("Check this out\n\n#fox #cute", rendered.Body);
    }

    [Fact]
    public void Render_non_native_tags_platform_omits_no_tags_when_include_tags_false()
    {
        var req = new RenderRequest("BlueSky", null, "Body {tags}", new Dictionary<string, string>(),
            ["one"], [], SupportsTags: false, IncludeTags: false);
        var rendered = _engine.Render(req);
        Assert.Equal("Body ", rendered.Body);
    }

    [Fact]
    public void Render_trims_excess_tags_to_respect_max_content_length()
    {
        var req = new RenderRequest("BlueSky", null, "Body {tags}", new Dictionary<string, string>(),
            ["aaaaaaaaaa", "bbbbbbbbbb", "cccccccccc"], [], SupportsTags: false, MaxContentLength: 20);
        var rendered = _engine.Render(req);
        Assert.Equal("Body #aaaaaaaaaa", rendered.Body);
        Assert.Equal(2, rendered.TagsOmitted);
    }

    [Fact]
    public void Render_no_max_content_length_keeps_all_tags()
    {
        var req = new RenderRequest("BlueSky", null, "Body {tags}", new Dictionary<string, string>(),
            ["aaaaaaaaaa", "bbbbbbbbbb", "cccccccccc"], [], SupportsTags: false, MaxContentLength: null);
        var rendered = _engine.Render(req);
        Assert.Equal("Body #aaaaaaaaaa #bbbbbbbbbb #cccccccccc", rendered.Body);
        Assert.Equal(0, rendered.TagsOmitted);
    }

    [Fact]
    public void Render_substitutes_text_template_tokens_in_title_and_body()
    {
        var req = new RenderRequest("DiscordWH", "Hi {{tt:mention}}", "See you there, {{tt:mention}}!",
            new Dictionary<string, string>(), [], [],
            TextTemplateValues: new Dictionary<string, string> { ["mention"] = "@alice" });
        var rendered = _engine.Render(req);
        Assert.Equal("Hi @alice", rendered.Title);
        Assert.Equal("See you there, @alice!", rendered.Body);
    }

    [Fact]
    public void Render_text_template_lookup_is_case_insensitive()
    {
        var req = new RenderRequest("DiscordWH", null, "{{tt:Mention}}", new Dictionary<string, string>(), [], [],
            TextTemplateValues: new Dictionary<string, string> { ["mention"] = "@alice" });
        Assert.Equal("@alice", _engine.Render(req).Body);
    }

    [Fact]
    public void Render_unknown_text_template_name_resolves_to_blank_not_a_raw_token()
    {
        var req = new RenderRequest("DiscordWH", null, "Body {{tt:typo}} end", new Dictionary<string, string>(), [], []);
        Assert.Equal("Body  end", _engine.Render(req).Body);
    }

    [Fact]
    public void Render_text_template_values_never_reference_another_text_template()
    {
        // {{tt:...}} substitution runs exactly once, at the start of Render(), so a value containing
        // {{tt:other}} can never expand it — one template can't reference another, so no cycles.
        var req = new RenderRequest("DiscordWH", null, "{{tt:snippet}}", new Dictionary<string, string>(), [], [],
            TextTemplateValues: new Dictionary<string, string> { ["snippet"] = "{{tt:other}}", ["other"] = "REAL" });
        Assert.Equal("{{tt:other}}", _engine.Render(req).Body);
    }

    [Fact]
    public void Render_text_template_values_still_pass_through_ordinary_variable_substitution()
    {
        // A text-template value is spliced into the body like any other author-controlled text — it
        // is not shielded from the post's own {variable} substitution that runs immediately after.
        var req = new RenderRequest("DiscordWH", null, "{{tt:snippet}}",
            new Dictionary<string, string> { ["name"] = "Sam" }, [], [],
            TextTemplateValues: new Dictionary<string, string> { ["snippet"] = "Hi {name}" });
        Assert.Equal("Hi Sam", _engine.Render(req).Body);
    }

    [Fact]
    public void Render_blank_title_after_text_template_substitution_omits_the_title()
    {
        var req = new RenderRequest("DiscordWH", "{{tt:blank}}", "Body", new Dictionary<string, string>(), [], [],
            TextTemplateValues: new Dictionary<string, string> { ["blank"] = "" });
        Assert.Null(_engine.Render(req).Title);
    }

    [Fact]
    public void Render_text_template_substitution_counts_toward_the_tag_trim_budget()
    {
        // The resolved value's length must be accounted for before the hashtag budget is computed —
        // substituting after would let a long value silently blow the platform's character limit.
        var req = new RenderRequest("BlueSky", null, "{{tt:long}} {tags}", new Dictionary<string, string>(),
            ["aaaaaaaaaa", "bbbbbbbbbb"], [], SupportsTags: false, MaxContentLength: 25,
            TextTemplateValues: new Dictionary<string, string> { ["long"] = "0123456789" });
        var rendered = _engine.Render(req);
        Assert.Equal("0123456789 #aaaaaaaaaa", rendered.Body);
        Assert.Equal(1, rendered.TagsOmitted);
    }
}
