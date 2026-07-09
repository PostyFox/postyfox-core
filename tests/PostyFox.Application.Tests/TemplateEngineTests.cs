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
}
