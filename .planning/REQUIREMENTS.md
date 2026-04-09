# Requirements: v0.6.0 UI Configuration Improvements

**Defined:** 2026-04-09
**Core Value:** Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.

## v0.6.0 Requirements

### Config Schema

- [ ] **CFG-01**: Config JSON extended with `excludeFieldsByItemType` (Dict<string, List<string>>) and `excludeXmlElementsByType` (Dict<string, List<string>>) alongside existing flat arrays
- [ ] **CFG-02**: Existing v0.5.0 configs with flat `excludeFields`/`excludeXmlElements` arrays continue to work (additive, no breaking changes)

### Item Types

- [ ] **ITEM-01**: "Item Types" tree node under Serialize lists all item types discovered in the system
- [ ] **ITEM-02**: Item type edit screen shows all fields for that item type as a CheckboxList where user selects fields to exclude from serialization
- [ ] **ITEM-03**: Item type field exclusions are persisted to config JSON under `excludeFieldsByItemType` and applied during serialize/deserialize

### Areas

- [ ] **AREA-06**: "Areas" tree node under Serialize lists all areas in the system
- [ ] **AREA-07**: Area edit screen shows all area columns as a CheckboxList where user selects columns to exclude
- [ ] **AREA-08**: Area column exclusions are persisted to config and applied during serialize/deserialize

### Embedded XML

- [ ] **XMLUI-01**: "Embedded XML" tree node under Serialize lists auto-discovered XML types (modules + URL providers)
- [ ] **XMLUI-02**: "Scan" action button discovers XML types via SQL (SELECT DISTINCT moduleSystemName, urlDataProviderTypeName)
- [ ] **XMLUI-03**: XML type edit screen shows all elements from that XML type as a CheckboxList for element exclusion
- [ ] **XMLUI-04**: XML element exclusions are persisted to config under `excludeXmlElementsByType` and applied during serialize

### Predicate UI

- [ ] **PRED-01**: Content predicate page exclusions use multi-select page picker instead of free-text textarea
- [ ] **PRED-02**: Predicate filtering section shows item field exclusions as read-only with link to Item Types screen
- [ ] **PRED-03**: Predicate filtering section shows XML element exclusions as read-only with link to Embedded XML screen
- [ ] **PRED-04**: SqlTable predicate excludeFields uses CheckboxList populated from table schema instead of textarea
- [ ] **PRED-05**: SqlTable predicate xmlColumns uses CheckboxList populated from table schema instead of textarea

## Future Requirements (deferred)

- Per-predicate item-type exclusion overrides (global-first, per-predicate later)
- Real-time preview of serialization output with current config
- Drag-and-drop field ordering for serialized output
- XML element value transforms (rewrite values, not just exclude)

## Out of Scope

| Feature | Reason |
|---------|--------|
| Inline editing on Item Edit/Area Edit screens | DW EditScreenInjector has no save hook for custom properties |
| Per-predicate item type overrides | Global exclusions sufficient for v0.6.0; per-predicate adds complexity |
| SqlTable XML element auto-discovery | Content XML auto-discovery first; SqlTable XML types less common |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CFG-01 | Phase 32 | Pending |
| CFG-02 | Phase 32 | Pending |
| ITEM-01 | Phase 35 | Pending |
| ITEM-02 | Phase 35 | Pending |
| ITEM-03 | Phase 35 | Pending |
| AREA-06 | Phase 36 | Pending |
| AREA-07 | Phase 36 | Pending |
| AREA-08 | Phase 36 | Pending |
| XMLUI-01 | Phase 34 | Pending |
| XMLUI-02 | Phase 34 | Pending |
| XMLUI-03 | Phase 34 | Pending |
| XMLUI-04 | Phase 34 | Pending |
| PRED-01 | Phase 37 | Pending |
| PRED-02 | Phase 37 | Pending |
| PRED-03 | Phase 37 | Pending |
| PRED-04 | Phase 33 | Pending |
| PRED-05 | Phase 33 | Pending |

**Coverage:**
- v0.6.0 requirements: 17 total
- Mapped to phases: 17
- Unmapped: 0

---
*Requirements defined: 2026-04-09*
*Last updated: 2026-04-07 after roadmap creation*
