# Requirements: DynamicWeb.Serializer

**Defined:** 2026-03-23
**Core Value:** Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.

## v2.0 Requirements

Requirements for the DynamicWeb.Serializer milestone. Each maps to roadmap phases.

### Provider Architecture

- [x] **PROV-01**: Pluggable ISerializationProvider interface with Serialize/Deserialize/DryRun contract
- [x] **PROV-02**: Provider registry mapping data type strings to provider instances
- [x] **PROV-03**: Existing ContentSerializer/ContentDeserializer wrapped as ContentProvider adapter
- [x] **PROV-04**: Orchestrator coordinates multiple providers based on predicate data types

### SQL Table Serialization

- [x] **SQL-01**: SqlTableProvider serializes any SQL table to YAML using DataGroup XML metadata (Table, NameColumn, CompareColumns)
- [x] **SQL-02**: Identity resolution matches rows by NameColumn with CompareColumns fallback for empty NameColumn tables
- [ ] **SQL-03**: FK dependency ordering via topological sort prevents constraint violations during deserialization
- [x] **SQL-04**: Structured result objects report rows added/updated/skipped/failed per table
- [x] **SQL-05**: Source-wins conflict strategy: YAML rows overwrite matched target rows

### Ecommerce Data Groups

- [ ] **ECOM-01**: OrderFlows and OrderStates serialized and deserialized
- [ ] **ECOM-02**: Payment and Shipping methods serialized and deserialized
- [ ] **ECOM-03**: Countries, Currencies, and VAT settings serialized and deserialized
- [ ] **ECOM-04**: Duplicate DataItemTypes across groups (e.g., EcomMethodCountryRelation) handled without duplicate rows

### Cache & Config

- [ ] **CACHE-01**: DW service caches invalidated after SQL table deserialization so admin UI reflects new data
- [x] **CACHE-02**: Predicate definitions extended with DataType field for provider routing
- [x] **CACHE-03**: Existing v1.x configs without DataType default to "Content" (backward compatibility)

### Admin UX

- [ ] **UX-01**: Log viewer screen shows per-provider summaries with guided advice (e.g., "Create missing groups: X, Y")
- [ ] **UX-02**: Deserialize action available on Asset management file detail page for zip files
- [ ] **UX-03**: Admin tree node relocated from Settings > Content > Sync to Settings > Database > Serialize
- [ ] **UX-04**: Scheduled tasks deprecated (API commands are the replacement)

### Rename

- [ ] **REN-01**: Project renamed from Dynamicweb.ContentSync to DynamicWeb.Serializer (namespace, assembly, NuGet package)

## Future Requirements

### Settings & Schema Providers

- **SET-01**: SettingsDataItemProvider serializes ~20 settings items to YAML
- **SET-02**: Environment-specific settings blocklist prevents dangerous key paths from serialization
- **SCH-01**: SchemaDataItemProvider exports ~5 schema definitions as reference YAML

### Remaining Data Groups

- **REM-01**: Users, Marketing, PIM, Apps SQL tables (~30 tables) serialized via SqlTableProvider
- **REM-02**: DataGroup auto-discovery from DW XML metadata at runtime

### Publishing

- **PUB-01**: App published to NuGet registry with dynamicweb-app-store tag
- **PUB-02**: Tested across multiple content trees beyond Customer Center

## Out of Scope

| Feature | Reason |
|---------|--------|
| Bidirectional merge / conflict resolution | Exponential complexity for arbitrary SQL rows; source-wins is proven |
| Incremental / delta sync | DW tables lack uniform modification timestamps; full sync is fast enough for config data |
| Real-time change detection (Notifications API) | Serializing in request context is slow and error-prone |
| Transactional data (orders, carts, sessions) | High-volume, environment-specific, not deployment artifacts |
| Per-field merge rules | Configuration nightmare across 74 tables |
| Schema migration (ALTER TABLE) | Use DW's own migration system or purpose-built tools |
| File / media serialization | Files stay in git directly; 24 file-based data groups excluded |
| Provider-level parallelism | FK ordering conflicts during parallel writes; sequential is correct |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| PROV-01 | Phase 13 | Complete |
| PROV-02 | Phase 13 | Complete |
| PROV-03 | Phase 14 | Complete |
| PROV-04 | Phase 14 | Complete |
| SQL-01 | Phase 13 | Complete |
| SQL-02 | Phase 13 | Complete |
| SQL-03 | Phase 15 | Pending |
| SQL-04 | Phase 13 | Complete |
| SQL-05 | Phase 13 | Complete |
| ECOM-01 | Phase 15 | Pending |
| ECOM-02 | Phase 15 | Pending |
| ECOM-03 | Phase 15 | Pending |
| ECOM-04 | Phase 15 | Pending |
| CACHE-01 | Phase 15 | Pending |
| CACHE-02 | Phase 14 | Complete |
| CACHE-03 | Phase 14 | Complete |
| UX-01 | Phase 16 | Pending |
| UX-02 | Phase 16 | Pending |
| UX-03 | Phase 16 | Pending |
| UX-04 | Phase 16 | Pending |
| REN-01 | Phase 17 | Pending |

**Coverage:**
- v2.0 requirements: 21 total
- Mapped to phases: 21
- Unmapped: 0

---
*Requirements defined: 2026-03-23*
*Last updated: 2026-03-23 after roadmap creation*
