# Stack Research: Granular Serialization Control

**Domain:** DynamicWeb content/SQL serialization -- embedded XML formatting, field-level filtering, area consolidation
**Researched:** 2026-04-07
**Confidence:** HIGH (verified against existing codebase, .NET 8 BCL, YamlDotNet 13.7.1, DW 10.23.9 API docs)

## Recommended Stack

### Zero New NuGet Dependencies

All three v0.5.0 features are achievable with APIs already available in the project:

| Technology | Version | Purpose | Status |
|------------|---------|---------|--------|
| System.Xml.Linq (XDocument) | .NET 8.0 BCL | Pretty-print embedded XML strings | **Already available** -- part of .NET BCL, no package needed |
| YamlDotNet | 13.7.1 | YAML serialization with custom event emitters | **Already referenced** -- extend existing ForceStringScalarEmitter |
| Dynamicweb.Content.Area | 10.23.9 | Read full Area properties (60+ columns) | **Already referenced** -- `Services.Areas.GetArea()` |
| Dynamicweb.Data.Database | 10.23.9 | Direct SQL for Area columns not on C# API | **Already referenced** -- used by SqlTableReader |

### Core Technologies (unchanged from v0.4.0)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET | 8.0 | Runtime | Already in use |
| Dynamicweb | 10.23.9 | DW10 API surface | Already referenced |
| YamlDotNet | 13.7.1 | YAML serialization | Already in use |

## API Surface for v0.5.0 Features

### 1. XML Pretty-Printing -- System.Xml.Linq

**Use `XDocument.Parse()` + `SaveOptions.None` (indented output) for formatting.**

```csharp
using System.Xml.Linq;

public static string? PrettyPrintXml(string? rawXml)
{
    if (string.IsNullOrWhiteSpace(rawXml)) return rawXml;
    try
    {
        var doc = XDocument.Parse(rawXml);
        // SaveOptions.None = indented; SaveOptions.DisableFormatting = compact
        return doc.ToString(SaveOptions.None);
    }
    catch (System.Xml.XmlException)
    {
        return rawXml; // Not valid XML, return as-is
    }
}
```

**Why XDocument over XmlDocument:**
- XDocument is the modern LINQ-to-XML API, part of .NET BCL since .NET 3.5
- `ToString()` produces indented XML by default (no extra config needed)
- Lighter weight -- no DOM overhead of XmlDocument
- No namespace needed beyond `System.Xml.Linq` (already in BCL)
- XmlDocument requires explicit `XmlWriterSettings { Indent = true }` and writing to a StringWriter -- more boilerplate

**Where XML appears in the current pipeline:**

| Field | Model | Location in Pipeline |
|-------|-------|---------------------|
| `ModuleSettings` | `SerializedParagraph` | `ContentMapper.MapParagraph()` reads from `paragraph.ModuleSettings` |
| `UrlDataProviderParameters` | `SerializedUrlSettings` | `ContentMapper.MapPage()` reads from `page.UrlDataProviderParameters` |
| SQL column values | `FlatFileStore.WriteRow()` | Any `nvarchar`/`ntext`/`xml` column in SqlTable YAML |

**Integration approach:** Create an `XmlFormatter` utility class. Call it in ContentMapper before assigning to DTO fields, and in FlatFileStore/SqlTableProvider before writing row YAML. On deserialization, the formatted XML is functionally identical (XML ignores whitespace between elements by default) -- no special handling needed on the read path.

### 2. YamlDotNet -- Multi-line XML in YAML via Literal Block Scalars

**Existing `ForceStringScalarEmitter` already handles this.** Pretty-printed XML contains `\n` (LF) line breaks (XDocument.ToString uses LF), so the existing emitter will automatically select `ScalarStyle.Literal` for formatted XML strings. No YamlDotNet changes needed.

Current logic in `ForceStringScalarEmitter.Emit()`:
```csharp
if (value.Contains('\n') && !value.Contains('\r'))
    eventInfo.Style = ScalarStyle.Literal;  // <-- Pretty-printed XML hits this path
```

**Result in YAML output:**
```yaml
moduleSettings: |
  <settings>
    <module systemName="Dynamicweb.Frontend.Navigation">
      <param name="StartLevel">0</param>
      <param name="EndLevel">5</param>
    </module>
  </settings>
```

This is the ideal format for git diffs -- each XML element on its own line, YAML literal block preserves it exactly.

**FlatFileStore caveat:** The SQL-specific serializer in `FlatFileStore` does NOT use `ForceStringScalarEmitter` -- it uses a plain `SerializerBuilder`. XML values in SQL YAML files will be emitted as single-line strings. To get literal block scalars for SQL XML too, add the emitter to FlatFileStore's serializer:

```csharp
_serializer = new SerializerBuilder()
    .WithNamingConvention(CamelCaseNamingConvention.Instance)
    .WithEventEmitter(next => new ForceStringScalarEmitter(next))  // ADD THIS
    .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve)
    .Build();
```

### 3. Field-Level Blacklist Filtering

**No new libraries needed.** This is a configuration + runtime filtering concern.

**Configuration model extension** -- add to `ProviderPredicateDefinition`:
```csharp
/// <summary>Fields/columns to exclude from serialization output.</summary>
public List<string> ExcludeFields { get; init; } = new();

/// <summary>XML elements to exclude from embedded XML values.</summary>
public List<string> ExcludeXmlElements { get; init; } = new();
```

**Integration points for field filtering:**

| Provider | Where to Filter | How |
|----------|----------------|-----|
| ContentProvider (pages) | `ContentMapper.MapPage()` | Remove keys from `Fields` and `PropertyFields` dicts before DTO creation |
| ContentProvider (paragraphs) | `ContentMapper.MapParagraph()` | Remove keys from `Fields` dict |
| ContentProvider (areas) | `ContentMapper.MapArea()` | Remove keys from `ItemFields` dict |
| SqlTableProvider | `FlatFileStore.WriteRow()` or `SqlTableProvider.Serialize()` | Remove keys from row dict before writing YAML |

**For XML element blacklisting** (removing specific `<param>` elements from moduleSettings):
```csharp
using System.Xml.Linq;

public static string? FilterXmlElements(string? xml, IEnumerable<string> excludeElements)
{
    if (string.IsNullOrWhiteSpace(xml) || !excludeElements.Any()) return xml;
    try
    {
        var doc = XDocument.Parse(xml);
        doc.Descendants()
           .Where(e => excludeElements.Contains(e.Name.LocalName, StringComparer.OrdinalIgnoreCase))
           .Remove();
        return doc.ToString(SaveOptions.None);
    }
    catch (XmlException) { return xml; }
}
```

### 4. Area Property Consolidation into ContentProvider

**Two-tier approach -- C# API properties + direct SQL for the rest.**

**Tier 1: Properties available on `Dynamicweb.Content.Area` class (verified from DW 10.23.9 API docs):**

| Category | Properties |
|----------|-----------|
| Identity | UniqueId, Name, Sort, IsMaster, IsLanguage, MasterAreaId |
| Display | Domain, DomainLock, LockPagesToDomain, Culture, Codepage, Encoding, Dateformat |
| Layout | LayoutTemplate, LayoutPhoneTemplate, LayoutTabletTemplate, MasterTemplate |
| SEO | Noindex, Nofollow, RobotsTxt, RobotsTxtIncludeSitemap, IncludeProductsInSitemap |
| Ecommerce | EcomShopId, EcomLanguageId, EcomCurrencyId, EcomCountryCode, EcomPricesWithVat, StockLocationID, ReverseChargeForVat |
| SSL/Security | SslMode, PermissionTemplate |
| Navigation | Frontpage, NotFound, RedirectFirstPage, UrlName, UrlIgnoreForChildren |
| Items | ItemType, ItemId, ItemTypePageProperty, ItemTypeLayouts |
| CDN | CdnHost, CdnImageHost, IsCdnActive |
| Cookie | CookieWarningTemplate, CookieCustomNotifications |
| State | Active, Published, CopyOf, LanguageDepth |

**Tier 2: Columns NOT exposed on the C# API** (need direct SQL via `Dynamicweb.Data.Database`):

The Area SQL table has ~60 columns. Most are accessible via the C# `Area` class. For any that are not (e.g., deprecated columns, or columns only accessible via reflection), use `Database.CreateDataReader()` to read the full row -- same pattern already used by `SqlTableReader`.

**Recommendation:** Start with Tier 1 only (C# API properties). The Area class exposes the vast majority of meaningful columns. Only fall back to direct SQL if specific columns are identified as missing during implementation.

**Extend `SerializedArea` model:**
```csharp
public record SerializedArea
{
    // Existing
    public required Guid AreaId { get; init; }
    public required string Name { get; init; }
    public required int SortOrder { get; init; }
    public string? ItemType { get; init; }
    public Dictionary<string, object> ItemFields { get; init; } = new();
    public List<SerializedPage> Pages { get; init; } = new();

    // NEW -- full area properties
    public string? Domain { get; init; }
    public string? Culture { get; init; }
    public string? LayoutTemplate { get; init; }
    public string? MasterTemplate { get; init; }
    public string? EcomShopId { get; init; }
    public string? EcomLanguageId { get; init; }
    public string? EcomCurrencyId { get; init; }
    // ... etc. (full list determined during implementation)
}
```

## Alternatives Considered

| Recommended | Alternative | Why Not |
|-------------|-------------|---------|
| `System.Xml.Linq.XDocument` | `System.Xml.XmlDocument` | XmlDocument requires more boilerplate (XmlWriterSettings + StringWriter) for pretty-printing. XDocument.ToString() does it in one call. |
| `System.Xml.Linq.XDocument` | Third-party XML library (e.g., HtmlAgilityPack) | Overkill. The XML here is well-formed (DW module settings, URL provider params). No need for tolerant HTML parsing. |
| Extend `ForceStringScalarEmitter` | YamlDotNet `ITypeConverter` | Type converters operate at the type level. We need string-level formatting decisions (is this string XML?). Event emitter is the right hook. |
| Field blacklist on predicate config | Separate filter config file | Predicates already define scope. Adding field exclusions to the same predicate keeps configuration co-located. |
| C# Area API properties | Full SQL `SELECT *` from Area table | API properties are type-safe and forward-compatible. Direct SQL risks breaking on DW schema changes. Use SQL only as fallback for missing properties. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| `System.Xml.XmlDocument` | Legacy DOM API, verbose for simple formatting | `System.Xml.Linq.XDocument` |
| `XmlSerializer` | For object-to-XML mapping, not string reformatting | `XDocument.Parse()` for string-to-string reformatting |
| New NuGet packages for XML | Adds dependency for something the BCL handles | `System.Xml.Linq` (BCL) |
| YamlDotNet `IYamlTypeConverter` for XML | Wrong abstraction level -- converters handle type mapping, not string formatting | Apply XML formatting before YAML serialization |
| `Regex` for XML formatting | Fragile, doesn't handle edge cases (CDATA, attributes, namespaces) | Proper XML parser (`XDocument`) |
| `string.Replace()` for XML element removal | Fragile, can corrupt XML structure | `XDocument.Descendants().Remove()` |

## Stack Patterns by Feature

**If pretty-printing XML in content YAML:**
- Format in `ContentMapper` before assigning to DTO
- `ForceStringScalarEmitter` automatically uses literal block scalar
- No changes to deserialization (formatted XML is semantically identical)

**If pretty-printing XML in SQL table YAML:**
- Add `ForceStringScalarEmitter` to `FlatFileStore` serializer
- Format XML values in `SqlTableProvider.Serialize()` before calling `WriteRow()`
- Detect XML columns by attempting `XDocument.Parse()` -- if it succeeds, format it

**If filtering fields from content YAML:**
- Filter dictionaries (`Fields`, `PropertyFields`, `ItemFields`) in ContentMapper
- Pass `ExcludeFields` from predicate through to mapper methods

**If filtering fields from SQL YAML:**
- Filter row dictionary keys in `SqlTableProvider.Serialize()` before calling `FlatFileStore.WriteRow()`

**If filtering XML elements from embedded XML:**
- Apply after pretty-printing, before assigning to DTO
- Chain: raw XML -> XDocument.Parse -> remove elements -> ToString -> assign

## Version Compatibility

| Component | Compatible With | Notes |
|-----------|-----------------|-------|
| System.Xml.Linq | .NET 8.0 | BCL, always available |
| YamlDotNet 13.7.1 | .NET 8.0 | Already in use, no version change |
| ForceStringScalarEmitter | YamlDotNet 13.7.1 | ChainedEventEmitter API stable since YamlDotNet 8.x |
| Dynamicweb.Content.Area | 10.23.9 | 60+ properties verified in API docs |

## Sources

- [DW10 Area Class API](https://doc.dynamicweb.com/api/html/ba5b14ce-41df-687d-3d33-b006e231a86a.htm) -- full property list (HIGH confidence)
- [DW10 AreaService API](https://doc.dynamicweb.com/api/html/02c7da84-1d1c-506d-0054-da04eaff373f.htm) -- GetArea, SaveArea methods (HIGH confidence)
- [XDocument vs XmlDocument comparison](https://learn.microsoft.com/en-us/archive/technet-wiki/22352.system-xml-xmldocument-and-system-xml-linq-xdocument-comparison) -- Microsoft docs (HIGH confidence)
- [YamlDotNet literal block scalar issue #391](https://github.com/aaubry/YamlDotNet/issues/391) -- confirms ChainedEventEmitter approach (HIGH confidence)
- Existing codebase: `ForceStringScalarEmitter.cs`, `FlatFileStore.cs`, `ContentMapper.cs`, `YamlConfiguration.cs` (HIGH confidence)
- Existing codebase: `ProviderPredicateDefinition.cs` -- current predicate model (HIGH confidence)

---
*Stack research for: DynamicWeb.Serializer v0.5.0 -- Granular Serialization Control*
*Researched: 2026-04-07*
