# Configuration

The full reference for `Serializer.config.json` and the admin UI screens that
edit it. Use this page as a lookup when you're composing a new baseline or
debugging a config-load failure.

## Table of contents

- [Where the config lives](#where-the-config-lives)
- [Top-level config schema](#top-level-config-schema)
- [Per-predicate mode](#per-predicate-mode)
- [Content predicate fields](#content-predicate-fields)
- [SqlTable predicate fields](#sqltable-predicate-fields)
- [Global exclusion maps](#global-exclusion-maps)
- [Admin UI screens](#admin-ui-screens)
- [Full config example](#full-config-example)
- [Config validation at load time](#config-validation-at-load-time)

## Where the config lives

The canonical config path is:

```
{DW_host}/Files/System/Serializer/Serializer.config.json
```

The config lives inside the serializer folder so the folder travels as one
unit — config plus YAML, Upload and Download. Copy it between environments, or
upload an example config (such as the Swift starter) into the folder through
the file manager to start from it.

The location is convention-fixed relative to the Files root. It is never
derived from the config's own `outputDirectory` value — that would be circular
(the file would define where to find itself). `outputDirectory` only governs
where the data subfolders are created.

The admin UI at `Settings > Developer > Serialize` reads and writes this file.
Manual edits are picked up on the next screen load (no restart required). The
Management API commands also read the same file on each call.

## Top-level config schema

The config is a single flat `predicates: [...]` list where each predicate carries its own
`mode`. Section-level `deploy: { ... }` / `seed: { ... }` keys are rejected by ConfigLoader
with a clear actionable error.

```json
{
  "outputDirectory": "Serializer",
  "deployOutputSubfolder": "deploy",
  "seedOutputSubfolder": "seed",
  "showSeedIndicators": false,
  "showDeployIndicators": true,
  "excludeFieldsByItemType": {
    "Swift_Content": ["SystemName_Internal"]
  },
  "excludeXmlElementsByType": {
    "eCom_CartV2": ["Mail1Recipient", "DefaultPaymentId"]
  },
  "predicates": [
    { "name": "...", "mode": "Deploy", "providerType": "Content", "areaId": 3, "path": "/" },
    { "name": "...", "mode": "Seed", "providerType": "SqlTable", "table": "EcomGroups" }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `outputDirectory` | string (required) | Top-level folder relative to `Files/System`. Subfolders `SerializeRoot/`, `Upload/`, `Download/`, `Log/` are created automatically. |
| `deployOutputSubfolder` | string | Subfolder under `SerializeRoot/` for Deploy-mode YAML output. Default: `deploy`. Validated against a safe-name regex to prevent path traversal. |
| `seedOutputSubfolder` | string | Subfolder under `SerializeRoot/` for Seed-mode YAML output. Default: `seed`. Same regex check. |
| `showSeedIndicators` | boolean | Show seed cues in the admin UI: the flower icon on content-tree pages covered by a seed predicate and the seed info message on content editing screens. Default: `false` — with broad seed coverage these would appear nearly everywhere and drown out the deploy warnings. |
| `showDeployIndicators` | boolean | Show deploy cues in the admin UI: the sync icon on content-tree pages covered by a deploy predicate, the deploy warning on content editing screens, and the deploy warning on commerce settings screens (payment methods, currencies, …) backed by a deploy-managed SqlTable predicate. Default: `true` — these warn editors that changes are overwritten by the next deploy. Switch off where the warnings are noise, e.g. on the source environment itself. |
| `excludeFieldsByItemType` | map | Global per-item-type field exclusions, applied to every predicate regardless of mode. Key: item-type system name. Value: list of field names to strip. |
| `excludeXmlElementsByType` | map | Global per-XML-type element exclusions, applied to every predicate regardless of mode. Key: XML type name (paragraph module system name or URL provider type). Value: list of element names to strip. |
| `predicates` | list | The predicates serialized and deserialized. Each entry must carry its own `mode` (Deploy or Seed). The orchestrator filters on `predicate.Mode` when iterating per mode. |

## Per-predicate mode

Every predicate must declare a `mode` value of `Deploy` or `Seed` (case-insensitive on disk).

| Mode | Conflict strategy | When to use |
|------|-------------------|-------------|
| `Deploy` | source-wins (YAML overwrites target on every deploy) | Reference data and structural deployment items: countries, currencies, shop definitions, payment methods, page templates, item-type schemas. |
| `Seed` | destination-wins via field-level merge | One-time bootstrap content the customer is expected to edit: product catalog, marketing copy, FAQ body text, newsletter templates. The serializer fills fields the target has NOT set, preserving customer edits. |

The conflict strategy is hardcoded per mode and is not a config knob.
[`MergePredicate`](../src/Truvio.Commerce.Serializer/Serialization/MergePredicate.cs) and
[`XmlMergeHelper`](../src/Truvio.Commerce.Serializer/Serialization/XmlMergeHelper.cs) implement
the Seed-mode field-level merge.

```json
{
  "name": "EcomCountries",
  "mode": "Deploy",
  "providerType": "SqlTable",
  "table": "EcomCountries"
}
```

```json
{
  "name": "EcomProducts",
  "mode": "Seed",
  "providerType": "SqlTable",
  "table": "EcomProducts",
  "nameColumn": "ProductName"
}
```

## Content predicate fields

```json
{
  "name": "Content - Swift 2",
  "providerType": "Content",
  "areaId": 3,
  "path": "/Customer Center",
  "excludes": ["/Customer Center/Drafts"],
  "excludeFields": ["AreaDomain", "GoogleTagManagerID"],
  "excludeXmlElements": ["EmptyCartRedirectPage"],
  "excludeAreaColumns": ["AreaCdnHost", "AreaCookieWarningTemplate"],
  "acknowledgedOrphanPageIds": []
}
```

| Field | Type | Description |
|-------|------|-------------|
| `name` | string (required) | Unique human-readable name. Shows in logs and admin UI. |
| `providerType` | `"Content"` | Routes to `ContentProvider`. |
| `areaId` | int (required) | DW area ID containing the content tree. Must exist on source. |
| `path` | string | Root path for the predicate. `/` includes everything under the area. Case-insensitive. |
| `pageId` | int | Optional page ID hint for the content-tree picker in the admin UI. |
| `excludes` | list of strings | Paths to exclude. Case-insensitive, with path-boundary matching so `/Home` does not exclude `/HomePage`. |
| `excludeFields` | list of strings | Item-type field names to strip from serialization. Applies to all items touched by the predicate. |
| `excludeXmlElements` | list of strings | XML element names to strip from embedded XML columns. Useful for masking env-specific page-ID references inside item-type XML payloads. |
| `excludeAreaColumns` | list of strings | Columns on the `[Area]` SQL table to strip from area metadata. Populated by the admin UI from the live schema. |
| `acknowledgedOrphanPageIds` | list of ints | Page IDs whose unresolvable references are logged as warnings rather than fatal errors by `BaselineLinkSweeper`. Escape hatch for known-broken source data that can't be cleaned upstream in time. |

## SqlTable predicate fields

```json
{
  "name": "AccessUser-Roles",
  "providerType": "SqlTable",
  "table": "AccessUser",
  "nameColumn": "AccessUserUserName",
  "compareColumns": "AccessUserUserName,AccessUserType",
  "where": "AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors')",
  "excludeFields": ["AccessUserPassword", "AccessUserPasswordSalt"],
  "includeFields": [],
  "xmlColumns": [],
  "excludeXmlElements": [],
  "serviceCaches": ["Dynamicweb.Ecommerce.Users.UserService"],
  "resolveLinksInColumns": []
}
```

| Field | Type | Description |
|-------|------|-------------|
| `name` | string (required) | Unique human-readable name. |
| `providerType` | `"SqlTable"` | Routes to `SqlTableProvider`. |
| `table` | string (required) | SQL table name. Validated against `INFORMATION_SCHEMA.TABLES` at config-load. |
| `nameColumn` | string | Column used as the natural key for per-row file naming. If absent, the composite primary key is used. Validated against `INFORMATION_SCHEMA.COLUMNS`. |
| `compareColumns` | string | Comma-separated columns used for change detection. Rows whose `compareColumns` match on target are skipped. Empty: compare all non-identity columns. |
| `where` | string | Optional row filter applied at serialize time. Every identifier must match `INFORMATION_SCHEMA.COLUMNS` of `table`. Banned tokens (`;`, `--`, `/*`, `xp_`, `sp_executesql`) and DDL/DML keywords are rejected. See [`sql-tables.md`](sql-tables.md). |
| `excludeFields` | list of strings | Columns to strip from serialization. Validated against `INFORMATION_SCHEMA.COLUMNS`. |
| `includeFields` | list of strings | Columns to KEEP in output even if they would otherwise be auto-excluded by `RuntimeExcludes`. See [`runtime-exclusions.md`](runtime-exclusions.md). |
| `xmlColumns` | list of strings | Columns containing embedded XML. Pretty-printed in YAML output for readable diffs. |
| `excludeXmlElements` | list of strings | XML element names to strip from every `xmlColumns` column. |
| `serviceCaches` | list of strings | DW service cache types to clear after deserialization. Accepts short name (`CountryService`) or full type name (`Dynamicweb.Ecommerce.International.CountryService`). Validated at config-load against `DwCacheServiceRegistry`. |
| `resolveLinksInColumns` | list of strings | Columns whose `Default.aspx?ID=N` strings should be rewritten source → target at deserialize. Validated against `INFORMATION_SCHEMA.COLUMNS`. See [`link-resolution.md`](link-resolution.md). |
| `schemaSync` | string | Optional schema-sync directive. `EcomGroupFields` is the only recognized value; runs `EcomGroupFieldSchemaSync` before row writes. |

## Global exclusion maps

Two dictionaries live at the top level of the config and apply across every predicate
regardless of mode. These live at the top level because
the same exclusions almost always apply to both Deploy and Seed.

```json
{
  "excludeFieldsByItemType": {
    "Swift_Content": ["SystemName_Internal"],
    "Swift-v2_Button": ["DebugMarker"]
  },
  "excludeXmlElementsByType": {
    "ParagraphModule": ["cache"],
    "PageItem": ["EmptyCartRedirectPage", "ShoppingCartLink"]
  },
  "predicates": [
    { "name": "...", "mode": "Deploy", "providerType": "Content", "areaId": 3, "path": "/" }
  ]
}
```

Use these for cross-predicate cleanup. Per-predicate exclusions still work;
the effective exclude set is the union of the predicate's list and the global
dictionary entry for that item type.

These maps are visible in four places in the admin UI: the settings screen
shows a per-type inventory of everything excluded; the Item Type Excludes and
Embedded XML Excludes sub-nodes edit them; content pages carrying an affected
type show as **partially managed** in the content tree (sync-slash icon) with
the excluded types named in the tooltip and a right-click "View excluded
fields" action; and the editing screens add a clickable header chip per
carved-out type ("eCom_CartV2 — 21 settings stay local — view") next to the
verdict alert. Both click-throughs open a read-only **"Stays local"** panel
listing the exact excluded fields — visible to every backend user, no Settings
access needed; administrators additionally get a "Manage exclusions" shortcut
into the editor. The cart page is the canonical case: covered by the deploy
predicate, but its `eCom_CartV2` module settings (mail recipients, error
messages, default payment/shipping ids) stay local per environment.

## Admin UI screens

Navigation: `Settings > Developer > Serialize`.

| Node | Purpose |
|------|---------|
| **Serialize** | Top-level settings screen. Every top-level config value is visible here: output directory, deploy/seed subfolders and the seed-indicator toggle are editable; the config file location, sync history (last deploy/seed received), coverage counts, the two exclusion maps and the predicate list show as read-only summaries. Actions: serialize/deserialize per mode plus **Preview … (dry run)** — the full pipeline without writing, per-field `[DRY-RUN]` detail in the Log Viewer. With no predicates configured the actions are replaced by a **Get started** group (apply the embedded Swift starter to a chosen website, or create an empty configuration). Per-mode conflict strategy is hardcoded — Deploy=source-wins, Seed=destination-wins — and is not an admin-editable setting. |
| **Predicates** | CRUD for Content and SqlTable predicates. Each predicate carries its own `mode` field (Deploy or Seed) — pick the mode on the predicate edit screen. Fields match the JSON schema above with dual-list pickers populated from the live DB schema. |
| **Item Types** | Browse item types by category, edit global per-type field exclusions (mode-agnostic). |
| **Embedded XML** | Browse XML types, edit global per-type element exclusions (mode-agnostic). |
| **Log Viewer** | Per-run logs with summary headers, per-predicate counts, and `AdviceGenerator` remediation hints. |

The **"Serialize subtree"** action appears in the Actions menu on every page
edit screen. It ad-hoc serializes the current page and its descendants to a
zip file downloaded by the browser and copied to `Files/System/Serializer/Download/`.
The matching import is at `Files/System/Serializer/Upload/` — drop a zip there
and use the file's **"Import to database"** action.

The commerce settings edit screens — payment, shipping, country, currency,
ecommerce language, shop, order flow and order state — show the same
deploy/seed alert as the content editors when a SqlTable predicate manages
their table. A predicate with exclusions adds a clickable "Stays local" header
chip that opens the predicate editor.

## Full config example

The Swift 2.2 reference baseline — a working config with one Content predicate
and seventeen SqlTable predicates. Lives at
`src/Truvio.Commerce.Serializer/Configuration/swift2.2-combined.json`. Abbreviated:

```json
{
  "outputDirectory": "Serializer",
  "deployOutputSubfolder": "deploy",
  "seedOutputSubfolder": "seed",
  "predicates": [
    {
      "name": "Content - Swift 2 (full baseline as shipped)",
      "mode": "Deploy",
      "providerType": "Content",
      "areaId": 3,
      "path": "/",
      "excludes": [],
      "excludeFields": [
        "AreaDomain", "AreaDomainLock", "AreaNoindex",
        "AreaNofollow", "AreaRobotsTxt", "AreaRobotsTxtIncludeSitemap",
        "GoogleTagManagerID"
      ],
      "excludeXmlElements": ["EmptyCartRedirectPage", "ShoppingCartLink"],
      "excludeAreaColumns": ["AreaCdnHost", "AreaCookieWarningTemplate"]
    },
    {
      "name": "EcomVatGroups",
      "mode": "Deploy",
      "providerType": "SqlTable",
      "table": "EcomVatGroups",
      "nameColumn": "VatGroupName",
      "serviceCaches": [
        "Dynamicweb.Ecommerce.International.VatGroupService"
      ]
    },
    {
      "name": "EcomPayments",
      "mode": "Deploy",
      "providerType": "SqlTable",
      "table": "EcomPayments",
      "nameColumn": "PaymentName",
      "xmlColumns": [
        "PaymentGatewayParameters",
        "PaymentCheckoutParameters"
      ],
      "serviceCaches": [
        "Dynamicweb.Ecommerce.Orders.PaymentService"
      ]
    },
    {
      "name": "UrlPath",
      "mode": "Deploy",
      "providerType": "SqlTable",
      "table": "UrlPath",
      "resolveLinksInColumns": ["UrlPathRedirect"]
    },
    {
      "name": "EcomProducts",
      "mode": "Seed",
      "providerType": "SqlTable",
      "table": "EcomProducts",
      "nameColumn": "ProductName"
    }
  ]
}
```

Open the full file to see every predicate the Swift 2.2 storefront needs. The
Deploy list covers reference data (countries, currencies, languages, VAT),
shop structure, payment and shipping definitions, order flows, and URL
redirects. The Seed list covers product catalog content.

## Config validation at load time

`ConfigLoader` enforces several checks before the first SQL statement runs:

- **JSON shape.** Required fields must be present. Mode subfolders must match
  a safe-name regex (no path traversal).
- **SQL identifiers.** Every `table`, `nameColumn`, `compareColumns` value,
  every name in `excludeFields`, `includeFields`, `xmlColumns`, and
  `resolveLinksInColumns`, and every identifier inside `where` clauses is
  validated against `INFORMATION_SCHEMA.TABLES` / `INFORMATION_SCHEMA.COLUMNS`.
  Mismatches fail at config-load with a message naming the predicate and field.
- **WHERE clause.** Tokens are whitelist-checked (`AND`, `OR`, `IN`, etc.);
  banned tokens (`;`, `--`, `/*`, `xp_`, `sp_executesql`) and DDL/DML keywords
  (`SELECT`, `UPDATE`, `DROP`, `EXEC`, …) are rejected. String literals are
  elided before tokenization so legitimate values like `'Admin Select Group'`
  pass.
- **Service caches.** Every `serviceCaches` entry must resolve through
  `DwCacheServiceRegistry`. Unknown names fail with a message listing the
  eighteen supported short and fully-qualified names.
- **Acknowledged orphans.** `acknowledgedOrphanPageIds` values are
  range-checked to reject malicious inputs.

When any of these fail, the error message names the predicate by `name` and
the offending field. Config-load errors surface as HTTP `Invalid` on the
Management API. No SQL runs until the config is clean.

## See also

- [Getting started](getting-started.md) — minimal working config
- [Glossary](glossary.md) — baseline, predicate, deploy/seed, drift, dry run
- [Concepts](concepts.md) — predicate semantics, Deploy/Seed modes
- [SQL tables](sql-tables.md) — `WHERE` clauses, field filters, credentials
- [Strict mode](strict-mode.md) — warning escalation and entry-point defaults
- [Runtime exclusions](runtime-exclusions.md) — what's auto-excluded and why
