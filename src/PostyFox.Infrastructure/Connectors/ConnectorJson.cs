using System.Text.Json;

namespace PostyFox.Infrastructure.Connectors;

/// <summary>Helpers for reading fields out of the connector config/secret JSON strings.</summary>
internal static class ConnectorJson
{
    public static string? Field(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty(name, out var v)
                ? v.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
