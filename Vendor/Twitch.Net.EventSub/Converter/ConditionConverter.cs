using System.Text.Json;
using System.Text.Json.Serialization;
using Twitch.Net.EventSub.Models;

namespace Twitch.Net.EventSub.Converter;

public class ConditionConverter: JsonConverter<IConditionModel>
{
    public override IConditionModel Read(
        ref Utf8JsonReader reader,
        Type typeToConvert, 
        JsonSerializerOptions options
        )
    {
        throw new NotImplementedException();
    }

    public override void Write(
        Utf8JsonWriter writer,
        IConditionModel value, 
        JsonSerializerOptions options
        )
    {
        switch (value)
        {
            case null:
                JsonSerializer.Serialize(writer, (IConditionModel) null!, options);
                break;
            default:
            {
                var type = value.GetType();
                JsonSerializer.Serialize(writer, value, type, options);
                break;
            }
        }
    }
}