# DynamicWeb.Serializer

**Git-versioned content and data sync for DynamicWeb 10.**

DynamicWeb.Serializer is a DynamicWeb AppStore app that serializes and deserializes
database state to and from YAML files on disk. Teams treat YAML as the single source
of truth for content, shop configuration, payment and shipping definitions, VAT rules,
and URL routing — committing diffs to Git, reviewing changes in pull requests, and
applying them across dev, test, QA, and production through ordinary CI/CD.

Identity is GUID-based, so pages survive environments where numeric IDs differ.
Cross-environment `Default.aspx?ID=N` references are rewritten automatically on
deserialize. A `strictMode` switch escalates recoverable warnings to hard failures
so CI/CD pipelines fail loud on content drift, schema drift, or missing templates.

## Why it exists

Hand-editing content across DynamicWeb environments is slow, error-prone, and leaves
no audit trail. Staging drifts from production. Nobody remembers who changed the VAT
rates last March. Rolling a bad content change back means restoring a whole database.

DynamicWeb.Serializer fixes that by treating the database state a DW instance depends
on — shop structure, payment definitions, item types, pages, permissions, navigation
— as code. Git becomes the audit log. Pull requests become the review step. Rollback
becomes `git revert` followed by a redeploy.

## Features

- **Predicate-based selective sync.** Content predicates pick subtrees of pages,
  grids, and paragraphs. SqlTable predicates pick arbitrary tables with optional
  `WHERE` clauses. Exclude rules, per-item-type field exclusions, and embedded-XML
  element filters keep per-environment noise out of the baseline.
- **GUID identity, not numeric IDs.** `PageUniqueId` matches source and target.
  Numeric `PageID` is resolved per environment at deserialize time.
- **Cross-environment link rewriting.** `Default.aspx?ID=N`, paragraph anchors,
  and `ButtonEditor` `SelectedValue` JSON are rewritten source → target on content
  deserialize. SqlTable columns opt in via `resolveLinksInColumns`.
- **Deploy and Seed modes.** `Deploy` is source-wins (baseline overwrites target).
  `Seed` is destination-wins (skip rows whose natural key already exists on target)
  — safe for first-run customer content that must not get trampled by re-deploys.
- **Strict mode for CI/CD.** Recoverable warnings (unresolvable links, missing
  templates, schema drift, FK orphans, cache invalidation failures) accumulate and
  throw one `CumulativeStrictModeException` at end-of-run. HTTP 4xx on the API.
  Default: `on` for API/CLI callers, `off` for admin UI (interactive exploration).
- **SQL identifier whitelisting.** Predicate `table`, `nameColumn`, `excludeFields`,
  `includeFields`, and `where` clauses are validated against `INFORMATION_SCHEMA`
  before any SQL runs. `;`, `--`, `/*`, `xp_`, `DROP`, `EXEC`, and related tokens
  are rejected at config-load.
- **Admin UI + Management API.** Configure predicates, item types, and XML filters
  from `Settings > Database > Serialize`. Run `SerializerSerialize` and
  `SerializerDeserialize` from CI/CD using the DW Management API.

## Quick start

```bash
# 1. Build the DLL
dotnet build src/DynamicWeb.Serializer/ -c Release

# 2. Copy to your DW instance's bin/ directory
cp src/DynamicWeb.Serializer/bin/Release/net8.0/DynamicWeb.Serializer.dll \
   /path/to/your/dw-instance/bin/

# 3. Restart the DW host, then sign in and go to
#    Settings > Database > Serialize > Predicates to configure what to sync.
#    Or edit Files/Serializer.config.json directly.

# 4. Serialize on the source environment
curl -X POST https://source.example.com/Admin/Api/SerializerSerialize \
  -H "Authorization: Bearer CLD.your-api-key"

# 5. Commit baselines/ to Git, deploy the YAML to the target, then deserialize
curl -X POST https://target.example.com/Admin/Api/SerializerDeserialize \
  -H "Authorization: Bearer CLD.your-api-key"
```

Full walkthrough: [`docs/getting-started.md`](docs/getting-started.md).

## How it works

```
    Source environment                          Target environment
    (e.g. dev, QA)                              (e.g. production)

      DW database                                 DW database
            |                                          ^
            | 1. POST SerializerSerialize              | 5. POST SerializerDeserialize
            v                                          |
    Files/System/Serializer/                    Files/System/Serializer/
      SerializeRoot/                              SerializeRoot/
        deploy/                 2. git add  .       deploy/
        seed/       ----------> 3. git push  ---->  seed/
                                4. deploy pipeline
                                   copies YAML into
                                   target's Files volume
```

Predicates (configured per-mode) select what gets serialized. The Deploy mode is
for data the developer owns (shop definitions, VAT groups, item types). The Seed
mode is for data the customer owns after first run (pages, product catalog). Both
modes sit in the same config and run through the same pipeline — they differ only
in conflict strategy and output subfolder.

## Documentation map

| Topic | Page |
|-------|------|
| Install, first serialize, first deserialize | [Getting started](docs/getting-started.md) |
| Mental model: predicates, GUID identity, folder layout | [Concepts](docs/concepts.md) |
| Every config key and admin UI screen | [Configuration](docs/configuration.md) |
| GitHub Actions, Azure DevOps, GitLab CI end-to-end | [CI/CD integration](docs/cicd.md) |
| Strict mode: what escalates, defaults, overrides | [Strict mode](docs/strict-mode.md) |
| Cross-environment `Default.aspx?ID=N` rewriting | [Link resolution](docs/link-resolution.md) |
| Role and group permission handling | [Permissions](docs/permissions.md) |
| `SqlTable` predicates, WHERE clauses, field filters | [SQL tables](docs/sql-tables.md) |
| Auto-excluded runtime columns and credential caveats | [Runtime exclusions](docs/runtime-exclusions.md) |
| Common errors and remedies | [Troubleshooting](docs/troubleshooting.md) |

Reference material also lives in [`docs/baselines/`](docs/baselines) (the Swift 2.2
reference baseline and the per-environment config bucket) and [`docs/findings/`](docs/findings)
(operational findings from baseline round-trip runs).

## CI/CD teaser

The intended flow is: serialize on source, commit YAML, deploy, deserialize on target.
A minimal GitHub Actions job that applies a baseline on deploy:

```yaml
- name: Apply baseline to target
  env:
    DW_HOST: ${{ secrets.DW_HOST }}
    DW_API_KEY: ${{ secrets.DW_API_KEY }}
  run: |
    # Deploy has already copied YAML into the target's Files volume.
    # Strict mode makes any warning (unresolvable link, missing template,
    # schema drift) fail the job.
    curl -f -X POST "$DW_HOST/Admin/Api/SerializerDeserialize?mode=deploy&strictMode=true" \
      -H "Authorization: Bearer $DW_API_KEY"
```

Complete pipelines for GitHub Actions, Azure DevOps, and GitLab CI — including
secret management, pre-commit link sweeps, and the Seed-vs-Deploy split — are in
[`docs/cicd.md`](docs/cicd.md).

## Supported environments

- .NET 8.0
- DynamicWeb 10.23.9 or newer
- SQL Server (via the DynamicWeb data layer)
- YamlDotNet 13.7.1

## Project status

Active development. Current milestone: **v0.4.0 Full Page Fidelity** (nearing
completion). Phase 38.1 closed 2026-04-22 with a full Swift 2.2 → CleanDB round-trip
passing under `strictMode: true` end-to-end via `tools/e2e/full-clean-roundtrip.ps1`.
Phase 41 closed 2026-05-01 with admin-UI polish across the Predicate, Item Type
Excludes, and Embedded XML Excludes screens (per-predicate Mode binding, dual-list
saved-exclusion merge, screen-name and tooltip cleanup). Test suite: 821 unit and
integration tests.

The API surface (Management API commands, predicate shape, YAML format) is stable
for the current release line. Config schema and runtime-exclusion defaults may
evolve before 1.0.

## Links

- Source: <https://github.com/justdynamics/DynamicWeb.Serializer>
- Issue tracker: <https://github.com/justdynamics/DynamicWeb.Serializer/issues>
- License: open source, no licensing required
