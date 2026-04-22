# SQL tables

`SqlTable` predicates capture arbitrary SQL tables as YAML, one file per row.
Use them for reference data, ecommerce configuration, URL redirects,
user groups, or any table whose rows map naturally to deployment or seed
content. This page covers when to reach for them, the full field surface,
and the validation guarantees.

## Table of contents

- [When to use SqlTable vs Content](#when-to-use-sqltable-vs-content)
- [Minimal predicate](#minimal-predicate)
- [Row identity: nameColumn vs composite key](#row-identity-namecolumn-vs-composite-key)
- [WHERE clauses](#where-clauses)
- [includeFields / excludeFields](#includefields--excludefields)
- [xmlColumns and excludeXmlElements](#xmlcolumns-and-excludexmlelements)
- [serviceCaches](#servicecaches)
- [compareColumns for change detection](#comparecolumns-for-change-detection)
- [resolveLinksInColumns](#resolvelinksincolumns)
- [Schema sync](#schema-sync)

## When to use SqlTable vs Content

Use **Content** predicates when:

- You're syncing pages, grids, paragraphs, areas, or item-type-tagged
  content the end user edits through DW admin's content tree.
- Permission mapping, GUID identity, and ItemType field rewriting
  matter. Content predicates route through the full DW content API; a
  SqlTable predicate pointed at `[Page]` and `[Paragraph]` would bypass
  every DW side-effect that makes content actually work.

Use **SqlTable** predicates when:

- The data is structured table data — reference lists (countries,
  currencies), ecommerce config (payment methods, shipping methods,
  VAT rules, order flows), redirects (`UrlPath`), users / groups.
- There is no content-tree hierarchy; rows are a flat set with a
  natural key.
- You need `WHERE`-clause filtering to include a subset of rows from a
  mixed-purpose table.

**Never** use SqlTable to sync `[Page]`, `[Paragraph]`, `[GridRow]`, or
`[Area]` — these are Content territory. Bypassing DW's content APIs
skips caching, permission inheritance, navigation refresh, and property
normalization. Use the Content predicate.

## Minimal predicate

```json
{
  "name": "EcomOrderFlow",
  "providerType": "SqlTable",
  "table": "EcomOrderFlow",
  "nameColumn": "OrderFlowName"
}
```

This captures every row in `EcomOrderFlow`, writes one YAML file per row
named by the `OrderFlowName` value, and will deserialize back into the
target matching rows by `OrderFlowName` (source-wins overwrite in Deploy
mode).

## Row identity: nameColumn vs composite key

`nameColumn` is optional but usually preferred.

**With `nameColumn`:** One file per row, named after the `nameColumn`
value. The file name becomes the natural key. `Default.yml` / `Quote.yml`
/ `Subscription.yml` is scannable in Git and reads well in diffs. On
deserialize, the writer matches rows by `nameColumn`; existing rows get
updated, new rows get inserted.

**Without `nameColumn`:** The writer falls back to the table's composite
primary key. File names derive from the key values — for
`EcomShopGroupRelation` (a junction table with composite PK
`ShopGroupShopId, ShopGroupGroupId`), files look like
`SHOP1$$GROUP253.yml`. Less readable but works for tables without a
natural single-column key.

`nameColumn` must exist on the table and be validated against
`INFORMATION_SCHEMA.COLUMNS` at config-load. A misspelled column fails
loud:

```
Column identifier not in INFORMATION_SCHEMA: '[EcomVatGroups].[VatName]'.
Check exclude/include/where fields in your predicate config.
```

The reference baseline used to have exactly this bug (`VatName` instead
of `VatGroupName`); `SqlIdentifierValidator` catches it at config-load now.

## WHERE clauses

The optional `where` field filters rows at serialize time. Use it to
include only a subset of rows from a mixed-purpose table:

```json
{
  "name": "AccessUser-Roles",
  "providerType": "SqlTable",
  "table": "AccessUser",
  "where": "AccessUserType = 2 AND AccessUserUserName IN ('Admin','Editors','CMS Editors')",
  "excludeFields": ["AccessUserPassword", "AccessUserPasswordSalt"]
}
```

This emits only the admin and editor group rows, leaving customer users
(AccessUserType = 1) out of the baseline.

### Validation rules

Every WHERE clause is validated at config-load and at admin-UI save:

- **Banned tokens** (literal substring scan, case-insensitive): `;`, `--`,
  `/*`, `*/`, `xp_`, `sp_executesql`.
- **Banned keywords** (whole-word, case-insensitive): `SELECT`, `UPDATE`,
  `DELETE`, `INSERT`, `MERGE`, `EXEC`, `EXECUTE`, `DROP`, `TRUNCATE`,
  `ALTER`, `CREATE`, `GRANT`, `REVOKE`, `UNION`, `INTO`, `WAITFOR`,
  `SHUTDOWN`.
- **Identifier check.** After stripping string literals, every
  identifier-shaped token (starts with a letter or underscore, not a
  known operator keyword) must match a column on the target table per
  `INFORMATION_SCHEMA.COLUMNS`.
- **Safe operators.** `AND`, `OR`, `NOT`, `IN`, `IS`, `NULL`, `LIKE`,
  `BETWEEN`, `TRUE`, `FALSE` pass through.

String literals are elided before tokenization so content like
`'Admin Select Group'` passes. Rejection messages name the offending
token and show the full clause:

```
WHERE clause references unknown identifier 'AccessUserUsrName'.
Not a column on the target table. Check INFORMATION_SCHEMA.
Clause: AccessUserUsrName = 'admin'
```

### Limits

- Single-table only. No joins, subqueries, or `EXISTS` blocks.
- Literal values only. String values in single quotes, numeric literals
  inline. Parameterization is not supported — the serializer runs under
  admin trust and reads only, so value injection is a non-issue; the
  surface is identifier splicing, which the validator closes.
- No function calls. `LOWER(AccessUserUserName) = 'admin'` is rejected
  (`LOWER` is not an allowed identifier).

Source: `src/DynamicWeb.Serializer/Configuration/SqlWhereClauseValidator.cs`.

## includeFields / excludeFields

**excludeFields** strips columns from serialization output. Common uses:

- Masking credentials (`AccessUserPassword`, `PaymentMerchantNum`,
  `PaymentGatewayMD5Key`, `CarrierAccount`).
- Masking environment-specific columns that the auto-exclude list
  doesn't cover yet.
- Dropping identity / audit columns like `CreatedBy` or timestamps
  where they add noise to diffs.

```json
"excludeFields": [
  "AccessUserPassword",
  "AccessUserPasswordSalt",
  "AccessUserLastLoginDate"
]
```

**includeFields** opts a column back IN that would otherwise be removed
by `RuntimeExcludes` (the auto-exclude list for runtime-only columns).
See [`runtime-exclusions.md`](runtime-exclusions.md) for the auto-exclude
set.

```json
{
  "name": "Shops (with index)",
  "providerType": "SqlTable",
  "table": "EcomShops",
  "includeFields": ["ShopIndexRepository", "ShopIndexName"]
}
```

Both lists are validated against `INFORMATION_SCHEMA.COLUMNS` at
config-load. Misspelled columns fail loud.

Effective serialized column set, per row:

```
all columns of table
  minus RuntimeExcludes[table]
  plus predicate.includeFields
  minus predicate.excludeFields
```

`includeFields` wins over `RuntimeExcludes`. `excludeFields` wins over
everything else.

## xmlColumns and excludeXmlElements

DW stores rich configuration in XML-shaped string columns:
`PaymentGatewayParameters`, `PaymentCheckoutParameters`,
`ShippingServiceParameters`, `OrderFlowXml`. Raw SQL serialization would
write them as single-line encoded strings — unreadable in YAML, useless
in diffs.

`xmlColumns` marks these columns for pretty-printing. At serialize, the
column value is XML-parsed and written as a nested YAML structure. At
deserialize, it's reassembled back to XML before the MERGE:

```json
{
  "name": "EcomPayments",
  "providerType": "SqlTable",
  "table": "EcomPayments",
  "nameColumn": "PaymentName",
  "xmlColumns": [
    "PaymentGatewayParameters",
    "PaymentCheckoutParameters"
  ]
}
```

`excludeXmlElements` strips specific XML elements from every XML column
on every row. The typical use case is masking env-specific page-ID
references embedded in the XML payload so the baseline stays portable:

```json
"excludeXmlElements": [
  "EmptyCartRedirectPage",
  "ShoppingCartLink"
]
```

Both lists are case-sensitive at the element name level. Validation is
structural (well-formed XML on write) rather than schema-driven; DW's
own consumer of the XML column is the final arbiter of correctness.

## serviceCaches

DW caches many domain objects in process memory:
`CountryService`, `VatGroupService`, `PaymentService`. When
deserialize writes to the underlying SQL tables, those caches go stale
until the next TTL expiry. Pages rendering immediately after a deploy
can serve cached values that no longer match the DB.

`serviceCaches` lists service types to clear after the predicate's
deserialize completes:

```json
"serviceCaches": [
  "Dynamicweb.Ecommerce.International.CountryService",
  "Dynamicweb.Ecommerce.International.CountryRelationService"
]
```

Accepted forms:

- **Short name:** `CountryService`
- **Full type name:** `Dynamicweb.Ecommerce.International.CountryService`

Both resolve case-insensitively through `DwCacheServiceRegistry`.
Unknown names fail at config-load with the full supported-names list
(eighteen entries today). Adding a new service is a PR against
`DwCacheServiceRegistry.cs` — see
[`strict-mode.md`](strict-mode.md#adding-a-new-cache-service).

## compareColumns for change detection

`compareColumns` drives the "skip unchanged" path. If set, deserialize
reads the target row's values for the listed columns and compares them
to the YAML's values. Matching rows go to `skipped`; mismatching rows
go to `updated`.

```json
{
  "name": "EcomOrderFlow",
  "providerType": "SqlTable",
  "table": "EcomOrderFlow",
  "nameColumn": "OrderFlowName",
  "compareColumns": "OrderFlowName,OrderFlowDescription,OrderFlowActive"
}
```

Empty or absent: all non-identity columns are compared. That default is
usually correct; set `compareColumns` explicitly only when specific
columns should drive skip-detection (e.g. ignoring audit timestamps).

Column names must exist on the table; validated at config-load.

## resolveLinksInColumns

Columns holding cross-environment page references (`Default.aspx?ID=N`
strings) opt into link rewriting at deserialize time:

```json
{
  "name": "UrlPath",
  "providerType": "SqlTable",
  "table": "UrlPath",
  "resolveLinksInColumns": ["UrlPathRedirect"]
}
```

At deserialize:

1. Content predicates run first, building the source → target page ID
   map via `InternalLinkResolver.BuildSourceToTargetMap`.
2. `SqlTableWriter` reads the row's column value.
3. `InternalLinkResolver.ResolveInStringColumn(value)` rewrites
   `Default.aspx?ID=N` (and `"SelectedValue": "N"` in ButtonEditor JSON)
   using the map.
4. The rewritten string is parameter-bound into MERGE — the raw rewrite
   never reaches SQL composition.

Column names validated at config-load, same gate as `excludeFields`.
Unresolved references log `WARNING: Unresolvable page ID N in link` and
— in strict mode — escalate at end of run. See
[`link-resolution.md`](link-resolution.md) for the three-pass pipeline.

## Schema sync

One predicate-level directive bridges table-shape differences between
source and target DBs:

```json
"schemaSync": "EcomGroupFields"
```

Currently `EcomGroupFields` is the only recognized value. It runs
`EcomGroupFieldSchemaSync` before the predicate's row writes, ensuring
the `EcomProductGroupField` definitions exist on target and the
corresponding custom columns are added to `[EcomGroups]` before any
row data flows in. Without this, a fresh target without matching field
definitions rejects the row writes with column-not-found errors.

The normal operational answer to schema differences is to align DW
NuGet versions between source and target. `schemaSync: EcomGroupFields`
is a targeted mitigation for the one DW-native case where the schema
depends on row data in a separate table.

For the broader "DW NuGet versions don't match" problem, see
[`baselines/env-bucket.md`](baselines/env-bucket.md#dw-nuget-version-alignment).

## See also

- [Configuration](configuration.md#sqltable-predicate-fields) — every field in one place
- [Runtime exclusions](runtime-exclusions.md) — the auto-excluded column list and credential handling
- [Link resolution](link-resolution.md) — how `resolveLinksInColumns` works end to end
- [Strict mode](strict-mode.md) — escalation of SQL-related warnings
