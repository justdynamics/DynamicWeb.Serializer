using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicWeb.Serializer.Configuration;

[JsonConverter(typeof(ConflictStrategyJsonConverter))]
public enum ConflictStrategy
{
    SourceWins
}

public sealed class ConflictStrategyJsonConverter : JsonConverter<ConflictStrategy>
{
    public override ConflictStrategy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "source-wins" => ConflictStrategy.SourceWins,
            _ => ConflictStrategy.SourceWins
        };
    }

    public override void Write(Utf8JsonWriter writer, ConflictStrategy value, JsonSerializerOptions options)
    {
        var str = value switch
        {
            ConflictStrategy.SourceWins => "source-wins",
            _ => "source-wins"
        };
        writer.WriteStringValue(str);
    }
}
