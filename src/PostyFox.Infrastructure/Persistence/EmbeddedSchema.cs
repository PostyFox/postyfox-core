using System.Text.Encodings.Web;
using System.Text.Json;

namespace PostyFox.Infrastructure.Persistence;

/// <summary>
/// Reads the field-descriptor schemas that ship as embedded resources under
/// <c>Persistence/Schemas</c>. A schema lands there instead of a C# string literal when its option
/// lists are too large to stay readable inline (see that folder's README).
/// </summary>
public static class EmbeddedSchema
{
    private static readonly JsonSerializerOptions Compact =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Loads a schema and drops its formatting. The file on disk is pretty-printed so it can be
    /// reviewed and diffed; the copy served to clients has no need for tens of KB of indentation.
    /// Parsing here also fails the process at startup if a schema is ever left malformed.
    /// </summary>
    public static string Load(string fileName)
    {
        var name = $"PostyFox.Infrastructure.Persistence.Schemas.{fileName}";
        using var stream = typeof(EmbeddedSchema).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded schema '{name}' is missing.");
        using var doc = JsonDocument.Parse(stream);
        return JsonSerializer.Serialize(doc.RootElement, Compact);
    }
}
