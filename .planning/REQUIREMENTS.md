# Requirements: v0.3.1 Internal Link Resolution

**Defined:** 2026-04-03
**Core Value:** Developers can reliably move DynamicWeb database state between environments through source control, with serialized YAML files as the single source of truth.

## Link Resolution

- [ ] **LINK-01**: Serializer detects `Default.aspx?ID=NNN` patterns in ItemType field values and rewrites page IDs to target environment IDs during deserialization
- [ ] **LINK-02**: Source-to-target page ID mapping is built using PageUniqueId (GUID) as the bridge — source numeric ID → GUID (from YAML) → target numeric ID (from PageGuidCache)
- [ ] **LINK-03**: Link resolution handles all field types: structured link fields, button fields, and rich text HTML containing embedded links
- [ ] **LINK-04**: Links referencing pages that don't exist in the target are preserved as-is and logged as warnings (no data corruption)
- [ ] **LINK-05**: Paragraph anchor fragments (`Default.aspx?ID=NNN#PPP`) are resolved for both page ID and paragraph ID

## Serialization

- [ ] **SER-01**: SerializedPage DTO includes SourcePageId (numeric) alongside the existing GUID, enabling ID mapping construction at deserialization time
- [ ] **SER-02**: SerializedParagraph DTO includes SourceParagraphId for paragraph anchor resolution

## Versioning

- [ ] **VER-01**: Git tags re-created from v1.0/v2.0 scheme to 0.x pre-release versioning

## Future Requirements (deferred)

- Product/group link resolution (`ProductID=`, `GroupID=` references) — separate concern from page links
- GUID-native YAML storage (store GUIDs instead of numeric IDs in link fields) — breaking format change, deferred to v0.4+

## Out of Scope

- HTML DOM parsing — regex/string replacement suffices for `Default.aspx?ID=` pattern
- Per-field-type mapper system — DW uses uniform link format across all field types
- Real-time link validation — sync tool, not a link checker

## Traceability

| REQ-ID | Phase | Plan | Status |
|--------|-------|------|--------|
| LINK-01 | — | — | Pending |
| LINK-02 | — | — | Pending |
| LINK-03 | — | — | Pending |
| LINK-04 | — | — | Pending |
| LINK-05 | — | — | Pending |
| SER-01 | — | — | Pending |
| SER-02 | — | — | Pending |
| VER-01 | — | — | Pending |
