using System.Security.Cryptography;
using System.Text;
using PostyFox.Application.Triggers;
using Xunit;

namespace PostyFox.Application.Tests;

public class GenericHmacTriggerSourceTests
{
    private readonly GenericHmacTriggerSource _source = new();

    private static string Sign(string secret, string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

    private static Dictionary<string, string> H(params (string, string)[] kv) =>
        kv.ToDictionary(x => x.Item1, x => x.Item2, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Valid_signature_passes()
    {
        var body = "{\"account\":\"a\"}";
        Assert.True(_source.VerifySignature(H(("X-Signature", Sign("sec", body))), body, "sec"));
    }

    [Fact]
    public void Wrong_signature_or_secret_fails()
    {
        var body = "{}";
        Assert.False(_source.VerifySignature(H(("X-Signature", Sign("sec", body))), body, "other"));
        Assert.False(_source.VerifySignature(H(("X-Signature", "deadbeef")), body, "sec"));
        Assert.False(_source.VerifySignature(H(), body, "sec"));       // no header
        Assert.False(_source.VerifySignature(H(("X-Signature", "x")), body, null)); // no secret
    }

    [Fact]
    public void Parse_extracts_account_variables_and_message_id()
    {
        var r = _source.Parse(H(("X-Message-Id", "m1")), "{\"account\":\"acme\",\"variables\":{\"game\":\"Go\"}}");
        Assert.False(r.IsChallenge);
        Assert.Equal("acme", r.ExternalAccount);
        Assert.Equal("m1", r.MessageId);
        Assert.Equal("Go", r.Variables["game"]);
    }

    [Fact]
    public void Parse_detects_challenge()
    {
        var r = _source.Parse(H(), "{\"challenge\":\"abc123\"}");
        Assert.True(r.IsChallenge);
        Assert.Equal("abc123", r.Challenge);
    }
}
