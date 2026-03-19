# Pitfalls Research

**Domain:** CMS content serialization / cross-environment sync tooling (DynamicWeb AppStore)
**Researched:** 2026-03-19
**Confidence:** HIGH (critical pitfalls derived from Sitecore Unicorn/Rainbow source, YamlDotNet issues, database FK constraint literature, and filesystem limitations — all verified against multiple sources)

---

## Critical Pitfalls

### Pitfall 1: Inserting Children Before Parents During Deserialization

**What goes wrong:**
During deserialization, if items are written to the database in file-system traversal order (e.g., alphabetical) rather than depth-first parent-first order, child items arrive before their parent rows exist. DynamicWeb's internal FK relationships (Page.ParentPageId, Paragraph.PageId, etc.) cause constraint violations or silent orphaning depending on whether FK enforcement is strict.

**Why it happens:**
Developers iterate over YAML files using directory enumeration, which returns files in undefined or alphabetical order. Alphabetical order does not guarantee parent-before-child insertion. On deserializing a new target environment where nothing exists yet, every row is an INSERT — so the parent must precede the child unconditionally.

**How to avoid:**
Build a depth-first, parent-before-child insertion queue before writing anything. When loading all YAML files into memory, sort them by tree depth (depth = 0 for Areas, 1 for Pages, 2 for Grids, etc.) before beginning the DB write pass. Never write to the database during file traversal — collect all deserialized objects first, sort, then write.

**Warning signs:**
- FK constraint exception messages during deserialization
- Pages with `ParentPageId` pointing to a numeric ID that was not yet inserted
- Grids/Rows/Paragraphs appearing with no associated page in the target DB
- Silent partial success: some content appears, some is missing with no error logged

**Phase to address:**
Deserialization core implementation phase. Must be designed into the write-pass architecture from the start — retrofitting correct ordering is significantly harder than building it in.

---

### Pitfall 2: YAML Round-Trip Data Loss from Special Characters

**What goes wrong:**
CMS content fields routinely contain HTML markup, rich text with embedded quotes, tilde (`~`) characters, Windows-style CRLF line endings, tab characters, and exclamation marks. YamlDotNet's default serialization does not quote or escape these correctly, causing deserialization to recover a different value than what was serialized. The tilde is parsed as YAML null. CRLFs become LFs or disappear. HTML with `>` or `&` survives but other characters may not.

**Why it happens:**
YamlDotNet uses scalar style inference by default — it chooses between plain, single-quoted, double-quoted, and literal block styles based on content heuristics. These heuristics have known gaps for: tilde, tab (`\t`), carriage return (`\r`), and single-character strings containing punctuation. The library has open issues dating to 2018 that are still present.

**How to avoid:**
- Use `ScalarStyle.Literal` (pipe block `|`) for any multiline or HTML-containing string fields. Implement a custom `ChainedEventEmitter` that forces `ScalarStyle.Literal` when a string contains newlines.
- Use `ScalarStyle.DoubleQuoted` as the default for all other string fields, or use `[YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]` on fields known to carry rich content.
- Write a round-trip fidelity test early: serialize a known-tricky string (with `~`, `\r\n`, `<html>`, `"quotes"`, `!bang`), deserialize it, and assert byte-for-byte equality before shipping any serialization code.

**Warning signs:**
- Content field values truncated at `~` character
- Rich text fields losing line breaks after round-trip
- HTML in paragraph body fields getting corrupted
- Any test that serializes and immediately deserializes returns a different value than the original

**Phase to address:**
YAML serialization foundation phase (the very first thing implemented). Round-trip fidelity must be proven before any other serialization work begins.

---

### Pitfall 3: Numeric ID Leakage — Serializing Environment-Specific IDs as References

**What goes wrong:**
DynamicWeb content items reference each other by numeric ID in database columns (e.g., a paragraph may reference another page by PageId). If these numeric foreign references are serialized as raw integers into YAML, they become meaningless or dangerous on the target environment — where the same content exists at a completely different numeric ID. Deserializing them creates broken internal links, or worse, links pointing to unrelated content that happens to share the numeric ID.

**Why it happens:**
The canonical identity strategy (GUID) is correctly applied to the item's own ID, but internal cross-references between items are often overlooked. Developers focus on the primary key mapping and forget that field values can also be numeric IDs (e.g., "link to page 8385" stored in a rich text field or paragraph property).

**How to avoid:**
- Audit every column and field type that DynamicWeb uses to store inter-item references. Create an explicit list of "reference fields" vs. "content fields."
- For reference fields: serialize as GUID (look up the referenced item's GUID by numeric ID at serialize time). On deserialize: look up numeric ID by GUID in the target DB before writing.
- For fields that embed numeric IDs inside HTML/text content (e.g., inline links): flag these as a known limitation in v1 documentation. Do not attempt to rewrite embedded references in v1 — that is a v2 concern.
- Test: serialize from instance A, deserialize into instance B, verify that cross-references resolve to the correct items in B (not the same numeric IDs as A).

**Warning signs:**
- Navigation or links within deserialized content pointing to wrong pages
- Paragraph "item type" field values containing numeric IDs that differ between environments
- Any field value in serialized YAML that looks like a raw integer and matches a DynamicWeb page/paragraph ID pattern

**Phase to address:**
ID strategy design phase, before serialization code is written. The decision of which fields are "reference fields" must be documented as a design artifact, not discovered during testing.

---

### Pitfall 4: Partial Deserialization Leaving the Target in a Broken State

**What goes wrong:**
A deserialization run fails partway through (exception, permission error, DB constraint, disk full). The target database is now in an intermediate state: some items written, some not. The next run either skips items it thinks already exist (if it checks GUID presence), or creates duplicates (if it always inserts). Either way, the target content tree is corrupted and the failure may not be obvious.

**Why it happens:**
The "source wins" conflict strategy is straightforward for a full success case. It provides no guidance for partial failure. Without wrapping the entire deserialization in a database transaction, there is no atomicity guarantee.

**How to avoid:**
- Wrap the entire deserialization write pass in a single database transaction. On any exception, roll back all writes for that run. Log the rollback clearly.
- If DynamicWeb's APIs do not support explicit transaction wrapping across all content types, document this limitation explicitly and implement compensating logic: detect partial runs by checking a "run start" marker, and warn operators to manually verify/rollback before re-running.
- Log every item written with its GUID and numeric ID, so that a failed run can be diagnosed.

**Warning signs:**
- Deserialization reports "X items processed" but content tree in target is incomplete
- Second deserialization run behaves differently from first (suggests state was partially mutated)
- Pages exist in DB without their child paragraphs after a failed run

**Phase to address:**
Deserialization error handling phase. Atomicity design must be decided before implementing the write loop — it cannot be bolted on after.

---

### Pitfall 5: File Path Length Overflow on Windows

**What goes wrong:**
The mirror-tree file layout maps content hierarchy directly to directory depth. A DynamicWeb site with deeply nested pages (Area > Page > Sub-page > Sub-sub-page × N levels) combined with long page names quickly exceeds Windows MAX_PATH (260 characters including drive letter, separators, and `.yml` extension). File creation silently fails or throws a `PathTooLongException`, leaving items not serialized without obvious error.

**Why it happens:**
Windows MAX_PATH is 260 characters by default. A realistic path like `C:\Projects\Solutions\swift.test\data\content\CustomerCenter\Products\Electronics\Televisions\Smart-Televisions\4K-OLED\Page-Name.yml` is already 130+ characters and that is a shallow example. DynamicWeb page names can be long (SEO titles, descriptive names).

**How to avoid:**
- Enable long path support in the app: set `<LongPathAware>true</LongPathAware>` in the app manifest and target .NET 8 (which supports long paths with `EnableWindowsLongPaths` in `runtimeconfig.json`).
- At serialization time, compute the full output path before attempting file write. If path length > 240 characters, truncate the slug and append a short hash of the full path to maintain uniqueness. Log the truncation.
- Test with page names containing 60-80 characters at 6+ depth levels.
- Alternatively, adopt Unicorn's approach: use a GUID-named folder as an escape hatch when path would overflow.

**Warning signs:**
- `PathTooLongException` in logs
- Items present in source content tree but missing from serialized output with no logged error
- File count in output directory does not match item count in source DB

**Phase to address:**
File system output design phase (initial serialization implementation). Path length handling must be in the initial `FilePathStrategy` component, not added as a fix later.

---

### Pitfall 6: Sibling Ordering Lost or Non-Deterministic

**What goes wrong:**
DynamicWeb pages and paragraphs have a sort order (typically a numeric `SortOrder` column). If this order is not serialized and restored correctly, deserialized content appears in a different order on the target site. Additionally, if the serializer traverses children in non-deterministic order (e.g., DB query without `ORDER BY`), serialized YAML files change on every full serialize run even when content has not changed — polluting git history with meaningless diffs.

**Why it happens:**
Two separate issues: (1) forgetting to serialize the sort order field, and (2) not enforcing deterministic ordering of items during serialization traversal. Developers often test with small content trees where DB returns items in consistent order, masking the non-determinism problem.

**How to avoid:**
- Always include `SortOrder` (or DynamicWeb's equivalent sort field) in the serialized YAML for every item type (pages, paragraphs, grid rows, columns).
- On deserialization, apply sort order after writing all items, or write with the correct sort value from the YAML.
- Enforce deterministic query ordering on all DB reads during serialization (always `ORDER BY SortOrder ASC` or equivalent). This ensures identical content produces identical YAML on every run.
- Verify: serialize twice without changing content, run `git diff` — expect zero changes.

**Warning signs:**
- Content navigation menus appear in different order on target than source
- Git diff after re-serializing with no content changes shows YAML files with reordered items
- Page children appear in DB query order (often insertion order) rather than intended order

**Phase to address:**
Serialization implementation phase. Both the sort field inclusion and the deterministic query ordering must be addressed in the initial serialization implementation.

---

### Pitfall 7: Predicate Configuration Silently Excludes Required Items

**What goes wrong:**
The predicate system (include/exclude rules defining which content trees to sync) misconfigures in ways that silently exclude items the developer intends to include. The serialization appears to succeed, the YAML files are created, but a required sub-tree was excluded by an overly broad exclusion rule. On deserialization, that content is simply absent, and the source-wins strategy does not restore it (because no YAML exists for it).

**Why it happens:**
Predicate matching logic has edge cases: path prefix matching that is case-sensitive when the CMS is case-insensitive, trailing slash differences, ordering of include/exclude rules (last rule wins vs. first rule wins ambiguity). Developers assume "include /CustomerCenter" also includes all descendants, but exclude rules lower in the config may silently trim sub-trees.

**How to avoid:**
- After any predicate configuration change, run serialization and immediately count the output files. Compare against the known item count in the source DB for that content tree.
- Log every item that is evaluated against predicates and whether it was included or excluded. In debug mode, emit a list of excluded items.
- Define and document the predicate evaluation model explicitly: does an exclude rule override a parent include? Does include-before-exclude or exclude-before-include win?
- Test predicates with a content tree where you know the exact item count.

**Warning signs:**
- Serialized file count lower than expected but no errors in log
- Specific sub-pages missing from output without obvious reason
- Predicate config changes that do not change the output file count (suggests the config is being parsed incorrectly)

**Phase to address:**
Predicate/configuration system phase. Build item-count verification tooling alongside predicate implementation.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Serialize numeric IDs directly for all fields | Simpler code, no GUID lookup required | Cross-environment references are broken; content links point to wrong items | Never — GUID mapping for reference fields must be in v1 |
| Skip database transaction wrapping on deserialization | Fewer API complications | Partial failures leave DB in corrupted state with no recovery path | Never for full deserialization; acceptable for single-item debug writes |
| Enumerate files in OS-default order | Zero effort | Non-deterministic insertion; parent-after-child FK violations on fresh target | Never — always enforce parent-first ordering |
| Use default YamlDotNet scalar styles | Zero configuration | Round-trip data loss for HTML, tildes, CRLFs — silent corruption of content | Never — configure scalar styles before any serialization code ships |
| Skip sort order field in YAML | Simpler schema | Content order wrong on target; re-serialization produces non-deterministic diffs | Never — sort order is fundamental to content correctness |
| Hardcode base output path without length checks | Simpler file writing | Crashes or silent skips on deep content trees with long page names | Never for production; acceptable in initial prototype if documented |

---

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| DynamicWeb Page API | Reading `PageId` and using it as the serialized identity | Use `PageUniqueId` (GUID) as canonical ID; `PageId` is environment-specific |
| DynamicWeb Paragraph API | Assuming all paragraph types have the same fields | Paragraph field shape varies by `ParagraphType`/module; deserialize must handle missing/extra fields gracefully |
| YamlDotNet serializer | Using default builder with no scalar style configuration | Configure `ScalarStyle.Literal` for multiline, `ScalarStyle.DoubleQuoted` for all other strings before using in production |
| DynamicWeb Scheduled Task host | Assuming write access to arbitrary file system paths | The app pool identity running DynamicWeb may not have write access to paths outside the web root; verify permissions in both test environments before designing output path |
| DynamicWeb database (SQL Server) | Performing raw SQL inserts without understanding which columns have defaults or triggers | Use DynamicWeb's own save APIs where available; direct SQL inserts bypass business logic and may miss required field population |
| File system (Windows) | Creating output directories at startup without verifying available disk space | Content trees can be large; verify target directory is writable and has sufficient space before starting a full serialization run |

---

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Loading entire content tree into memory before writing | High memory usage; potential OOM on large sites | Process in batches by subtree; stream writes where possible | Sites with 10,000+ content items or paragraphs with large HTML fields |
| N+1 DB queries during serialization (fetching each paragraph's children one by one) | Serialization takes minutes instead of seconds | Batch-load children by parent ID; use `WHERE PageId IN (...)` rather than per-item queries | Any content tree with more than ~500 items |
| Reading all files from disk into memory during deserialization before processing | Memory spikes proportional to total YAML file size | Stream-parse files and process them as they are loaded; only retain the current insertion queue in memory | Large serialized trees (100MB+ of YAML) |
| No progress logging during long scheduled task runs | Task appears hung; no way to monitor progress | Log every N items processed (e.g., every 100 items) with elapsed time and item count | Any run exceeding ~1 minute |

---

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Writing serialized YAML to a web-accessible path (e.g., inside wwwroot) | Content data (potentially including internal field values, metadata) exposed publicly via HTTP | Write YAML files to a path outside the web root; or add a web.config/IIS rule blocking `.yml` files from being served |
| No input validation on config file paths | A misconfigured `OutputPath` in the config file could cause writes to arbitrary filesystem locations | Validate that the configured output path is within an allowed base directory before any file writes |
| Logging full field values at DEBUG level | Rich text fields may contain PII or sensitive content in some DynamicWeb deployments | Log item GUIDs and counts at DEBUG, never full field content; use TRACE level only if explicitly opted in |

---

## "Looks Done But Isn't" Checklist

- [ ] **Serialization completeness:** "It serialized the test page" — verify the entire sub-tree (grids, rows, columns, paragraphs) was included, not just the top-level page object
- [ ] **Round-trip fidelity:** "YAML files look right" — run an automated round-trip test (serialize → deserialize → compare DB values byte-for-byte) before declaring serialization done
- [ ] **Sort order preservation:** "Content appears on target" — verify it appears in the correct order, not just present; check navigation menus and paragraph ordering
- [ ] **Cross-reference integrity:** "Pages deserialized successfully" — verify that any inter-page references (links, paragraph datasources) resolve to the correct items on target, not just that they are non-null
- [ ] **Deterministic output:** "Serialization works" — serialize twice without changing content, run `git diff`, confirm zero changes
- [ ] **Fresh-environment deserialization:** "Deserialization works on our test instance" — test on a completely empty DynamicWeb database, not just the pre-populated test instance B; the partial-population case hides ordering bugs
- [ ] **Long-path handling:** "File output works" — test with page names containing 60+ characters at 5+ directory depth levels
- [ ] **Scheduled task permissions:** "Task runs in DynamicWeb admin" — verify the task can write files to the configured output path when invoked by DynamicWeb's scheduler, not just when run under developer credentials
- [ ] **Error visibility:** "Task completed" — verify that a deserialization failure mid-run produces a clear error in DynamicWeb logs, not a silent success with partial content

---

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Children inserted before parents (FK violations) | MEDIUM | Truncate affected tables in target DB and re-run full deserialization with corrected ordering logic |
| YAML round-trip data loss (wrong scalar style) | HIGH | All YAML files must be re-serialized with corrected configuration; if files were already committed to source control, all previously serialized content is suspect |
| Numeric ID leakage in reference fields | HIGH | Re-serialize from scratch with correct GUID-based reference handling; manually fix any broken links in target DB; requires audit of all reference field types |
| Partial deserialization failure | MEDIUM | Identify last successfully written item from logs; truncate target DB to known-good state (or restore DB backup); re-run full deserialization |
| Path length overflow (items not serialized) | LOW | Enable long path support, re-run serialization, commit newly created YAML files for previously-missing deep items |
| Sibling ordering lost | MEDIUM | Re-serialize with sort order included; re-deserialize; manually verify content ordering on target site |
| Predicate excludes required items | LOW | Correct predicate config, re-serialize, verify file count matches expected item count |

---

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Children before parents (insert order) | Deserialization core implementation | Automated test: deserialize into empty DB, verify no FK errors, verify all items present |
| YAML round-trip data loss | YAML serialization foundation (first implemented) | Automated round-trip test with known-tricky strings before any other serialization code |
| Numeric ID leakage in reference fields | ID strategy design (before any serialization code) | Serialize from A, deserialize into B, verify cross-references resolve to correct GUIDs |
| Partial deserialization leaving broken state | Deserialization error handling | Simulate mid-run failure (kill process), verify DB rollback, verify re-run produces correct state |
| File path length overflow | File system output design (initial serialization) | Test with 6+ directory depth and 60+ character page names |
| Sibling ordering non-deterministic | Serialization implementation | Serialize twice without content changes, `git diff` produces zero changes |
| Predicate silently excluding items | Predicate/configuration system | Item count in YAML files matches item count in source DB for configured trees |

---

## Sources

- [Sitecore Unicorn GitHub — child ordering and GUID disambiguation issues](https://github.com/SitecoreUnicorn/Unicorn/issues/145)
- [Sitecore Unicorn GitHub — same-name duplicate ID errors](https://github.com/SitecoreUnicorn/Unicorn/issues/43)
- [Sitecore Unicorn GitHub — not syncing all fields on first run](https://github.com/SitecoreUnicorn/Unicorn/issues/283)
- [Sitecore Content Serialization Deployment Failures — Arroact](https://www.arroact.com/blogs/sitecore-content-serialization-deployments/)
- [YamlDotNet special character serialization issues — GitHub Issue #846](https://github.com/aaubry/YamlDotNet/issues/846)
- [YamlDotNet multiline ScalarStyle.Literal — GitHub Issue #391](https://github.com/aaubry/YamlDotNet/issues/391)
- [YamlDotNet deserializing strings with newlines adds extra lines — GitHub Issue #361](https://github.com/aaubry/YamlDotNet/issues/361)
- [YamlDotNet numeric string encoding pitfall — GitHub Issue #934](https://github.com/aaubry/YamlDotNet/issues/934)
- [Windows MAX_PATH file path limitations — Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation)
- [FK constraint: insert child before parent — Microsoft Q&A](https://learn.microsoft.com/en-us/answers/questions/706411/the-insert-statement-conflicted-with-the-foreign-k)
- [Bloomreach: same-name sibling index cross-environment issues (ID mapping)](https://xmdocumentation.bloomreach.com/library/concepts/configuration-management/yaml-format.html)
- [Michael West: Unicorn + Sitecore CLI YAML field compatibility issues](https://michaellwest.blogspot.com/2023/01/working-with-unicorn-and-sitecore-cli.html)
- [Sitecore 10 content serialization best practices](https://www.aceik.com.au/insights/sitecore-10-content-serialisation-best-practices-part-1/)

---
*Pitfalls research for: DynamicWeb content serialization / Dynamicweb.ContentSync*
*Researched: 2026-03-19*
