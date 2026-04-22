# Configuration

The full reference for `Serializer.config.json` and the admin UI screens that
edit it. Use this page as a lookup when you're composing a new baseline or
debugging a config-load failure.

## Table of contents

- [Where the config lives](#where-the-config-lives)
- [Top-level config schema](#top-level-config-schema)
- [Deploy and Seed mode configs](#deploy-and-seed-mode-configs)
- [Content predicate fields](#content-predicate-fields)
- [SqlTable predicate fields](#sqltable-predicate-fields)
- [Global exclusion maps](#global-exclusion-maps)
- [Admin UI screens](#admin-ui-screens)
- [Full config example](#full-config-example)
- [Config validation at load time](#config-validation-at-load-time)

## Where the config lives

The canonical config path is:

```
{DW_host}/Files/Serializer.config.json
```

The admin UI at `Settings > Database > Serialize` reads and writes this file.
Manual edits are picked up on the next screen load (no restart required). The
Management API commands also read the same file on each call.

Legacy `ContentSync.config.json` names are recognized for backward
compatibility. New installs should use `Serializer.config.json`.

## Top-level config schema

```json
{
  "outputDirectory": "Serializer",
  "logLevel": "info",
  "dryRun": false,
  "strictMode": false,
  "deploy": { ... },
  "seed": { ... }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `outputDirectory` | string (required) | Top-level folder relative to `Files/System`. Subfolders `SerializeRoot/`, `Upload/`, `Download/`, `Log/` are created automatically. |
| `logLevel` | `info` / `debug` / `warn` / `error` | Logging verbosity. Default: `info`. |
| `dryRun` | boolean | When `true`, deserialize reports what would change without mutating the DB. Default: `false`. |
| `strictMode` | boolean or null | `true` escalates recoverable warnings to `CumulativeStrictModeException`; `false` logs and continues; `null` uses the entry-point default (API/CLI: on, admin UI: off). See [`strict-mode.md`](strict-mode.md). |
| `deploy` | ModeConfig | The Deploy mode (source-wins, default). |
| `seed` | ModeConfig | The Seed mode (destination-wins, opt-in). |

## Deploy and Seed mode configs

Each mode is a `ModeConfig` with its own predicate list and exclusion dictionaries:

```json
{
  "outputSubfolder": "deploy",
  "conflictStrategy": "source-wins",
  "predicates": [ ... ],
  "excludeFieldsByItemType": {
    "Swift_Content": ["SystemName_Internal"]
  },
  "excludeXmlElementsByType": {
    "ParagraphModule": ["cache"]
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `outputSubfolder` | string | Subfolder under `SerializeRoot/` where this mode writes YAML. Default: `deploy` / `seed`. Validated against a safe-name regex to prevent path traversal. |
| `conflictStrategy` | `source-wins` / `destination-wins` | How deserialize resolves conflicts. Deploy default is `source-wins`; Seed default is `destination-wins`. |
| `predicates` | list | The predicates serialized and deserialized when this mode runs. |
| `excludeFieldsByItemType` | map | Global per-item-type field exclusions, scoped to this mode. Key: item-type system name. Value: list of field names to strip. |
| `excludeXmlElementsByType` | map | Global per-XML-type element exclusions, scoped to this mode. Key: XML type name. Value: list of element names to strip. |

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

Two dictionaries live inside each mode config and apply across every predicate in that mode:

```json
"deploy": {
  "excludeFieldsByItemType": {
    "Swift_Content": ["SystemName_Internal"],
    "Swift-v2_Button": ["DebugMarker"]
  },
  "excludeXmlElementsByType": {
    "ParagraphModule": ["cache"],
    "PageItem": ["EmptyCartRedirectPage", "ShoppingCartLink"]
  }
}
```

Use these for cross-predicate cleanup. Per-predicate exclusions still work;
the effective exclude set is the union of the predicate's list and the mode's
dictionary entry for that item type.

## Admin UI screens

Navigation: `Settings > Database > Serialize`.

| Node | Purpose |
|------|---------|
| **Serialize** | Top-level settings screen: output directory, log level, dry-run toggle, conflict strategy, strict-mode toggle. |
| **Predicates** | CRUD for Content and SqlTable predicates. Each predicate has sub-nodes per mode (Deploy, Seed). Fields match the JSON schema above with dual-list pickers populated from the live DB schema. |
| **Item Types** | Browse item types by category, edit per-type field exclusions for each mode. |
| **Embedded XML** | Browse XML types, edit per-type element exclusions for each mode. |
| **Log Viewer** | Per-run logs with summary headers, per-predicate counts, and `AdviceGenerator` remediation hints. |

The **"Serialize subtree"** action appears in the Actions menu on every page
edit screen. It ad-hoc serializes the current page and its descendants to a
zip file downloaded by the browser and copied to `Files/System/Serializer/Download/`.
The matching import is at `Files/System/Serializer/Upload/` — drop a zip there
and use the file's **"Import to database"** action.

## Full config example

The Swift 2.2 reference baseline — a working config with one Content predicate
and seventeen SqlTable predicates. Lives at
`src/DynamicWeb.Serializer/Configuration/swift2.2-combined.json`. Abbreviated:

```json
{
  "outputDirectory": "Serializer",
  "logLevel": "info",
  "dryRun": false,
  "strictMode": true,
  "deploy": {
    "outputSubfolder": "deploy",
    "conflictStrategy": "source-wins",
    "predicates": [
      {
        "name": "Content - Swift 2 (full baseline as shipped)",
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
        "providerType": "SqlTable",
        "table": "EcomVatGroups",
        "nameColumn": "VatGroupName",
        "serviceCaches": [
          "Dynamicweb.Ecommerce.International.VatGroupService"
        ]
      },
      {
        "name": "EcomPayments",
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
        "providerType": "SqlTable",
        "table": "UrlPath",
        "resolveLinksInColumns": ["UrlPathRedirect"]
      }
    ]
  },
  "seed": {
    "outputSubfolder": "seed",
    "conflictStrategy": "destination-wins",
    "predicates": [
      {
        "name": "EcomProducts",
        "providerType": "SqlTable",
        "table": "EcomProducts",
        "nameColumn": "ProductName"
      }
    ]
  }
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
- [Concepts](concepts.md) — predicate semantics, Deploy/Seed modes
- [SQL tables](sql-tables.md) — `WHERE` clauses, field filters, credentials
- [Strict mode](strict-mode.md) — `strictMode` behavior and defaults
- [Runtime exclusions](runtime-exclusions.md) — what's auto-excluded and why
