# Feature Research

**Domain:** CMS Content Serialization / Source Control Sync Tool
**Researched:** 2026-03-19
**Confidence:** HIGH (Unicorn/TDS directly analyzed; DynamicWeb deployment tool verified via official docs)

---

## Feature Landscape

### Table Stakes (Users Expect These)

Features users assume exist. Missing these means the product feels incomplete or broken.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Serialize full content tree to files | Core value proposition — without this the tool does nothing | MEDIUM | Area > Page > Grid > Row > Column > Paragraph hierarchy. Must handle all node types. |
| Deserialize files back to database | Enables cross-environment promotion — the other half of the core loop | MEDIUM | Must resolve GUIDs to existing IDs or insert as new. Parent-before-child ordering required. |
| GUID-based item identity | Numeric IDs differ per environment; GUID is the only stable identifier | LOW | PageUniqueId is the canonical key. Already chosen in PROJECT.md. |
| Mirror-tree file layout | Developers expect disk layout to reflect content hierarchy — navigable without a tool | LOW | One .yml file per item, folders reflect parent-child. Standard in Unicorn/SCS. |
| Predicate configuration (include/exclude) | Without scoping, tool syncs everything or nothing — both are wrong for real projects | MEDIUM | Path-based rules defining which trees to serialize. Unicorn calls this SerializationPresetPredicate. |
| Source-wins conflict resolution | Files as truth is the only safe default for a sync tool; ambiguity causes data loss | LOW | Disk always overwrites DB on deserialize. No merge logic required for v1. |
| Orphan handling on deserialize | Items deleted from source must also disappear on target — otherwise data accumulates | MEDIUM | Items in DB scope but not in files should be deleted. Unicorn deletes orphans by default. |
| Structured logging of changes | Developers need to know what changed and why — opaque syncs erode trust | LOW | Log: new items, updated items, deleted items, skipped items, errors. Field-level diff is ideal. |
| Error handling with clear failure messages | If a deserialize fails silently, environments diverge invisibly | LOW | Log exceptions with item context. Fail loudly; do not silently skip. |
| Config file (not UI) for configuration | Configuration must live in source control alongside the serialized content | LOW | Standalone file. DynamicWeb admin UI config deferred to v2 per PROJECT.md. |

### Differentiators (Competitive Advantage)

Features that set this product apart from the built-in DynamicWeb deployment tool. Not assumed, but valuable.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Scheduled task automation (serialize + deserialize) | Enables hands-off CI/CD workflows — no manual admin UI interaction required | LOW | Two separate tasks: serialize on push, deserialize on pull. DynamicWeb built-in tool requires manual UI clicks. |
| YAML serialization format | Human-readable, git-diff-friendly — developers can review content changes in PRs | LOW | XML is verbose and unreadable in diffs. JSON lacks comments. YAML is the SCS/Unicorn standard. |
| AppStore packaging (NuGet) | Installable without custom infrastructure — just add the package | LOW | Standard DynamicWeb AppStore distribution model. Built-in deployment tool is bundled; no equivalent open-source package exists. |
| Multiple named configurations (configuration sets) | Different teams/features can serialize independent trees without interference | MEDIUM | Unicorn supports multiple named configs. Useful when multiple feature teams share one DW instance. |
| Field-level exclusions | Certain fields (e.g., last-modified timestamps, system fields) should not be synced | MEDIUM | Prevents noisy diffs and avoids overwriting environment-specific values. Unicorn calls these FieldFilters. |
| Dependency-ordered deserialization | Parent items must exist before children can be inserted — tool handles this automatically | MEDIUM | Without ordering, foreign key constraints or parent lookups fail on insert. |

### Anti-Features (Commonly Requested, Often Problematic)

| Feature | Why Requested | Why Problematic | Alternative |
|---------|---------------|-----------------|-------------|
| Real-time change detection (event-driven serialization) | Feels more accurate — serialize exactly when content changes | Increases complexity dramatically; race conditions during bulk edits; requires Notifications API integration and careful state management. v1 scope risk. | Full scheduled serialize on a cron — simpler, predictable, deterministic. Plan for v2 using DynamicWeb Notifications API. |
| Merge/three-way conflict resolution | Teams want to merge concurrent changes instead of source-wins | Exponentially complex. TDS introduced a dedicated Sitecore Item Merge Tool as a separate product just for this. Content items are not code — merge semantics are unclear for structured data. | Source-wins for v1. Document the expectation clearly. Teams should coordinate content changes as they do with code. |
| Admin UI for configuration | Would make the tool more accessible to non-developers | Config in source control is the point — moving it to the UI defeats the developer workflow goal. Also requires DW admin module scaffolding that multiplies scope. | Config file checked into git. v2 can add a read-only status UI if needed. |
| Incremental/delta sync | Faster syncs by only processing changed items | Requires change tracking infrastructure (timestamps, checksums, event log). Dramatically increases complexity for marginal gain at typical content tree sizes. | Full sync via scheduled task is safe, deterministic, and simple for v1. |
| Media/file serialization (images, documents) | Content and media go together — why not sync both? | Binary files in git repos cause bloat. Media GUIDs and paths are environment-specific. Sitecore Unicorn explicitly excludes media by default. | Media deployment is a separate concern. Use a CDN or media sync pipeline. |
| Bi-directional sync (push and pull simultaneously) | Convenient to sync in both directions | Bi-directional sync with source-wins in both directions creates data loss. DynamicWeb's own deployment tool documents itself as not a merge tool. | Explicit directionality: serialize = DB to files, deserialize = files to DB. Never both at once. |
| Rollback / version history | Undo a bad deserialize | Rollback requires snapshotting the target DB state before deserialization — significant added complexity. | Git is the version history for the YAML files. Database-level backup/restore is the rollback mechanism for the DB state. |

---

## Feature Dependencies

```
[Predicate Configuration]
    └──required by──> [Serialize Content Tree]
    └──required by──> [Deserialize Content Tree]
    └──required by──> [Orphan Handling]

[Serialize Content Tree]
    └──produces──> [YAML Files on Disk]
                       └──consumed by──> [Deserialize Content Tree]

[GUID-Based Identity]
    └──required by──> [Deserialize Content Tree]
                           └──required by──> [Orphan Handling]

[Deserialize Content Tree]
    └──requires──> [Dependency-Ordered Deserialization] (parent before child)

[Scheduled Tasks]
    └──wraps──> [Serialize Content Tree]
    └──wraps──> [Deserialize Content Tree]

[Field Exclusions] ──enhances──> [Serialize Content Tree]
[Multiple Named Configs] ──enhances──> [Predicate Configuration]
```

### Dependency Notes

- **Predicate Configuration requires resolution before serialize/deserialize:** Without a resolved predicate, the tool doesn't know what scope to operate on. Config file must be parsed first.
- **Deserialize requires GUID-based identity:** Without GUIDs, the tool cannot determine whether to update an existing item or insert a new one.
- **Orphan handling depends on deserialize scope:** Orphan detection only makes sense within the predicate scope — items outside the scope are never orphans.
- **Dependency-ordered deserialization is non-optional:** DynamicWeb's content model requires parent pages to exist before child pages/grids/rows/paragraphs can be inserted. This is a hard constraint.
- **Scheduled tasks are wrappers:** They add no logic — they call serialize and deserialize respectively. Can be built after core logic is stable.

---

## MVP Definition

### Launch With (v1)

| Feature | Why Essential |
|---------|---------------|
| Serialize full content tree (Area > Page > Grid > Row > Paragraph) | Core value — nothing works without this |
| YAML file output with mirror-tree layout | Enables git diff and source control — the whole point |
| GUID-based identity resolution on deserialize | Without this, every sync creates duplicates or fails on re-run |
| Predicate configuration via config file | Without scoping, tool is unsafe on any real installation |
| Source-wins conflict resolution | Safe default — no ambiguity, no merge complexity |
| Orphan handling (delete items in scope but not in files) | Without this, deleted content accumulates on target |
| Dependency-ordered deserialization | Required for correctness — parent-before-child is mandatory |
| Scheduled tasks (serialize + deserialize as separate tasks) | Primary execution mechanism — how users trigger the tool |
| Structured logging (what changed, what failed) | Required for trust — silent operations are unusable |
| Error handling (fail loudly with context) | Prevent silent divergence between environments |

### Add After Validation (v1.x)

| Feature | Trigger for Adding |
|---------|-------------------|
| Field-level exclusions (FieldFilters) | When users report noisy diffs from system-managed fields (e.g., ModifiedDate) |
| Multiple named configurations | When a customer has two independent content trees that shouldn't mix |
| Dry-run mode (report what would change without applying) | When users are nervous about running deserialize on production |

### Future Consideration (v2+)

| Feature | Why Defer |
|---------|-----------|
| Real-time change detection via Notifications API | High complexity; full sync is sufficient for v1 |
| Admin UI for configuration and status | Out of scope for v1 per PROJECT.md; requires additional DW module scaffolding |
| Incremental/delta sync | Requires change tracking infrastructure; full sync is safe and predictable |
| Media/file serialization | Binary files in git; separate deployment concern |

---

## Feature Prioritization Matrix

| Feature | User Value | Implementation Cost | Priority |
|---------|------------|---------------------|----------|
| Serialize content tree to YAML | HIGH | MEDIUM | P1 |
| Deserialize YAML to database | HIGH | MEDIUM | P1 |
| GUID-based identity | HIGH | LOW | P1 |
| Mirror-tree file layout | HIGH | LOW | P1 |
| Predicate configuration | HIGH | MEDIUM | P1 |
| Source-wins conflict resolution | HIGH | LOW | P1 |
| Orphan handling | HIGH | MEDIUM | P1 |
| Dependency-ordered deserialization | HIGH | MEDIUM | P1 |
| Scheduled tasks (serialize + deserialize) | HIGH | LOW | P1 |
| Structured logging | MEDIUM | LOW | P1 |
| Error handling | HIGH | LOW | P1 |
| Field-level exclusions | MEDIUM | MEDIUM | P2 |
| Multiple named configurations | MEDIUM | MEDIUM | P2 |
| Dry-run mode | MEDIUM | LOW | P2 |
| Real-time change detection | LOW | HIGH | P3 |
| Admin UI configuration | LOW | HIGH | P3 |
| Incremental sync | LOW | HIGH | P3 |
| Media serialization | LOW | HIGH | P3 |

**Priority key:**
- P1: Must have for launch
- P2: Should have, add when possible
- P3: Nice to have, future consideration

---

## Competitor Feature Analysis

| Feature | Sitecore Unicorn | Sitecore TDS | DynamicWeb Built-in Deployment | Our Approach |
|---------|-----------------|--------------|-------------------------------|--------------|
| Serialization format | YAML (Rainbow) | .item (proprietary) | Proprietary binary package | YAML — git-friendly, human-readable |
| File layout | Mirror-tree | Visual Studio project | Single package file | Mirror-tree — directly navigable |
| Identity model | GUID | GUID | Numeric ID | GUID (PageUniqueId) |
| Conflict resolution | Source-wins (disk master) | Visual Studio merge | Last-write-wins (manual) | Source-wins |
| Predicate config | XML config (include/exclude paths) | VS project file | Data configuration sets | Config file with include/exclude |
| Orphan handling | Deletes items in scope not on disk | Configurable | None (additive only) | Delete orphans within scope |
| Automation | PowerShell API + MicroCHAP auth | MSBuild tasks | Manual admin UI only | Scheduled tasks |
| CI/CD integration | Yes (via API) | Yes (via MSBuild) | No | Yes (via scheduled tasks triggerable by deploy scripts) |
| Field exclusions | Yes (FieldFilters) | Yes | No | v1.x |
| Multiple named configs | Yes | Yes | No | v1.x |
| Admin UI | Control panel at /unicorn.aspx | Visual Studio | Full admin UI | Config file only (v1) |
| Open source | Yes | No (commercial) | No (bundled) | Yes |
| Change detection | Yes (automatic on save) | Yes (auto sync) | No | v2 (Notifications API) |

---

## Sources

- [Unicorn GitHub Repository](https://github.com/SitecoreUnicorn/Unicorn) — MEDIUM confidence (GitHub README, verified against multiple secondary sources)
- [Sitecore Serialization: SCS vs Unicorn vs TDS](https://the-sitecore-chronicles.cyber-solutions.at/blogs/sitecore-content-serialization-vs-unicorn-and-tds-a-deep-dive-with-examples) — MEDIUM confidence (community blog, consistent with primary sources)
- [Unicorn Field Filter documentation](https://www.flux-digital.com/blog/excluding-specific-fields-unicorn-serialisation-field-filter/) — MEDIUM confidence (verified against Unicorn GitHub)
- [Unicorn sync deletes orphans — Issue #259](https://github.com/SitecoreUnicorn/Unicorn/issues/259) — HIGH confidence (official project issue tracker)
- [DynamicWeb Deployment Tool official docs](https://doc.dynamicweb.com/documentation-9/platform/platform-tools/deployment-tool) — HIGH confidence (official documentation)
- [Sitecore TDS and Unicorn comparison brochure](https://www.teamdevelopmentforsitecore.com/-/media/TDS/Files/Brochures/2020-SitecoreTDSandUnicorn-Update-v2-LTR-061520.pdf) — MEDIUM confidence (vendor-produced, 2020)
- [Unicorn Octopus Deploy integration](https://www.sitecorenutsbolts.net/2016/03/14/Octopus-Deploy-Step-for-Unicorn-Sync/) — LOW confidence (third-party, dated 2016, pattern still valid)

---

*Feature research for: CMS Content Serialization / Source Control Sync Tool (Dynamicweb.ContentSync)*
*Researched: 2026-03-19*
