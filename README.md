# Truvio.Commerce.Serializer

**Git-versioned content and data sync for Truvio Commerce 10.**

> Truvio Commerce is the platform formerly known as Dynamicweb. The platform's
> NuGet packages, API namespaces, and host binaries still carry the `Dynamicweb`
> name (e.g. `Dynamicweb 10.23.9`, `Dynamicweb.Host.Suite`), and this project's
> docs use "DW" as shorthand for the platform throughout.

Truvio.Commerce.Serializer is a Truvio Commerce AppStore app that serializes and deserializes
database state to and from YAML files on disk. Teams treat YAML as the single source
of truth for content, shop configuration, payment and shipping definitions, VAT rules,
and URL routing — committing diffs to Git, reviewing changes in pull requests, and
applying them across dev, test, QA, and production through ordinary CI/CD.

Identity is GUID-based, so pages survive environments where numeric IDs differ.
Cross-environment `Default.aspx?ID=N` references are rewritten automatically on
deserialize. A `strictMode` switch escalates recoverable warnings to hard failures
so CI/CD pipelines fail loud on content drift, schema drift, or missing templates.

## Why it exists

Hand-editing content across Truvio Commerce environments is slow, error-prone, and leaves
no audit trail. Staging drifts from production. Nobody remembers who changed the VAT
rates last March. Rolling a bad content change back means restoring a whole database.

Truvio.Commerce.Serializer fixes that by treating the database state a DW instance depends
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

### From a release (no build, no source environment)

Every [GitHub release](https://github.com/justdynamics/Truvio.Commerce.Serializer/releases)
ships two assets so you can start a Swift solution **from YAML alone**:

- `Truvio.Commerce.Serializer.<version>.nupkg` — the app (also in the DW10 app store
  under *Available apps*).
- `Truvio.Commerce.Serializer-SwiftYaml-<version>.zip` — the verified Swift 2.2 content
  baseline: deploy YAML (site structure + framework tables) and seed YAML (starter blog
  posts + catalog), plus the starter `Serializer.config.json`.

```text
1. Install the app on a DW10 host that has the Swift Files folder (templates/designs).
2. Unzip:  Serializer.config.json  ->  wwwroot/Files/
           SerializeRoot/          ->  wwwroot/Files/System/Serializer/SerializeRoot/
3. On a blank database, run deploy then seed:
   POST /Admin/Api/SerializerDeserialize?mode=deploy
   POST /Admin/Api/SerializerDeserialize?mode=seed
4. Commit the YAML to your repo and manage deploy/seed content in your git flow
   from day one: serialize where you author, deserialize everywhere else.
```

The zip's `INSTALL.txt` has the step-by-step details (including the one schema note for
databases that never saw a Swift import).

### From source

```bash
# 1. Build the DLL
dotnet build src/Truvio.Commerce.Serializer/ -c Release

# 2. Copy to your DW instance's bin/ directory
cp src/Truvio.Commerce.Serializer/bin/Release/net8.0/Truvio.Commerce.Serializer.dll \
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

### Start from the Swift starter configuration

For a Swift site, copy
[`src/Truvio.Commerce.Serializer/Configuration/swift-starter.json`](src/Truvio.Commerce.Serializer/Configuration/swift-starter.json)
to `Files/Serializer.config.json`. It encodes the recommended split:

- **Deploy** — `Site structure and design` (the whole site **excluding** `/Posts`) plus the
  commerce framework tables (countries, currencies, languages, VAT, shops, payments,
  shippings, order flow, URL paths). Identical on every environment; re-deploys overwrite.
- **Seed** — `Starter blog posts` (`/Posts`) plus starter catalog data (groups, products,
  variants, discounts). Lands once; afterwards the receiving environment owns it — re-deploys
  only fill fields that are still empty.
- **Everything else** (orders, users, logs) is environment data and is never serialized.

### Reading the content tree

The admin content tree shows per-page coverage so you can see what deploys without opening
the config:

| Icon | Meaning |
|---|---|
| sync | Fully managed at deploy — tooltip names the predicate |
| sync-slash | Partially managed — tooltip lists the excluded paths below this page, or the deploy-managed subtrees under an unmanaged page |
| flower | Seeded starter content — lands once, local edits on the target are preserved |
| *(none)* | Not serialized (environment-owned) |

Every page also gets right-click **Serialize subtree** (zip download) and
**Deserialize from zip** (upload into this website) actions.

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
- Truvio Commerce 10.23.9 or newer
- SQL Server (via the Truvio Commerce data layer)
- YamlDotNet 13.7.1

## Project status

Shared with partners as open source — usable today, not a fully productized
offering. You are expected to build the DLL yourself, validate against your own
solution, and read the docs; there is no SLA or formal support channel. Issues
and PRs are welcome.

Milestone history: v0.6.0 **Manifest-Driven Deserialize** completed 2026-05-11
(manifest-driven entry dispatch, zip import/export convergence). v0.5.0
**Production-Ready Baseline** closed with a full Swift 2.2 → CleanDB round-trip
passing under `strictMode: true` end-to-end via `tools/e2e/full-clean-roundtrip.ps1`.
Test suite: 851 unit tests, plus integration tests that require a live DW host.

The API surface (Management API commands, predicate shape, YAML format) is stable
for the current release line. Config schema and runtime-exclusion defaults may
evolve before 1.0.

## Links

- Source: <https://github.com/justdynamics/Truvio.Commerce.Serializer>
- Issue tracker: <https://github.com/justdynamics/Truvio.Commerce.Serializer/issues>
- License: [MIT](LICENSE)
