# Technology Stack — Manifest-Driven Deserialize (v0.6.0)

**Project:** Truvio.Commerce.Serializer
**Researched:** 2026-05-08
**Mode:** Project research — STACK only (subsequent milestone, narrow scope)
**Overall confidence:** HIGH

> **Bottom line:** No new NuGet dependencies. The new manifest schema is built entirely
> on `System.Text.Json` 8.x features that already ship with .NET 8: `[JsonPolymorphic]` +
> `[JsonDerivedType]` for the polymorphic `Entry` hierarchy, `[JsonUnmappedMemberHandling]`
> for fail-fast strict reads, and a hand-rolled `schemaVersion` gate that runs *before*
> typed deserialization. Reuse `ManifestWriter.ManifestJsonOptions` as the canonical
> options bag.

---

## Recommended Stack

### Core (no change)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET | 8.0 | Target framework | Already in csproj; STJ 8.x ships in-box |
| `System.Text.Json` | 8.x (in-box) | Manifest read/write | Already used by `ManifestWriter` / `ConfigLoader` / `ConfigWriter` / `LogFileWriter` |
| YamlDotNet | 13.7.1 | Provider payload (.yml files) | Untouched by this milestone — manifest is JSON-only |

### Net-new dependencies

**None.** All capabilities required by the new manifest are already in `System.Text.Json` 8.x.

---

## Concrete Recommendations

### 1. Polymorphic `Entry` hierarchy → `[JsonPolymorphic]` + `[JsonDerivedType]`

**Decision:** Use the first-party .NET 7+ attribute model with a string discriminator.
Default `$type` slot is fine; rename to `providerType` for self-documenting JSON.

```csharp
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "providerType",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
    IgnoreUnrecognizedTypeDiscriminators = false)]
[JsonDerivedType(typeof(ContentEntry),     typeDiscriminator: "Content")]
[JsonDerivedType(typeof(SqlTableEntry),    typeDiscriminator: "SqlTable")]
[JsonDerivedType(typeof(EmbeddedXmlEntry), typeDiscriminator: "EmbeddedXml")]
public abstract record ManifestEntry
{
    public required string RelativePath { get; init; }   // POSIX, like ManifestWriter.Files today
    public required string Sha256 { get; init; }         // optional but recommended (D-22 stub)
    public List<string> ServiceCaches { get; init; } = new();
    public List<string> ResolveLinksInColumns { get; init; } = new();
    public string? SchemaSync { get; init; }
}

public sealed record ContentEntry : ManifestEntry
{
    public required int AreaId { get; init; }
    public required int PageId { get; init; }            // 0 = whole-area
    public Guid? PageUniqueId { get; init; }
    public List<string> ExcludeAreaColumns { get; init; } = new();
}

public sealed record SqlTableEntry : ManifestEntry
{
    public required string Table { get; init; }
    public string? NameColumn { get; init; }
    public string? CompareColumns { get; init; }
    public List<string> XmlColumns { get; init; } = new();
    public List<string> ExcludeFields { get; init; } = new();
    public List<string> IncludeFields { get; init; } = new();
}

public sealed record EmbeddedXmlEntry : ManifestEntry
{
    public required string Table { get; init; }
    public required string Column { get; init; }
    public required string RowKey { get; init; }
    public List<string> ExcludeXmlElements { get; init; } = new();
}
```

**Why this beats alternatives:**

| Option | Verdict | Reason |
|--------|---------|--------|
| **`[JsonPolymorphic]` + `[JsonDerivedType]`** | **CHOSEN** | First-party, attribute-driven, self-documenting, zero runtime allocation, source-generator compatible (metadata mode), explicit allow-list closes the open-set risk. |
| Hand-rolled `JsonConverter<ManifestEntry>` | Rejected | We already have one example (`ConflictStrategyJsonConverter`) — they work but every new subtype means converter edits + boilerplate. The attribute model gives the same control with less code. Keep the converter pattern in reserve for cases the attribute model can't express (none here). |
| `Dictionary<string, JsonElement>` "loose" entries | Rejected | Pushes type discrimination into per-call code, defeats the entire point of locking knowledge into typed records, makes tests fragile. |
| External lib (e.g. Newtonsoft `TypeNameHandling`) | Rejected | Adds a dep, security-prickly (`TypeNameHandling.Auto` is famously CVE-prone), and we already removed Newtonsoft from the deser path. |

**Important specifics confirmed against .NET 8 docs:**

- Default `UnknownDerivedTypeHandling` is `FailSerialization` — set it explicitly anyway so a future
  reviewer reading the attribute does not have to look up the default.
- `IgnoreUnrecognizedTypeDiscriminators = false` (the default) makes deserialize **throw `JsonException`**
  when the manifest contains a discriminator value not in the allow-list. This is exactly the
  fail-fast we want for hand-edited manifests.
- The discriminator must appear as the **first property of the object** unless
  `JsonSerializerOptions.AllowOutOfOrderMetadataProperties = true`. We control both writer and
  reader, so leave the option false (avoids the documented streaming-OOM risk) and have
  `ManifestWriter` emit `providerType` first. STJ does this automatically when the discriminator
  is metadata-typed.
- The base record can stay `abstract` — STJ supports abstract polymorphic bases.
- Polymorphism is **supported** under metadata-mode source generation but **not** fast-path source
  generation. We don't use source generation today, so this is a non-issue. If we ever turn it on,
  swap `[JsonSerializable]` to metadata mode.

**Sources:**
- [How to serialize properties of derived classes — Microsoft Learn (.NET 8)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism) — HIGH confidence
- `System.Text.Json.Serialization.JsonPolymorphicAttribute` API ref — HIGH confidence

---

### 2. `schemaVersion` field — hand-rolled fail-fast, NO library

**Decision:** Add `schemaVersion` (int) as the first field of the manifest envelope.
Read it with a single `JsonDocument.Parse` pass *before* the typed deserialize.
On mismatch, throw `InvalidOperationException` with a clear actionable message.

```csharp
public sealed record Manifest
{
    public required int SchemaVersion { get; init; }    // start at 2 — v1 was the flat file list
    public required string Mode { get; init; }
    public required DateTime WrittenAtUtc { get; init; }
    public required List<ManifestEntry> Entries { get; init; }
}

public Manifest Read(string modeRoot, string mode)
{
    var path = Path.Combine(modeRoot, $"{mode}-manifest.json");
    if (!File.Exists(path))
        throw new FileNotFoundException($"Manifest not found: {path}", path);

    var json = File.ReadAllText(path);

    // Fail-fast version check BEFORE typed deserialize so the error message is about
    // schema mismatch, not random missing-property/discriminator-unknown noise.
    using (var doc = JsonDocument.Parse(json))
    {
        if (!doc.RootElement.TryGetProperty("schemaVersion", out var v) || v.ValueKind != JsonValueKind.Number)
            throw new InvalidOperationException(
                $"Manifest '{path}' is missing a numeric 'schemaVersion' field. " +
                "v0.6.0 manifests require schemaVersion=2.");

        var version = v.GetInt32();
        if (version != ManifestSchema.CurrentVersion)
            throw new InvalidOperationException(
                $"Manifest '{path}' has schemaVersion={version}, expected {ManifestSchema.CurrentVersion}. " +
                "Re-run serialize against the current Serializer build to regenerate the manifest.");
    }

    return JsonSerializer.Deserialize<Manifest>(json, ManifestJsonOptions)
        ?? throw new InvalidOperationException($"Manifest '{path}' deserialized to null.");
}
```

**Why this beats alternatives:**

| Option | Verdict | Reason |
|--------|---------|--------|
| **Hand-rolled `JsonDocument` precheck** | **CHOSEN** | ~10 lines, zero deps, gives the best error message because it runs *before* typed deserialize sees mismatched shapes. Aligns with no-backcompat policy (per `feedback_no_backcompat.md`). |
| Migration framework (`fluentmigrator`-style) | Rejected | We have no historical thoughtfulness mandate. Fail-fast on mismatch is the explicit policy. |
| `[JsonRequired]` on `SchemaVersion` only | Rejected | Catches *missing* version, but a wrong-value version still produces a confusing "could not bind ContentEntry" downstream error. The precheck gives a targeted message. Use `required` (the C# language keyword, lower-case) on the property as a *secondary* guard — STJ honors it as of .NET 7+. |
| External version-aware lib (e.g. JSON Schema with `if/then`) | Rejected | NJsonSchema / JsonSchema.Net could express version conditional schemas, but the cost (extra dep, schema-file maintenance, slower CI) is far higher than 10 lines of code. |

**Source:** Native `JsonDocument` is part of `System.Text.Json` and has been stable since .NET Core 3.0 — HIGH confidence.

---

### 3. Strict / torn-manifest detection — `JsonUnmappedMemberHandling.Disallow`, NO schema validator

**Decision:** Add `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]` to
the new `Manifest`, `ManifestEntry`, and every concrete `*Entry` record. This makes
hand-edits with typos (`provdierType`, `tableName` instead of `table`) throw a
targeted `JsonException` at read time naming the offending property — exactly the
"caught at read time" behavior the question asks about.

```csharp
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SqlTableEntry : ManifestEntry { ... }
```

Set the same default globally as a belt-and-braces guard in the options bag:

```csharp
private static readonly JsonSerializerOptions ManifestJsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,   // NEW for v0.6.0
    Converters = { new JsonStringEnumConverter() }                  // for any future enum field
};
```

Combined with the C# `required` modifier on every load-bearing property (already used
in `ManifestWriter.Manifest`), STJ throws on:

- **Unknown property** → `JsonException: The JSON property 'foo' could not be mapped to any .NET member contained in type 'X'.`
- **Missing required property** → `JsonException: JSON deserialization for type 'X' was missing required properties, including the following: 'Y'.`
- **Unknown discriminator** → `JsonException: The JSON property name '...' for type '...' is not a known type discriminator.`

That's the entire failure surface a JSON Schema validator would catch — caught natively, by
the same parse pass, with no extra dep.

**Why this beats schema-validation libraries:**

| Library | Verdict | Reason |
|---------|---------|--------|
| **Native STJ strict members + `required`** | **CHOSEN** | Same coverage as a JSON Schema validator for our case, single parse pass, no extra dep, errors point at the offending property name + path. |
| `NJsonSchema` (Rico Suter) | Rejected | Adds a transitive dep (12 MB+ tree incl. NewtonsoftJson historically; modern versions split, but still adds weight). Our manifest is producer-controlled — schema drift is the only failure mode, and that's caught by the version gate. **No value over native.** |
| `JsonSchema.Net` (json-everything) | Rejected | Higher quality validator than NJsonSchema (faster, draft-2020-12 compliant) but same conclusion — we don't need a dialect-aware validator for a producer-controlled file. **No value over native.** |
| `Corvus.JsonSchema` | Rejected | Build-time codegen against schemas — nice for OpenAPI consumers, overkill for an internal artifact. |

The only scenario where a schema validator would add value is if we wanted to *publish* the
manifest schema for third-party tools to validate against. That's not on the v0.6.0 roadmap
and would be additive (the records can be reflected to JSON Schema later via NJsonSchema's
generator if needed, without changing runtime behavior).

**Sources:**
- [Handle unmapped members during deserialization — Microsoft Learn (.NET 8)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members) — HIGH confidence
- `System.Text.Json.Serialization.JsonUnmappedMemberHandlingAttribute` API — HIGH confidence

---

### 4. Existing pieces to reuse

Verified via grep across `src/Truvio.Commerce.Serializer`:

| Existing piece | Where | How v0.6.0 reuses it |
|----------------|-------|----------------------|
| `ManifestWriter.ManifestJsonOptions` | `Infrastructure/ManifestWriter.cs:20-25` | Promote to a `static readonly` exposed via internal accessor (or factor into `ManifestSchema` static class). Add `UnmappedMemberHandling.Disallow`. Every other manifest read/write keeps using it. |
| `ManifestWriter.Manifest` record + `Read`/`Write` | `Infrastructure/ManifestWriter.cs` | Replace the `List<string> Files` field with `List<ManifestEntry> Entries`, add `SchemaVersion`. Same per-mode file location (`{mode}-manifest.json`), same POSIX path normalization, same one-call `Write` API. |
| `ManifestCleaner` | `Infrastructure/ManifestCleaner.cs` | Reuse unchanged — it operates on the flat file list extracted from `Entries`. Just project `entries.Select(e => e.RelativePath)` before passing in. T-37-01-01 symlink confinement still applies. |
| `JsonStringEnumConverter` pattern | `Models/ProviderPredicateDefinition.cs:25` (Mode field) | Apply the same way for any enum on `ManifestEntry` (e.g. `DeploymentMode` if we keep it on the entry vs implying it from the folder). |
| `ConflictStrategyJsonConverter` | `Configuration/ConflictStrategy.cs` | Pattern reference for any future hand-rolled converter. NOT needed for v0.6.0 itself. |
| `SerializeResult.WrittenFiles` | `Providers/SerializeResult.cs:18` | Source for the manifest entries — but provider needs an upgrade: a new `IReadOnlyList<ManifestEntry> WrittenEntries` field (built by each provider's new `BuildManifestEntry` step) feeds the manifest, while the existing `WrittenFiles` keeps powering the cleaner's stale-file sweep. |
| `RawSerializerConfiguration` / `RawPredicateDefinition` shadow-DTO pattern | `Configuration/ConfigLoader.cs:348-399` | **Pattern reference.** ConfigLoader uses nullable shadow types so it can produce custom errors instead of generic STJ ones. The new manifest reader does NOT need this — fail-fast on the version gate handles the "wrong shape" case, and `[JsonUnmappedMemberHandling(Disallow)]` plus `required` properties give clean errors for the rest. Resist the urge to add a shadow layer; the manifest is producer-controlled. |
| `RuntimeBundle` | (none — not in repo, only referenced in user prompt) | **No-op.** Confirmed via grep: no `RuntimeBundle` type exists anywhere in `src/`. The prompt's mention of it is hypothetical. If a future runtime-context object is needed (mode, conflictStrategy, strictMode, dryRun, providerFilter — the "caller-supplied" set from PROJECT.md line 86), introduce it as a fresh type — `DeserializeRuntimeContext` is the obvious name. |

**Key reuse rule:** `ManifestJsonOptions` is the single canonical options bag. Do **not**
introduce a parallel options bag for the new schema. Extend the existing one with
`UnmappedMemberHandling.Disallow` and the polymorphism kicks in via attributes alone
(no options-level configuration required for the attribute model).

---

## Updated `ManifestWriter` — concrete delta

```csharp
public class ManifestWriter
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow  // NEW
    };

    public record Manifest
    {
        public required int SchemaVersion { get; init; }              // NEW
        public required string Mode { get; init; }
        public required DateTime WrittenAtUtc { get; init; }
        public required List<ManifestEntry> Entries { get; init; }    // CHANGED from List<string> Files
    }

    public void Write(string modeRoot, string mode, IEnumerable<ManifestEntry> entries) { ... }
    public Manifest Read(string modeRoot, string mode) { ... }   // with version gate, see §2
}
```

Migration is a hard cut: any existing `{mode}-manifest.json` from v0.5.x has no
`schemaVersion` and will fail the version gate with a clear "re-run serialize" message.
This matches the project's stated no-backcompat policy.

---

## Alternatives Considered (Summary Matrix)

| Concern | Recommended | Alternative | Why Not |
|---------|-------------|-------------|---------|
| Polymorphic Entry | `[JsonPolymorphic]` + `[JsonDerivedType]` | Custom `JsonConverter<ManifestEntry>` | More code, no benefit |
| Polymorphic Entry | `[JsonPolymorphic]` + `[JsonDerivedType]` | `Dictionary<string, JsonElement>` per entry | Defeats type-safety goal |
| Polymorphic Entry | `[JsonPolymorphic]` + `[JsonDerivedType]` | Newtonsoft `TypeNameHandling` | Extra dep, security concerns, we already use STJ |
| Schema version | `JsonDocument` precheck + `required` field | Migration framework | Violates no-backcompat policy |
| Schema version | `JsonDocument` precheck + `required` field | `[JsonRequired]` only | Catches missing, not wrong-valued |
| Strict shape | `JsonUnmappedMemberHandling.Disallow` + `required` | NJsonSchema | Extra dep, no extra coverage |
| Strict shape | `JsonUnmappedMemberHandling.Disallow` + `required` | JsonSchema.Net | Extra dep, no extra coverage |
| Strict shape | `JsonUnmappedMemberHandling.Disallow` + `required` | Hand-rolled validator | Reinvents STJ's built-in |

---

## Installation

**Nothing to install.** All capabilities ship in-box with .NET 8.0:

```xml
<!-- Truvio.Commerce.Serializer.csproj — NO new PackageReference entries -->
<TargetFramework>net8.0</TargetFramework>
```

The only csproj change v0.6.0 *might* warrant is a version bump (`0.3.0` → `0.6.0`)
on `<Version>` and `<AssemblyVersion>`, which is unrelated to this stack research.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| `[JsonPolymorphic]` API (.NET 8) | HIGH | Microsoft Learn doc fetched 2026-05-08, content date 2025-12-04 |
| `JsonUnmappedMemberHandling` (.NET 8) | HIGH | Microsoft Learn doc fetched 2026-05-08, content date 2025-01-15 |
| `required` keyword + STJ behavior | HIGH | Stable since .NET 7, behavior preserved in .NET 8 |
| Existing codebase reuse points | HIGH | Verified via direct file reads + grep — see §4 |
| No-library recommendation | HIGH | Producer-controlled artifact + native strict-mode coverage = nothing left for a schema validator to catch |

---

## Sources

- [System.Text.Json polymorphism — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism) — HIGH
- [Handle unmapped members during deserialization — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/missing-members) — HIGH
- [`JsonPolymorphicAttribute` API ref](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonpolymorphicattribute) — HIGH
- [`JsonUnknownDerivedTypeHandling` enum](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonunknownderivedtypehandling) — HIGH
- Local files (verified): `src/Truvio.Commerce.Serializer/Infrastructure/ManifestWriter.cs`,
  `src/Truvio.Commerce.Serializer/Infrastructure/ManifestCleaner.cs`,
  `src/Truvio.Commerce.Serializer/Models/ProviderPredicateDefinition.cs`,
  `src/Truvio.Commerce.Serializer/Configuration/ConfigLoader.cs`,
  `src/Truvio.Commerce.Serializer/Configuration/ConfigWriter.cs`,
  `src/Truvio.Commerce.Serializer/Configuration/ConflictStrategy.cs`,
  `src/Truvio.Commerce.Serializer/Providers/SerializerOrchestrator.cs`,
  `src/Truvio.Commerce.Serializer/Providers/SerializationProviderBase.cs`,
  `src/Truvio.Commerce.Serializer/Providers/ISerializationProvider.cs`,
  `src/Truvio.Commerce.Serializer/Providers/SerializeResult.cs` — HIGH
