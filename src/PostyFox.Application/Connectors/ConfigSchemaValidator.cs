using System.Text.Json;
using System.Text.RegularExpressions;

namespace PostyFox.Application.Connectors;

/// <summary>
/// Server-side enforcement of the field descriptors declared in a
/// <see cref="Domain.Entities.ServiceDefinition"/> schema. The very same descriptors drive the
/// frontend's rendering and inline validation; this is the authoritative gate — the client checks
/// are UX only and can be bypassed (scripted callers, stale tabs, …).
///
/// A schema is a JSON object keyed by field name. Each value is either a legacy placeholder string
/// (<c>""</c>, meaning "no rules") or a descriptor object. Only the validation keys are read here:
/// <list type="bullet">
///   <item><c>required</c> (bool) — value must be present and non-blank.</item>
///   <item><c>pattern</c> (string) — .NET regex the value must match.</item>
///   <item><c>message</c> (string) — error shown when <c>pattern</c> fails (else a generic one).</item>
///   <item><c>minLength</c> / <c>maxLength</c> (int).</item>
/// </list>
/// Presentation keys (label/help/placeholder/type/link) are ignored here — the client owns rendering.
/// </summary>
public static class ConfigSchemaValidator
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Returns the first validation error for <paramref name="valuesJson"/> against
    /// <paramref name="schemaJson"/>, or <c>null</c> when every field satisfies its descriptor.
    /// A missing/blank/malformed schema imposes no rules (returns <c>null</c>).
    /// </summary>
    public static string? Validate(string? schemaJson, string? valuesJson)
    {
        if (string.IsNullOrWhiteSpace(schemaJson)) return null;

        JsonElement schema;
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            schema = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null; // a malformed schema is an operator error, not the user's — don't block them.
        }

        if (schema.ValueKind != JsonValueKind.Object) return null;

        var values = ParseValues(valuesJson);
        foreach (var field in schema.EnumerateObject())
        {
            if (field.Value.ValueKind != JsonValueKind.Object) continue; // legacy placeholder: no rules.
            var error = ValidateField(field.Name, field.Value, values.GetValueOrDefault(field.Name));
            if (error is not null) return error;
        }
        return null;
    }

    private static string? ValidateField(string key, JsonElement descriptor, string? value)
    {
        var label = descriptor.TryGetProperty("label", out var l) && l.ValueKind == JsonValueKind.String
            ? l.GetString()!
            : key;
        var trimmed = value?.Trim() ?? string.Empty;

        if (descriptor.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.True
            && trimmed.Length == 0)
            return $"{label} is required.";

        if (trimmed.Length == 0) return null; // length/pattern rules only apply to supplied values.

        if (descriptor.TryGetProperty("minLength", out var min) && min.TryGetInt32(out var minLen)
            && trimmed.Length < minLen)
            return $"{label} must be at least {minLen} characters.";

        if (descriptor.TryGetProperty("maxLength", out var max) && max.TryGetInt32(out var maxLen)
            && trimmed.Length > maxLen)
            return $"{label} must be at most {maxLen} characters.";

        if (descriptor.TryGetProperty("pattern", out var pat) && pat.ValueKind == JsonValueKind.String
            && pat.GetString() is { Length: > 0 } pattern)
        {
            try
            {
                if (!Regex.IsMatch(trimmed, pattern, RegexOptions.None, RegexTimeout))
                    return Message(descriptor, $"{label} is invalid.");
            }
            catch (ArgumentException) { /* invalid pattern in schema: don't block the user. */ }
            catch (RegexMatchTimeoutException) { /* pathological input: treat as a pass, not a block. */ }
        }
        return null;
    }

    private static string Message(JsonElement descriptor, string fallback) =>
        descriptor.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
            && m.GetString() is { Length: > 0 } s
            ? s
            : fallback;

    private static Dictionary<string, string> ParseValues(string? valuesJson)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(valuesJson)) return result;
        try
        {
            using var doc = JsonDocument.Parse(valuesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
            foreach (var p in doc.RootElement.EnumerateObject())
                result[p.Name] = p.Value.ValueKind == JsonValueKind.String
                    ? p.Value.GetString() ?? string.Empty
                    : p.Value.ToString();
        }
        catch (JsonException) { /* malformed values: treat as no values (required rules will fire). */ }
        return result;
    }
}
