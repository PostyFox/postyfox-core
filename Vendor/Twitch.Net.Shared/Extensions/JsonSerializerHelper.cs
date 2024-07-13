using System.Text.Json;
using System.Text.Json.Serialization;

namespace Twitch.Net.Shared.Extensions;

public static class JsonSerializerHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeModel(object model) =>
        JsonSerializer.Serialize(model, SerializerOptions);

    public static Dictionary<string, object> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch
        {
            return new Dictionary<string, object>();
        }
    }
}