using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PostyFox.Application.Triggers;

/// <summary>
/// A generic signed-webhook source. Signature = hex HMAC-SHA256 of the raw body with the shared
/// signing secret, presented in the <c>X-Signature</c> header. Message id in <c>X-Message-Id</c>.
/// Body: <c>{ "account": "..", "variables": { .. }, "challenge"?: ".." }</c>.
/// A body containing "challenge" is treated as a verification handshake.
/// </summary>
public sealed class GenericHmacTriggerSource : ITriggerSource
{
    public const string TypeName = "generic";
    public string SourceType => TypeName;

    public bool VerifySignature(IReadOnlyDictionary<string, string> headers, string rawBody, string? signingSecret)
    {
        if (string.IsNullOrEmpty(signingSecret)) return false;
        if (!headers.TryGetValue("X-Signature", out var presented) || string.IsNullOrEmpty(presented)) return false;

        var expected = Convert.ToHexStringLower(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(rawBody)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(presented.Trim()));
    }

    public TriggerParseResult Parse(IReadOnlyDictionary<string, string> headers, string rawBody)
    {
        headers.TryGetValue("X-Message-Id", out var messageId);
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("challenge", out var ch) && ch.ValueKind == JsonValueKind.String)
                return new TriggerParseResult(true, ch.GetString(), messageId, null, EmptyVars);

            var account = root.TryGetProperty("account", out var a) ? a.GetString() : null;
            var vars = new Dictionary<string, string>();
            if (root.TryGetProperty("variables", out var v) && v.ValueKind == JsonValueKind.Object)
                foreach (var p in v.EnumerateObject())
                    vars[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText();

            return new TriggerParseResult(false, null, messageId, account, vars);
        }
        catch (JsonException)
        {
            return new TriggerParseResult(false, null, messageId, null, EmptyVars);
        }
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyVars = new Dictionary<string, string>();
}
