# Architecture Research

**Domain:** Granular serialization control for DynamicWeb.Serializer v0.5.0
**Researched:** 2026-04-07
**Confidence:** HIGH (based on full codebase review of existing architecture)

## System Overview: Current State

```
+-----------------------------------------------------------------+
|                          Admin UI Layer                          |
|  +------------------+  +------------------+  +----------------+ |
|  |PredicateEditScreen|  |SettingsEditScreen|  |LogViewerScreen | |
|  +--------+---------+  +--------+---------+  +----------------+ |
+-----------+------------------------+----------------------------+
|                      Orchestration Layer                         |
|  +----------------------+  +----------------+  +--------------+ |
|  |SerializerOrchestrator|  |ProviderRegistry|  |FkDepResolver | |
|  +----------+-----------+  +-------+--------+  +--------------+ |
+-------------+------------------------+--------------------------+
|                        Provider Layer                            |
|  +------------------+            +------------------+            |
|  | ContentProvider   |            | SqlTableProvider  |            |
|  +--------+---------+            +--------+---------+            |
+-----------+-----------------------------------+------------------+
|                      Pipeline Layer                              |
|  +--------------+  +----------------+  +--------------------+    |
|  |ContentMapper  |  |ContentSerializr|  |ContentDeserializr  |    |
|  |(DB -> DTO)    |  |(DTO -> YAML)   |  |(YAML -> DB)        |    |
|  +--------------+  +----------------+  +--------------------+    |
|  +--------------+  +----------------+  +----------------+        |
|  |SqlTableReader |  | FlatFileStore  |  |SqlTableWriter  |        |
|  +--------------+  +----------------+  +----------------+        |
+-----------------------------------------------------------------+
|                     Infrastructure Layer                         |
|  +------------------+  +----------------------------+            |
|  | FileSystemStore   |  |ForceStringScalarEmitter    |            |
|  | (content YAML)    |  |(YAML string quoting)       |            |
|  +------------------+  +----------------------------+            |
|  +------------------+  +----------------------------+            |
|  | ConfigLoader      |  |YamlConfiguration           |            |
|  +------------------+  +----------------------------+            |
+-----------------------------------------------------------------+
```

## Integration Analysis: Where Each Feature Hooks In

### Feature 1: XML Pretty-Printing

**Problem:** Fields like `ModuleSettings` and `UrlDataProviderParameters` (on paragraphs/pages) contain raw XML strings. SQL table rows also contain XML columns. These are currently serialized as opaque strings, making git diffs unreadable.

**Where it hooks in:** A shared utility that transforms XML strings, called from two sites:

1. **Content pipeline** -- `ContentMapper.MapParagraph()` writes `paragraph.ModuleSettings` as-is into `SerializedParagraph.ModuleSettings`. The pretty-printer intercepts here, reformatting the XML before it reaches the DTO. Similarly, `ContentMapper.MapPage()` writes `page.UrlDataProviderParameters` into the `SerializedUrlSettings` DTO.

2. **SqlTable pipeline** -- `FlatFileStore.WriteRow()` serializes `Dictionary<string, object?>` to YAML. XML columns are just strings. The pretty-printer intercepts in `SqlTableReader.ReadAllRows()` or in `FlatFileStore.WriteRow()` before YAML serialization.

**New component: `XmlFormatter` (shared utility)**

```
Infrastructure/XmlFormatter.cs
```

```csharp
public static class XmlFormatter
{
    /// <summary>
    /// If the value looks like XML, pretty-print it with indentation.
    /// Returns the original string if it's not valid XML.
    /// </summary>
    public static string PrettyPrint(string value);

    /// <summary>
    /// Compact XML back to single-line for database write-back.
    /// </summary>
    public static string Compact(string value);

    /// <summary>
    /// Remove specific XML elements by name (for XML field blacklist).
    /// </summary>
    public static string RemoveElements(string xml, IEnumerable<string> elementNames);

    /// <summary>
    /// Returns true if the string appears to be XML (starts with < after trimming).
    /// </summary>
    public static bool LooksLikeXml(string value);
}
```

**Modification points:**

| Component | Change | Why |
|-----------|--------|-----|
| `ContentMapper.MapParagraph()` | Call `XmlFormatter.PrettyPrint(paragraph.ModuleSettings)` before assigning to DTO | Pretty-print content XML on serialize |
| `ContentMapper.MapPage()` | Call `XmlFormatter.PrettyPrint(page.UrlDataProviderParameters)` | Pretty-print URL provider XML |
| `ContentDeserializer` | Call `XmlFormatter.Compact()` when writing ModuleSettings/UrlDataProviderParameters back to DB | Restore original compact format |
| `FlatFileStore.WriteRow()` | Detect XML columns and pretty-print before YAML serialization | Pretty-print SQL table XML |
| `SqlTableProvider.Deserialize()` (CoerceRowTypes area) | Compact XML strings back before SQL write | Restore compact format |

**Key design decision:** Pretty-printing happens at the **mapping boundary** (DB value -> DTO or YAML dict), not in the YAML serializer itself. This keeps `ForceStringScalarEmitter` and `YamlConfiguration` unchanged. The YAML serializer sees a multiline string and uses Literal block style (already handled by `ForceStringScalarEmitter` for `\n`-containing strings).

**For SQL tables:** `FlatFileStore` doesn't currently know which columns contain XML. Two options:
- **Option A (recommended):** Auto-detect -- any string value starting with `<` and ending with `>` gets pretty-printed. Simple, no config needed, handles all tables uniformly.
- **Option B:** Explicit column list in predicate config. More precise but adds config burden for every table.

Use Option A because it's zero-config and the false positive risk is negligible (few non-XML values start with `<` and are valid XML).

### Feature 2: Field-Level Filtering (Blacklists)

**Problem:** Environment-specific columns (e.g., `AreaDomain`, `AreaSSLCertificate` for areas; `PageCreatedBy` for pages; specific SQL columns) should be excludable per predicate.

**Where it hooks in:** The `ProviderPredicateDefinition` gains new fields, and filtering is applied at the mapping layer.

**Config model change: `ProviderPredicateDefinition`**

```csharp
// New fields on ProviderPredicateDefinition:

/// <summary>
/// Field names to exclude from serialization. Applied to page/paragraph/area
/// properties for Content predicates, or column names for SqlTable predicates.
/// </summary>
public List<string> ExcludeFields { get; init; } = new();

/// <summary>
/// XML element names to exclude from embedded XML fields (ModuleSettings, etc.).
/// Applied after XML pretty-printing, before YAML serialization.
/// </summary>
public List<string> ExcludeXmlElements { get; init; } = new();
```

**Data flow for Content predicates:**

```
DB (DW API)
    |
    v
ContentMapper.MapPage(page, ..., excludeFields)     -- NEW: receives exclude list
    |-- Skips excluded properties when building DTO
    |-- Skips excluded keys in Fields/PropertyFields/ItemFields dictionaries
    v
SerializedPage DTO (filtered)
    |
    v
FileSystemStore.WriteTree() (unchanged)
    |
    v
YAML on disk (excluded fields absent)
```

**Modification points for Content:**

| Component | Change | Why |
|-----------|--------|-----|
| `ProviderPredicateDefinition` | Add `ExcludeFields`, `ExcludeXmlElements` properties | Config model extension |
| `ContentMapper` | Accept `IReadOnlySet<string>? excludeFields` parameter on `MapPage`, `MapParagraph`, `MapArea` | Filter fields during mapping |
| `ContentMapper.MapPage()` | Skip DTO properties and `Fields`/`PropertyFields` keys in exclude set | Field-level blacklist |
| `ContentMapper.MapParagraph()` | Skip `Fields` keys in exclude set | Field-level blacklist |
| `ContentMapper.MapArea()` | Skip `ItemFields` keys in exclude set | Field-level blacklist |
| `ContentSerializer` | Pass `predicate.ExcludeFields` to ContentMapper calls | Thread config through |
| `ContentDeserializer` | No change needed -- absent fields are simply not written to DB (already handled by source-wins null-out) | Absent = skip on deserialize |

**Modification points for SqlTable:**

| Component | Change | Why |
|-----------|--------|-----|
| `SqlTableProvider.Serialize()` | Remove excluded columns from row dict before writing | Filter SQL columns |
| `FlatFileStore.WriteRow()` | No change -- receives pre-filtered dict | Clean separation |

**XML element blacklist flow:**

```
Raw XML string (e.g., ModuleSettings)
    |
    v
XmlFormatter.PrettyPrint(xml)                              -- Step 1: format
    |
    v
XmlFormatter.RemoveElements(xml, excludeXmlElements)       -- Step 2: strip
    |
    v
Assign to DTO field
```

This is a two-step transform: pretty-print first, then strip. Both happen in `ContentMapper` (or the `SqlTableProvider.Serialize` loop for SQL).

### Feature 3: Area Consolidation into ContentProvider

**Problem:** Currently `SerializedArea` only captures `AreaId`, `Name`, `SortOrder`, `ItemType`, `ItemFields`. The DW `Area` table has 60+ columns (AreaDomain, AreaCulture, AreaMasterArea, AreaSSLCertificate, etc.). These should be serialized into `area.yml` for full environment portability.

**Where it hooks in:** `ContentMapper.MapArea()` is the sole location.

**Design decision -- Typed properties vs. Dictionary:**

Use a **Dictionary<string, object> `Properties`** field on `SerializedArea` rather than 60+ typed properties. Reasons:
1. DW Area table schema can change between versions -- a dictionary is resilient.
2. Field-level blacklist filtering works naturally with dictionary key removal.
3. Consistent with how `Fields` and `ItemFields` already work on pages/paragraphs.
4. The area.yml already uses YAML's natural key-value format.

```csharp
public record SerializedArea
{
    // ... existing fields (AreaId, Name, SortOrder, ItemType, ItemFields, Pages) ...

    /// <summary>
    /// All Area table columns beyond the named properties above.
    /// Serialized as flat key-value pairs in area.yml.
    /// </summary>
    public Dictionary<string, object> Properties { get; init; } = new();
}
```

**How to read all Area columns:** Read directly from the Area SQL table using `CommandBuilder`/`Database.CreateDataReader`. The DW `Area` class doesn't expose all 60+ columns as properties, but they are all in the `[Area]` table. This is a read-only operation for serialization, so direct SQL is acceptable here.

```csharp
// New helper in ContentMapper or a dedicated AreaPropertyReader:
public static Dictionary<string, object> ReadAreaProperties(int areaId)
{
    var props = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    var cb = new CommandBuilder();
    cb.Add("SELECT * FROM [Area] WHERE [AreaID] = @id");
    cb.AddParameter("id", areaId);
    using var reader = Database.CreateDataReader(cb);
    if (reader.Read())
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            var value = reader.GetValue(i);
            if (value != DBNull.Value)
                props[name] = value;
        }
    }
    // Remove columns already captured by named DTO properties
    props.Remove("AreaID");
    props.Remove("AreaName");
    props.Remove("AreaSort");
    props.Remove("AreaItemType");
    return props;
}
```

**Modification points:**

| Component | Change | Why |
|-----------|--------|-----|
| `SerializedArea` | Add `Dictionary<string, object> Properties` | Carry full Area table data |
| `ContentMapper.MapArea()` | Read Area properties via SQL, populate Properties dict, apply excludeFields | Capture and filter all columns |
| `ContentDeserializer.DeserializePredicate()` | Write Properties back to Area table via SQL UPDATE on deserialize | Full round-trip |
| `FileSystemStore.WriteTree()` | No change -- Properties dict serializes naturally as YAML | Already handles dicts |

**Deserialization approach for Area properties:**

```csharp
// In ContentDeserializer, after existing area ItemType handling:
if (area.Properties.Count > 0 && !_isDryRun)
{
    // Build SQL UPDATE with all Properties keys
    var cb = new CommandBuilder();
    cb.Add("UPDATE [Area] SET ");
    var first = true;
    foreach (var kvp in area.Properties)
    {
        if (!first) cb.Add(", ");
        cb.Add($"[{kvp.Key}] = @{kvp.Key}");
        cb.AddParameter(kvp.Key, kvp.Value);
        first = false;
    }
    cb.Add(" WHERE [AreaID] = @areaId");
    cb.AddParameter("areaId", predicate.AreaId);
    Database.ExecuteNonQuery(cb);
    Services.Areas.ClearCache();
}
```

### Feature 4: Enhanced Predicate UI

**Where it hooks in:** `PredicateEditScreen`, `PredicateEditModel`, `SavePredicateCommand`.

**Modification points:**

| Component | Change | Why |
|-----------|--------|-----|
| `PredicateEditModel` | Add `ExcludeFields` (string, textarea), `ExcludeXmlElements` (string, textarea) | UI model for new fields |
| `PredicateEditScreen.BuildEditScreen()` | Add "Field Filtering" group with ExcludeFields and ExcludeXmlElements editors, shown for both provider types | Show new fields in UI |
| `PredicateEditScreen.GetEditor()` | Add cases for new fields (Textarea editors with explanatory text) | Configure editor appearance |
| `SavePredicateCommand.Handle()` | Parse ExcludeFields/ExcludeXmlElements from textarea (newline-separated) into List<string>, for both Content and SqlTable branches | Persist to config |
| `PredicateByIndexQuery` | Map new fields from config to edit model | Load for editing |

The UI groups should be:
1. **Configuration** (Name, ProviderType) -- existing
2. **Content Settings** or **SQL Table Settings** -- existing, provider-specific
3. **Field Filtering** (ExcludeFields, ExcludeXmlElements) -- NEW, shown for both provider types when ProviderType is selected

## Component Map: New vs. Modified

### New Components

| Component | File | Purpose |
|-----------|------|---------|
| `XmlFormatter` | `Infrastructure/XmlFormatter.cs` | Pretty-print, compact, and filter XML strings. Uses `System.Xml.Linq.XDocument` |

### Modified Components

| Component | File | Nature of Change |
|-----------|------|------------------|
| `ProviderPredicateDefinition` | `Models/ProviderPredicateDefinition.cs` | Add ExcludeFields, ExcludeXmlElements list properties |
| `SerializedArea` | `Models/SerializedArea.cs` | Add Properties dictionary for full Area table columns |
| `ContentMapper` | `Serialization/ContentMapper.cs` | XML pretty-print on map, field filtering, area property capture via SQL |
| `ContentSerializer` | `Serialization/ContentSerializer.cs` | Pass predicate exclude config to mapper calls |
| `ContentDeserializer` | `Serialization/ContentDeserializer.cs` | XML compact on deserialize, area property write-back via SQL UPDATE |
| `FlatFileStore` | `Providers/SqlTable/FlatFileStore.cs` | XML auto-detect and pretty-print on WriteRow |
| `SqlTableProvider` | `Providers/SqlTable/SqlTableProvider.cs` | Field filtering in serialize loop, XML compact in deserialize CoerceRowTypes |
| `PredicateEditModel` | `AdminUI/Models/PredicateEditModel.cs` | Add ExcludeFields, ExcludeXmlElements string fields |
| `PredicateEditScreen` | `AdminUI/Screens/PredicateEditScreen.cs` | Add Field Filtering UI group for both provider types |
| `SavePredicateCommand` | `AdminUI/Commands/SavePredicateCommand.cs` | Parse and persist new fields in both Content and SqlTable branches |
| `PredicateByIndexQuery` | `AdminUI/Queries/PredicateByIndexQuery.cs` | Load new fields for editing |

### Unchanged Components

| Component | Why Unchanged |
|-----------|---------------|
| `ISerializationProvider` | Interface unchanged -- filtering is config-level, not contract-level |
| `SerializerOrchestrator` | Passes predicates through unchanged -- filtering happens inside providers |
| `ProviderRegistry` | No new providers added |
| `ForceStringScalarEmitter` | Already handles multiline strings correctly (Literal for LF-only) |
| `YamlConfiguration` | No serializer config changes needed |
| `FileSystemStore` | Writes whatever DTOs contain -- filtering happens upstream |
| `InternalLinkResolver` | Unaffected by field filtering or XML formatting |
| `ConfigLoader/ConfigWriter` | JSON serialization handles new List<string> properties automatically |
| `ContentPredicate/ContentPredicateSet` | Path-based filtering unchanged; field filtering is orthogonal |

## Suggested Build Order

The features have dependencies that dictate order:

### Phase 1: XmlFormatter Utility (foundation, no dependencies)

Build `Infrastructure/XmlFormatter.cs` with:
- `PrettyPrint(string)` -- uses `System.Xml.Linq.XDocument.Parse()` then `ToString(SaveOptions.None)` for indented output
- `Compact(string)` -- `XDocument.Parse()` then `ToString(SaveOptions.DisableFormatting)`
- `LooksLikeXml(string)` -- quick heuristic: trim, starts with `<`, try `XDocument.Parse`
- `RemoveElements(string, IEnumerable<string>)` -- `XDocument`-based element removal via `Descendants().Where().Remove()`

**Why first:** Both Content and SqlTable pipelines depend on this. No external dependencies beyond `System.Xml.Linq` (already in .NET 8). Can be unit tested in isolation.

### Phase 2: XML Pretty-Printing in Content Pipeline

Modify `ContentMapper.MapParagraph()` and `ContentMapper.MapPage()` to pretty-print XML fields. Modify `ContentDeserializer` to compact on write-back.

**Why second:** Validates XmlFormatter works end-to-end with real DW content data before adding filtering complexity. Test with existing Swift test environment.

### Phase 3: XML Pretty-Printing in SqlTable Pipeline

Modify `FlatFileStore.WriteRow()` to auto-detect and pretty-print XML values. Modify `SqlTableProvider` deserialize path to compact.

**Why third:** Extends proven XmlFormatter to second pipeline. Independent from Content changes but benefits from Phase 2 validation.

### Phase 4: Predicate Config Extension + Field-Level Filtering (Content)

1. Extend `ProviderPredicateDefinition` with `ExcludeFields` and `ExcludeXmlElements`
2. Modify `ContentMapper` methods to accept and apply exclude sets
3. Modify `ContentSerializer` to thread predicate config to mapper
4. Add XML element filtering (calls `XmlFormatter.RemoveElements`)

**Why fourth:** Depends on Phase 1 (XmlFormatter.RemoveElements). Config model change is prerequisite for UI (Phase 7).

### Phase 5: Field-Level Filtering (SqlTable)

Apply `ExcludeFields` in `SqlTableProvider.Serialize()` to remove columns before write. Apply `ExcludeXmlElements` to XML columns.

**Why fifth:** Same pattern as Phase 4, applied to second pipeline. Reuses config model from Phase 4.

### Phase 6: Area Consolidation

1. Extend `SerializedArea` with `Properties` dictionary
2. Modify `ContentMapper.MapArea()` to read full Area properties via SQL
3. Modify `ContentDeserializer` to write Properties back via SQL UPDATE
4. Apply field filtering from Phase 4 to Area Properties

**Why sixth:** Depends on field-level filtering (Phase 4) to be practical -- without filtering, you'd serialize environment-specific area columns like `AreaDomain` that shouldn't be portable. Must come after Phase 4 so users can immediately exclude environment-specific columns.

### Phase 7: Enhanced Predicate UI

1. Extend `PredicateEditModel` with new textarea fields
2. Extend `PredicateEditScreen` with "Field Filtering" group
3. Extend `SavePredicateCommand` to persist new fields
4. Extend `PredicateByIndexQuery` to load new fields

**Why last:** UI is the final consumer. All backend features must work first. Users can test via JSON config editing during Phases 1-6.

### Dependency Graph

```
Phase 1: XmlFormatter
    |
    +---> Phase 2: XML in Content
    |         |
    +---> Phase 3: XML in SqlTable
    |
    +---> Phase 4: Config + Filtering (Content)
              |
              +---> Phase 5: Filtering (SqlTable)
              |
              +---> Phase 6: Area Consolidation
              |
              +---> Phase 7: Predicate UI
```

Phases 2 and 3 can run in parallel. Phases 5, 6, and 7 depend on Phase 4 but are independent of each other.

## Anti-Patterns to Avoid

### Anti-Pattern 1: XML Processing in YAML Emitter

**What people do:** Try to detect and format XML inside `ForceStringScalarEmitter` or a custom YAML type converter.
**Why it's wrong:** YAML serialization should be format-agnostic. Mixing XML awareness into YAML infrastructure creates coupling and makes the emitter untestable in isolation.
**Do this instead:** Format XML at the mapping layer (ContentMapper / FlatFileStore), before YAML sees it.

### Anti-Pattern 2: Filtering in the Deserializer

**What people do:** Apply field blacklists during deserialization (skip excluded fields when writing to DB).
**Why it's wrong:** Source-wins semantics mean absent fields get null-ed out. If a field is absent because it was filtered during serialization, that's correct behavior. If you also filter during deserialization, you lose the ability to null-out stale values.
**Do this instead:** Filter only during serialization. Deserialization should faithfully replay whatever's in the YAML.

### Anti-Pattern 3: Area Properties via SqlTableProvider

**What people do:** Create a separate SqlTable predicate for the Area table to capture all columns.
**Why it's wrong:** Area is identity-linked to Content predicates via AreaId. Two different predicates managing the same Area creates ordering issues and config duplication. The predicate UI already requires an AreaId for Content predicates.
**Do this instead:** Consolidate Area properties into ContentProvider's `area.yml` output.

### Anti-Pattern 4: Separate XML Columns Config

**What people do:** Add a per-predicate list of "XML column names" to tell the system which columns contain XML.
**Why it's wrong:** Adds config burden for every SQL table predicate. Users must know which columns contain XML.
**Do this instead:** Auto-detect XML by attempting `XDocument.Parse()`. False positives are negligible in practice.

## Sources

- Full codebase review of all source files in `src/DynamicWeb.Serializer/` -- HIGH confidence
- `System.Xml.Linq.XDocument` API for XML formatting -- HIGH confidence, standard .NET 8 API
- Existing `ForceStringScalarEmitter` pattern for YAML multiline string handling -- HIGH confidence, verified in codebase
- DW `Area` table schema knowledge from previous v0.4.0 research -- HIGH confidence

---
*Architecture research for: DynamicWeb.Serializer v0.5.0 granular serialization control*
*Researched: 2026-04-07*
