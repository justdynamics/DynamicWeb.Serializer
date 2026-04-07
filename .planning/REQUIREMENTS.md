# Requirements: v0.5.0 Granular Serialization Control

**Defined:** 2026-04-07
**Core Value:** Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.

## v0.5.0 Requirements

### XML Pretty-Printing

- [x] **XML-01**: Embedded XML strings (moduleSettings, urlDataProviderParameters) in content YAML are pretty-printed as indented multi-line XML using YAML literal block scalars
- [x] **XML-02**: Embedded XML strings in SQL table YAML are pretty-printed using config-driven xmlColumns list per predicate
- [x] **XML-03**: Pretty-printed XML round-trips correctly — deserialize compacts XML back to single-line before writing to DB

### Field-Level Filtering

- [x] **FILT-01**: Content predicates support an excludeFields list that omits specified page/paragraph/area fields during serialization
- [x] **FILT-02**: SqlTable predicates support an excludeFields list that omits specified columns during serialization
- [x] **FILT-03**: Excluded fields are NOT nulled out during deserialization (skip guard on source-wins null-out logic)
- [x] **FILT-04**: Predicates support an excludeXmlElements list that removes specific XML element names from embedded XML blobs before serialization

### Area Consolidation

- [x] **AREA-03**: ContentProvider serializes full Area properties (60+ columns including Domain, Layout, Culture, EcomSettings, SSL, CDN, etc.) in area.yml alongside existing ItemType fields
- [x] **AREA-04**: ContentProvider deserializes full Area properties back to the database, creating the area if it doesn't exist on target
- [x] **AREA-05**: Area field-level blacklist works via the same excludeFields mechanism (e.g., exclude AreaDomain, AreaNoindex for environment-specific values)

### Predicate UI

- [x] **UI-01**: Predicate edit screen shows excludeFields configuration with a textarea or multi-select for field names
- [x] **UI-02**: SqlTable predicate edit screen shows xmlColumns configuration
- [x] **UI-03**: Predicate edit screen shows excludeXmlElements configuration

## Future Requirements (deferred)

- **Item-to-Item reference resolution** — Component Selector ID portability (numeric Item IDs across environments)
- **User Group GUID-based matching** — Cross-environment permissions portability
- **Deploy action across UI screens** — Contextual serialize/deploy on ecommerce, area, and other settings screens
- **Tree view for content comparison** — Visual diff showing content tree with changed items highlighted
- **Selective deployment (changed-only)** — Diff-based packages instead of full state
- **Timestamp preservation** — CreatedDate/UpdatedDate requires direct SQL post-save
- **Default exclusion presets** — Hardcoded common exclusions for area environment columns (add after real-world validation)

## Out of Scope

| Feature | Reason |
|---------|--------|
| XML element-level transforms (rewriting values) | Filtering only, not transformation — v0.6+ |
| Whitelist-only field filtering | Blacklist is safer default — new fields would silently disappear with whitelist |
| Auto-detect XML columns via heuristic | False positives on HTML content; config-driven xmlColumns is explicit and safe |
| Backward compatibility with pre-v0.5.0 YAML format | Beta product (0.x), no external consumers |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| XML-01 | Phase 26 | Complete |
| XML-02 | Phase 27 | Complete |
| XML-03 | Phase 26 | Complete |
| FILT-01 | Phase 28 | Complete |
| FILT-02 | Phase 29 | Complete |
| FILT-03 | Phase 28 | Complete |
| FILT-04 | Phase 28 | Complete |
| AREA-03 | Phase 30 | Complete |
| AREA-04 | Phase 30 | Complete |
| AREA-05 | Phase 30 | Complete |
| UI-01 | Phase 31 | Complete |
| UI-02 | Phase 31 | Complete |
| UI-03 | Phase 31 | Complete |

**Coverage:**
- v0.5.0 requirements: 13 total
- Mapped to phases: 13
- Unmapped: 0

---
*Requirements defined: 2026-04-07*
*Last updated: 2026-04-07 — roadmap created, all requirements mapped to phases 26-31*
