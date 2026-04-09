# Phase 32: Config Schema Extension - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-09
**Phase:** 32-config-schema-extension
**Areas discussed:** Config placement, Merge semantics, Config save/load

---

## Config Placement

| Option | Description | Selected |
|--------|-------------|----------|
| Top-level global (Recommended) | Add excludeFieldsByItemType and excludeXmlElementsByType as top-level properties on SerializerConfiguration, alongside outputDirectory and predicates. Item type exclusions apply globally across all predicates. | ✓ |
| Per-predicate dictionaries | Add the dictionaries as new properties on each ProviderPredicateDefinition alongside existing flat arrays. Different predicates can have different item-type exclusions. | |

**User's choice:** Top-level global (Recommended)
**Notes:** Item type exclusions are a system-wide concern, not per-predicate.

---

## Merge Semantics

| Option | Description | Selected |
|--------|-------------|----------|
| Union — both apply (Recommended) | Flat per-predicate exclusions apply to ALL fields/elements on that predicate. Global typed dictionaries apply only to matching item types/XML types. Final exclusion set = union of both. No conflict possible since they target different scopes. | ✓ |
| Typed replaces flat | If a typed dictionary entry exists for an item type, it fully replaces the flat array for that type. Flat array only applies to types NOT in the dictionary. | |
| You decide | Claude picks the best merge strategy based on code analysis. | |

**User's choice:** Union — both apply (Recommended)
**Notes:** Flat arrays are broad (all types), typed dictionaries are narrow (specific types). Both additive.

---

## Config Save/Load

| Option | Description | Selected |
|--------|-------------|----------|
| Same path (Recommended) | Add the new dictionary properties to SerializerConfiguration and RawSerializerConfiguration. ConfigWriter.Save() and ConfigLoader.Load() handle them automatically via System.Text.Json serialization. UI screens load config, mutate the dictionaries, save back. | ✓ |
| Separate helper methods | Add targeted methods like ConfigWriter.UpdateItemTypeExclusions() that do JSON merge-patch on just the dictionary keys, avoiding full config rewrite. Safer for concurrent edits but more complex. | |
| You decide | Claude picks based on existing patterns and complexity. | |

**User's choice:** Same path (Recommended)
**Notes:** Keeps it simple, follows existing patterns.

---

## Claude's Discretion

- Test coverage approach for backward compatibility (CFG-02)
- Internal implementation of the merge logic

## Deferred Ideas

None — discussion stayed within phase scope
