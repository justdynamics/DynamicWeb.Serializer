# Runtime exclusions

Some SQL columns hold values that are either recomputed at runtime,
environment-specific, or credentials. Serializing them into a baseline
either dirties diffs with meaningless per-run variation, overwrites
target state on deploy, or leaks secrets into Git. This page documents
what the serializer auto-excludes, what it deliberately does NOT
auto-exclude, and the pre-commit checks to run before pushing a baseline.

## Table of contents

- [Auto-excluded columns](#auto-excluded-columns)
- [Opting back in with includeFields](#opting-back-in-with-includefields)
- [Credential handling](#credential-handling)
- [Pre-commit grep check](#pre-commit-grep-check)
- [Roadmap: curated credential registry (v0.6.0)](#roadmap-curated-credential-registry-v060)

## Auto-excluded columns

`RuntimeExcludes` is a single flat map of table → columns. Columns
listed here are stripped from SqlTable predicate output by default,
whether or not the predicate lists them in `excludeFields`.

| Table     | Column                  | Rationale |
|-----------|-------------------------|-----------|
| `UrlPath` | `UrlPathVisitsCount`    | Visit counter. Recomputed at runtime; overwrites target on deploy with a stale snapshot. |
| `EcomShops` | `ShopIndexRepository` | Env-specific search-index repository name. Differs between Azure dev / test / QA / prod. |
| `EcomShops` | `ShopIndexName`       | Env-specific search-index name. |
| `EcomShops` | `ShopIndexDocumentType` | Env-specific document type. |
| `EcomShops` | `ShopIndexUpdate`     | Runtime last-updated tick. |
| `EcomShops` | `ShopIndexBuilder`    | Env-specific index builder type. |

The list is conservative by design. An entry is added only when the
column is demonstrably runtime-only or environment-specific on every
DW 10.x install. New entries require a PR against
`src/DynamicWeb.Serializer/Configuration/RuntimeExcludes.cs`.

Source: `src/DynamicWeb.Serializer/Configuration/RuntimeExcludes.cs`.

## Opting back in with includeFields

If a predicate specifically wants one of the auto-excluded columns
serialized — e.g. a test-only predicate that captures search-index
configuration for reproducibility — list the column in `includeFields`:

```json
{
  "name": "Shops (with index)",
  "providerType": "SqlTable",
  "table": "EcomShops",
  "includeFields": ["ShopIndexRepository", "ShopIndexName"]
}
```

`includeFields` wins over `RuntimeExcludes`. Effective exclude set
per predicate:

```
RuntimeExcludes[table]
  minus predicate.includeFields
  plus predicate.excludeFields
```

`excludeFields` on the predicate still wins over `includeFields` if
there's overlap (belt and suspenders — a user can explicitly exclude
what they had explicitly included, and the exclude takes effect).

## Credential handling

**Credentials are NOT auto-excluded in v0.5.0.** Payment gateway keys
(`PaymentMerchantNum`, `PaymentGatewayMD5Key`), carrier account tokens
(`CarrierAccount`), and any other sensitive column must be listed
manually in `excludeFields` per predicate:

```json
{
  "name": "EcomPayments",
  "providerType": "SqlTable",
  "table": "EcomPayments",
  "nameColumn": "PaymentName",
  "excludeFields": [
    "PaymentMerchantNum",
    "PaymentGatewayMD5Key"
  ],
  "xmlColumns": [
    "PaymentGatewayParameters",
    "PaymentCheckoutParameters"
  ],
  "excludeXmlElements": [
    "apiKey",
    "sharedSecret",
    "merchantPassword"
  ]
}
```

Two places need attention for payment / shipping predicates:

1. **Top-level columns.** List credential columns in `excludeFields` by
   name.
2. **Embedded XML.** Payment gateways often store credentials as
   nested XML elements inside `PaymentGatewayParameters` or
   `PaymentCheckoutParameters`. Use `excludeXmlElements` to strip
   specific child elements by name. There is no schema-driven
   credential detection in v0.5.0; the element names come from the
   gateway's own config shape.

The same caveat applies to:

- `AccessUser.AccessUserPassword` / `AccessUserPasswordSalt` if syncing users.
- `EcomShippings` carrier account tokens.
- Any custom ecommerce integration that stores tokens in a table.

## Pre-commit grep check

Run this against your baseline before committing to catch credential
leakage:

```bash
grep -rn \
  -e "PaymentGatewayMD5Key" \
  -e "PaymentMerchantNum" \
  -e "CarrierAccount" \
  -e "apiKey" \
  -e "sharedSecret" \
  baselines/
```

If any match returns a non-empty value, add the column (or XML element
name) to the predicate's `excludeFields` / `excludeXmlElements` and
re-run serialize.

For teams that serialize additional sensitive tables, extend the pattern
list. A reasonable starting set:

```bash
# Password / token / secret / key patterns
grep -irn \
  -e "password" \
  -e "secret" \
  -e "token" \
  -e "apikey" \
  -e "privatekey" \
  baselines/ \
  | grep -v ": *['\"]\{0,1\}[[:space:]]*$"   # ignore empty values
```

Tune per your naming conventions. The goal is a CI-runnable check that
catches "oops, a credential column made it into the baseline" before
the PR lands.

## Roadmap: curated credential registry (v0.6.0)

A curated credential registry — analogous to `RuntimeExcludes` but for
credential columns — was deliberately deferred to v0.6.0 per the
Phase 37 decision log (`D-07`, `D-09`). Two reasons:

- **Classification is per-customer.** What's a credential on one
  customer's payment gateway is a shared config value on another
  (e.g. a test-mode API key that's safe to ship but a prod API key
  that isn't). A one-size registry would be wrong often enough to
  erode trust.
- **The env-config workflow is the real fix.** Credentials belong in
  Azure Key Vault (or equivalent), injected as App Service settings at
  startup. The env-config workflow — per-environment secret bindings
  that the DW host reads from the platform, not the DB — is the v0.6.0
  milestone that retires the "exclude manually" workaround.

Until then, `excludeFields` is explicit. Review it on every baseline PR.
The pre-commit grep above is the compensating control.

## See also

- [SQL tables](sql-tables.md) — `excludeFields` and `xmlColumns` in context
- [Configuration](configuration.md) — the full predicate schema
- [Swift 2.2 baseline](baselines/Swift2.2-baseline.md) — the reference
  payment / shipping predicate with XML handling
- [Per-environment config](baselines/env-bucket.md) — where credentials
  belong instead of in YAML
