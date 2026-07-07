# Glossary

The serializer's vocabulary in one place. Every cue in the admin UI uses these terms.

| Term | Meaning |
|---|---|
| **Baseline** | The YAML tree on disk (`Files/System/Serializer/SerializeRoot/`) describing the desired state of the managed data. Committed to Git; the unit you review in a PR. |
| **Serialize** | Read the database, write the baseline. Run on the **source** environment. |
| **Deserialize** | Read the baseline, write the database. Run on the **target** environment. An upsert, never a mirror — nothing is ever deleted. |
| **Predicate** | One rule in `Serializer.config.json` saying *what* to sync: a content subtree (root path + area) or a SQL table, each with its own mode. Only data matching a predicate participates. |
| **Replace (mode)** | Source wins. The baseline overwrites the target on every deserialize — for platform-wired data that must be identical everywhere (checkout pages, payment methods, countries, currencies). |
| **Merge (mode)** | Destination wins. Lands once; afterwards only fields the target left empty are filled — for starter content the customer owns from then on (homepage, blog, catalog). |
| **Managed** | Covered by a predicate. The tree shows a sync icon; editing screens warn that a replace run overwrites edits (or that a merge run preserves them). |
| **Partially managed** | Managed, but with carve-outs: excluded subtrees below the page, or fields/settings on the page that stay local (sync-slash icon). |
| **Carve-out / exclusion** | A field, XML element, column, path or row exempted from sync. Lives at several levels (predicate excludes, per-type dictionaries, runtime registry) — see [configuration](configuration.md) and the "Stays local" panels in the admin UI. |
| **Stays local** | The carve-out verdict from this environment's point of view: a replace run never overwrites the value, a merge run never fills it. Each environment keeps its own (API keys, mail recipients, tracking ids). |
| **Dry run** | The full deserialize pipeline without writing: reports what *would* be created/updated/skipped, with per-field `[DRY-RUN]` detail in the log. |
| **Drift** | A replace-managed page edited on this environment after the last replace run landed — the next replace run will overwrite those edits. Flagged on the tree tooltip and editing-screen alert. |
| **Manifest** | The envelope written next to the YAML describing what a serialize run produced (entries, exclusion maps); the deserialize side reads it to know what to apply. |
| **Orphan (acknowledged)** | A page-id reference in source content pointing at a page that is not serialized. Acknowledged ids are logged instead of failing the run. |
| **Distribution** | The companion repo of ready-made content — [justdynamics/Truvio.Commerce.Distribution](https://github.com/justdynamics/Truvio.Commerce.Distribution). Everything needed to make a DW10 host run as Truvio Commerce, consumed by git clone. |
| **Layer / Edition** | A **layer** is one versioned unit in the Distribution: serialized mode trees (source-wins / field-level) plus an optional disk overlay. An **edition** is a gate-proven composition of a base layer plus additions. Consumed by git clone and pinned by annotated tag (`layers/<name>/<semver>`, `editions/<name>/<semver>`). |

## See also

- [Concepts](concepts.md) — the architecture behind these terms
- [Getting started](getting-started.md) — first round-trip
- [Swift replace/merge analysis](swift-replace-merge-analysis.md) — how the modes are assigned in practice
