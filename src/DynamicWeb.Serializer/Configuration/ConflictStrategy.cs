using System.Text.Json;
using System.Text.Json.Serialization;

namespace DynamicWeb.Serializer.Configuration;

/// <summary>
/// How to handle conflicts between YAML source and DB target during deserialize.
/// SourceWins (default for Deploy): YAML overwrites target.
/// DestinationWins (default for Seed, Phase 37 CONTEXT.md D-06): rows/pages whose natural key
/// or PageUniqueId is already present on target are NOT modified — preserves customer edits.
/// </summary>
[JsonConverter(typeof(ConflictStrategyJsonConverter))]
public enum ConflictStrategy
{
    SourceWins,
    DestinationWins
}

public sealed class ConflictStrategyJsonConverter : JsonConverter<ConflictStrategy>
{
    public override ConflictStrategy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "source-wins" => ConflictStrategy.SourceWins,
            "destination-wins" => ConflictStrategy.DestinationWins,
            _ => ConflictStrategy.SourceWins
        };
    }

    public override void Write(Utf8JsonWriter writer, ConflictStrategy value, JsonSerializerOptions options)
    {
        var str = value switch
        {
            ConflictStrategy.SourceWins => "source-wins",
            ConflictStrategy.DestinationWins => "destination-wins",
            _ => "source-wins"
        };
        writer.WriteStringValue(str);
    }
}
