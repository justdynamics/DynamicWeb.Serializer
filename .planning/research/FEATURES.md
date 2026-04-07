# Feature Research: Granular Serialization Control (v0.5.0)

**Domain:** CMS content serialization -- embedded XML handling, field-level filtering, area consolidation
**Researched:** 2026-04-07
**Confidence:** HIGH (established patterns from Sitecore Unicorn/Rainbow, well-understood XML/YAML mechanics)

## Feature Landscape

### Table Stakes (Users Expect These)

Features that any serialization tool with "granular control" must have. Without these, the milestone feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| XML pretty-printing in content YAML | `moduleSettings` and `urlDataProviderParameters` are currently single-line escaped strings -- unreadable in git diffs, impossible to review in PRs | MEDIUM | Rainbow (Unicorn's YAML layer) pretty-prints XML fields via `XmlFieldFormatter` specifically because XML diffs are the #1 source of merge conflicts. Use `XDocument.Parse()` + `ToString()` to indent, then emit as YAML literal block scalar (`\|`). Depends on existing `ForceStringScalarEmitter`. |
| XML pretty-printing in SQL table YAML | SQL tables like `EcomPayments`, `EcomShippings` store XML config blobs. Same readability problem as content XML. | MEDIUM | Same technique as content XML but applied in `FlatFileStore` serialization path. Must detect XML-shaped strings (starts with `<`) and format them. |
| Field-level blacklist for pages/paragraphs | Environment-specific fields (timestamps, CreatedBy/UpdatedBy, SourcePageId) clutter diffs. Unicorn's `fieldFilter` excludes `__Updated`, `__Revision`, `__Owner` by default for exactly this reason. | MEDIUM | Needs per-predicate config (list of field names to exclude). Applied during serialization in `ContentMapper`. Unicorn learned the hard way that global-only filtering is insufficient -- they added per-configuration overrides in v4.0. |
| Field-level blacklist for SQL columns | Same rationale as content fields. Some SQL columns are environment-specific (auto-increment IDs, timestamps, machine-specific paths). | LOW | Applied during `SqlTableReader.ReadAllRows()` post-processing or in `FlatFileStore.WriteRow()`. Simpler than content because columns are flat key-value, no nesting. |
| Area property consolidation into ContentProvider | Currently `SerializedArea` only has 5 properties (AreaId, Name, SortOrder, ItemType, ItemFields). Real DW areas have 60+ columns (domain, culture, SSL, master page, layout, ecom settings). Incomplete area sync = broken websites. | HIGH | Must read all Area columns via DW `Services.Areas` API or direct SQL, add them to `SerializedArea`, serialize to `area.yml`. Deserialization must write them back. This is the largest single feature. |
| Predicate UI for field blacklists | Users need to configure field exclusions without editing JSON config files. Current predicate edit screen has no field-level controls. | MEDIUM | Extend `PredicateEditScreen` with a textarea or multi-select for excluded fields. Must work for both Content and SqlTable predicates. |

### Differentiators (Competitive Advantage)

Features that go beyond what comparable tools offer, or solve DynamicWeb-specific problems elegantly.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| XML element-level blacklist | Exclude specific XML elements *within* a pretty-printed XML blob (e.g., remove `<cache>` settings from moduleSettings). No comparable tool does sub-field XML filtering -- Unicorn's fieldFilter works at the field level, not inside XML content. | MEDIUM | Parse XML, remove matching elements by XPath or element name, then pretty-print remainder. Applied after pretty-printing, before YAML emission. Powerful for stripping environment-specific XML settings without excluding the entire field. |
| Blacklist-as-default with sensible presets | Ship default exclusion lists per entity type (e.g., always exclude `CreatedDate`, `UpdatedDate`, `CreatedBy`, `UpdatedBy` from pages/paragraphs unless overridden). Unicorn ships default field exclusions in `Unicorn.config` -- users expect sane defaults. | LOW | Hardcoded defaults in code, overridable per predicate in config. Reduces config burden for the 90% case. |
| Area property field filtering | Apply the same field blacklist to area columns. Some area columns are environment-specific (domain names, SSL settings, analytics IDs). | LOW | Reuses the same blacklist mechanism as page/paragraph filtering, just applied to `SerializedArea` properties. |
| Round-trip fidelity for pretty-printed XML | Pretty-printed XML must deserialize back to functionally identical XML (whitespace-insensitive comparison). DW may store XML with inconsistent formatting -- normalizing on serialize and comparing normalized on deserialize prevents false diffs. | LOW | `XDocument.Parse()` normalizes automatically. Store pretty-printed, compare normalized. No extra work beyond the pretty-print implementation. |

### Anti-Features (Commonly Requested, Often Problematic)

Features that seem valuable but create more problems than they solve.

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Field *whitelist* (include-only mode) | "I only want these 5 fields" | Brittle -- new fields added by DW upgrades silently excluded, causing data loss on deserialize. Sitecore Unicorn Issue #305 explicitly rejected per-config include-only filtering. | Use blacklist (exclude mode). New fields serialize by default, user explicitly opts out. Safe by default. |
| XML schema validation on serialize | "Ensure XML is valid before writing" | DW stores invalid/partial XML in some fields. Validating would reject real data. ModuleSettings XML is not always well-formed (legacy modules). | Pretty-print with try/catch -- if XML parse fails, fall back to raw string. Log a warning. |
| Automatic field discovery UI (introspect DB schema) | "Show me all available columns to exclude" | Requires live DB connection at config time, couples UI to specific environment's schema. Columns differ between DW versions. | Free-text field name entry (textarea, one per line). Users know their own fields. Optional: show column list as hint text from current environment. |
| Merge/diff strategy for XML fields | "Smart merge of XML changes from two sources" | Massive complexity, XML merge is an unsolved problem. Source-wins strategy means no merging needed. | Stick with source-wins. Pretty-printed XML makes manual merge in git easier. |
| Per-field conflict resolution | "Keep target value for field X, use source for field Y" | Breaks the simple source-wins mental model. Partial merges create inconsistent state. | Exclude environment-specific fields via blacklist instead. If a field is excluded, target keeps its value. |
| Transform/rewrite rules for field values | Unicorn 4.1 added "field transforms" (replace value on deploy). | Significant complexity for niche use case. DW Serializer is not yet at the adoption level where transforms are needed. | Defer to future milestone. Field blacklist covers 90% of the use case (exclude env-specific fields). |

## Feature Dependencies

```
XML Pretty-Print (Content)
    |-- depends on --> ForceStringScalarEmitter (existing, needs XML-aware branch)
    |-- depends on --> System.Xml.Linq (XDocument.Parse + ToString)

XML Pretty-Print (SQL Tables)
    |-- depends on --> FlatFileStore (existing, needs XML detection)
    |-- depends on --> System.Xml.Linq

XML Element Blacklist
    |-- depends on --> XML Pretty-Print (must parse XML first)
    |-- depends on --> Predicate config (needs excludeXmlElements field)

Field Blacklist (Content)
    |-- depends on --> ProviderPredicateDefinition (needs ExcludeFields property)
    |-- depends on --> ContentMapper (apply filtering during map)
    |-- depends on --> ContentSerializer (pass predicate context to mapper)

Field Blacklist (SQL Tables)
    |-- depends on --> ProviderPredicateDefinition (same ExcludeFields property)
    |-- depends on --> FlatFileStore or SqlTableProvider (apply filtering)

Area Consolidation
    |-- depends on --> SerializedArea model expansion (60+ new properties)
    |-- depends on --> ContentMapper.MapArea() rewrite
    |-- depends on --> ContentDeserializer area write-back
    |-- depends on --> Field Blacklist (area columns need filtering too)

Predicate UI Enhancement
    |-- depends on --> ProviderPredicateDefinition schema (all new fields added first)
    |-- depends on --> PredicateEditScreen (new UI controls)
    |-- depends on --> ConfigWriter/ConfigLoader (persist new fields)
```

### Dependency Notes

- **XML Element Blacklist requires XML Pretty-Print:** You must parse the XML to remove elements. Pretty-print is the prerequisite.
- **Area Consolidation benefits from Field Blacklist:** Area has 60+ columns, many environment-specific. Without blacklist, area.yml contains domain names, analytics IDs, etc. that differ between environments.
- **Predicate UI must come last:** All config schema changes (ExcludeFields, ExcludeXmlElements) must be finalized before building UI.
- **Content and SQL XML pretty-print are independent:** Can be built in parallel, different code paths.

## MVP Definition

### Launch With (v0.5.0 core)

- [x] XML pretty-printing for content YAML (moduleSettings, urlDataProviderParameters) -- highest user-visible value, makes git diffs readable
- [x] XML pretty-printing for SQL table YAML -- same technique, different code path
- [x] Field-level blacklist config schema on ProviderPredicateDefinition -- ExcludeFields string list
- [x] Field blacklist applied in ContentMapper and FlatFileStore -- actual filtering
- [x] Area property consolidation -- full 60+ columns in area.yml
- [x] Predicate UI for field blacklist configuration -- textarea for excluded field names

### Add After Validation (v0.5.x)

- [ ] XML element-level blacklist -- only if users report needing sub-field XML filtering
- [ ] Default exclusion presets per entity type -- reduce config burden after patterns emerge from real usage
- [ ] Column hint text in predicate UI -- show available columns from current environment as help text

### Future Consideration (v0.6+)

- [ ] Field transforms (Unicorn 4.1 style value replacement on deploy) -- only when adoption justifies complexity
- [ ] Schema-aware field validation -- verify excluded field names exist in target schema
- [ ] Conditional field inclusion by environment tag -- "exclude AnalyticsId only in dev"

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| XML pretty-print (content) | HIGH | MEDIUM | P1 |
| XML pretty-print (SQL) | HIGH | LOW | P1 |
| Area consolidation (60+ columns) | HIGH | HIGH | P1 |
| Field blacklist (content) | HIGH | MEDIUM | P1 |
| Field blacklist (SQL) | MEDIUM | LOW | P1 |
| Predicate UI enhancement | MEDIUM | MEDIUM | P1 |
| XML element blacklist | MEDIUM | MEDIUM | P2 |
| Default exclusion presets | LOW | LOW | P2 |
| Area field blacklist | MEDIUM | LOW | P1 |

**Priority key:**
- P1: Must have for v0.5.0 launch
- P2: Should have, add if time permits or defer to v0.5.x
- P3: Nice to have, future consideration

## Competitor Feature Analysis

| Feature | Sitecore Unicorn/Rainbow | Sitecore SCS (native) | DW Serializer (our approach) |
|---------|--------------------------|----------------------|------------------------------|
| XML pretty-print | YES -- `XmlFieldFormatter` pretty-prints layout and rules XML in YAML output | Not documented | Implement via `XDocument.Parse()` + literal block scalar. Same approach as Rainbow. |
| Field exclusion scope | Global `fieldFilter` in defaults; per-configuration override in v4.0+. Cannot scope to individual items. | Global `excludedFields` array. No per-module scoping. | Per-predicate `excludeFields` list. More granular than Unicorn (scoped to predicate, not just configuration). |
| Field exclusion by | Field GUID (`fieldID` attribute) | Field GUID | Field name (string). Simpler, no GUID lookup needed. Works for both Content item fields and SQL columns. |
| Sub-field filtering | Not supported. Field is all-or-nothing. | Not supported. | XML element blacklist (unique differentiator). |
| Area/site-level properties | Not applicable (Sitecore sites are config-defined). | Not applicable. | Full 60+ column serialization with field blacklist. DW-specific need. |
| Default exclusions | Ships with `__Updated`, `__Revision`, `__Owner`, `Last run` excluded. | Ships with Owner, Revision, Updated, UpdatedBy, Lock excluded. | Ship defaults for CreatedDate, UpdatedDate, CreatedBy, UpdatedBy. Same pattern. |
| Field transforms | YES (v4.1) -- replace values on deploy per predicate/include. | Not supported. | Deferred. Blacklist covers 90% of use case. |
| Multiline values | YES -- multilists stored multi-line for fewer conflicts. | YAML format with multiline. | Already handled by `ForceStringScalarEmitter` literal block scalar. |

## Sources

- [Sitecore Unicorn Field Filter](https://www.flux-digital.com/blog/excluding-specific-fields-unicorn-serialisation-field-filter/) -- per-configuration field exclusion patterns
- [Unicorn Issue #305: Sync only specific fields](https://github.com/SitecoreUnicorn/Unicorn/issues/305) -- why include-only was rejected
- [Rainbow YAML serialization](https://kamsar.net/index.php/2015/07/Rethinking-the-Sitecore-Serialization-Format-Unicorn-3-Preview-part-1/) -- XML field formatting rationale
- [Sitecore SCS excluded fields](https://doc.sitecore.com/xp/en/developers/latest/developer-tools/configure-excluded-fields.html) -- native Sitecore field exclusion
- [Unicorn 4.1 Field Transforms](https://intothecloud.blog/2019/05/26/Rise-of-the-Unicorn-Transformers/) -- per-predicate/include field transform scoping
- [Sitecore SCS configuration reference](https://doc.sitecore.com/xp/en/developers/latest/developer-tools/sitecore-content-serialization-configuration-reference.html) -- rules and scoping
- [YamlDotNet literal block scalar](https://github.com/aaubry/YamlDotNet/issues/391) -- ScalarStyle.Literal for multiline strings
- [YAML multiline reference](https://yaml-multiline.info/) -- literal vs folded block scalar semantics
- [Rainbow GitHub](https://github.com/SitecoreUnicorn/Rainbow) -- XmlFieldFormatter and field formatting architecture

---
*Feature research for: DynamicWeb.Serializer v0.5.0 Granular Serialization Control*
*Researched: 2026-04-07*
