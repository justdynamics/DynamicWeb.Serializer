using System.Text.Json.Serialization;

namespace DynamicWeb.Serializer.Infrastructure;

/// <summary>
/// Polymorphic base for manifest entries. Discriminator is <c>providerType</c> at position 0
/// (STJ writes properties in declaration order; ProviderType is declared first so writers pin
/// it to position 0 of the entry object — defends pitfall #4 STJ polymorphism fragility).
/// IgnoreUnrecognizedTypeDiscriminators=false ensures unknown discriminator values throw
/// JsonException at read time (MANIFEST-02). UnknownDerivedTypeHandling=FailSerialization
/// ensures unmapped derived types throw at write time.
/// </summary>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "providerType",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
    IgnoreUnrecognizedTypeDiscriminators = false)]
[JsonDerivedType(typeof(ContentEntry), typeDiscriminator: "Content")]
[JsonDerivedType(typeof(SqlTableEntry), typeDiscriminator: "SqlTable")]
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public abstract record ManifestEntry
{
    /// <summary>Stable id within the manifest. Used for per-entry log prefixes and Phase 43 outcome reporting.</summary>
    public required string EntryId { get; init; }

    /// <summary>
    /// Provider-type string ("Content" / "SqlTable"). Mirrors the STJ discriminator value for
    /// non-STJ inspection / logging without downcasting. <see cref="JsonIgnoreAttribute"/>
    /// because the same JSON key (<c>providerType</c>) is already produced by the polymorphism
    /// discriminator — emitting it twice would conflict at read time. Derived classes override
    /// to return their canonical type string; never null.
    /// </summary>
    [JsonIgnore]
    public abstract string ProviderType { get; }

    /// <summary>POSIX-relative paths under the mode root (forward slashes, no leading slash).</summary>
    public required IReadOnlyList<string> Files { get; init; }
}
