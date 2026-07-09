using System.Text.Json;

namespace PostyFox.Application;

/// <summary>Shared JSON options used for internal (blob/column) serialization.</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
}
