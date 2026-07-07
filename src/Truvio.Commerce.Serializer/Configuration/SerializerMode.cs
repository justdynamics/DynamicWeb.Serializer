namespace Truvio.Commerce.Serializer.Configuration;

/// <summary>
/// Selects between Replace (source-wins: the source overwrites the destination) and
/// Merge (destination-wins, field-level fill: only empty destination fields are filled,
/// existing destination values are preserved). Each predicate carries its own mode.
/// </summary>
public enum SerializerMode
{
    Replace,
    Merge
}
