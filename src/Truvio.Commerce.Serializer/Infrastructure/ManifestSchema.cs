using System.Text.Json;
using System.Text.Json.Serialization;

namespace Truvio.Commerce.Serializer.Infrastructure;

/// <summary>
/// Canonical schema constants + JsonSerializerOptions for v0.6.0 manifest read/write.
/// Phase 42-01: hard cut from v1 (the implicit flat-files manifest). No backcompat;
/// reading a manifest with SchemaVersion != CurrentVersion fails fast at the JsonDocument
/// precheck before typed deserialize sees mismatched shapes.
/// </summary>
public static class ManifestSchema
{
    /// <summary>v0.6.0 manifest schema version. Bump = hard reject of older artifacts.</summary>
    public const int CurrentVersion = 2;

    /// <summary>
    /// Canonical JsonSerializerOptions for every manifest read/write. Single options bag —
    /// do NOT introduce a parallel one (per STACK.md §4 reuse rule). Strict reads via
    /// UnmappedMemberHandling.Disallow + JsonDerivedType allow-list with
    /// IgnoreUnrecognizedTypeDiscriminators=false.
    /// </summary>
    public static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };
}
