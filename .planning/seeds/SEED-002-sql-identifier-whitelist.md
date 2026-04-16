---
id: SEED-002
status: dormant
planted: 2026-04-17
planted_during: v0.4.0 (completed)
trigger_when: Serializer.config.json or predicate definitions come from an untrusted or semi-trusted source
scope: Small
---

# SEED-002: Whitelist SQL identifiers (table/column names) against INFORMATION_SCHEMA

## Why This Matters

`SqlTableProvider` takes table and column names from `Serializer.config.json`
(and the admin-UI predicate editor) and composes them directly into SQL. Values
are parameterized via `CommandBuilder`, but **identifiers cannot be
parameterized** in T-SQL — they're spliced as text. That's fine today because
predicate configs are authored by a trusted admin through the DW admin UI (role-
gated).

The moment that assumption weakens — a self-service UI, an import flow that
accepts a `.config.json` from a customer, a predicate-as-code repo where
anyone can open a PR that gets auto-applied — the splicing becomes a SQL
injection surface. `"Products; DROP TABLE EcomOrders;--"` as a table name is
all it takes.

Pre-emptive fix is cheap: validate every identifier against
`INFORMATION_SCHEMA.TABLES` / `.COLUMNS` at the boundary (config load + UI save),
before it ever reaches a SQL string. Whitelist, not escape — escaping
identifiers in SQL Server is a minefield (`[` / `]` / bracketed names with
embedded `]]`).

## When to Surface

**Trigger:** Any scenario where predicate configs are authored by someone less
trusted than the DW admin.

Present this seed during `/gsd-new-milestone` when the milestone scope mentions
any of:

- Multi-tenant deployment (one serializer, multiple customers)
- Import/export of configs across environments without human review
- Self-service predicate creation for non-admin roles
- Open-source release where contributors submit example configs
- Any form of "configuration as code" with automated application

## Scope Estimate

**Small** — a few hours.

- Add `SqlIdentifierValidator` with `ValidateTable(string)` and `ValidateColumn(string, string)` methods that hit `INFORMATION_SCHEMA`
- Call at two choke points: `SerializerConfiguration.Load(...)` and `SavePredicateCommand`
- Cache the schema lookup for the duration of a single operation (don't re-query per predicate)
- Fail fast with a clear error ("table 'X' does not exist — check predicate name")
- Unit test: malicious identifier rejected, legitimate identifier accepted, identifier matching existing table but with different casing normalized

## Breadcrumbs

Splicing sites:

- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableReader.cs` — `SELECT ... FROM {table}`
- `src/DynamicWeb.Serializer/Providers/SqlTable/SqlTableWriter.cs` — `MERGE ... INTO {table}` / `INSERT INTO {table}`
- `src/DynamicWeb.Serializer/Providers/SqlTable/FkDependencyResolver.cs` — queries `INFORMATION_SCHEMA` already, reuse pattern
- `src/DynamicWeb.Serializer/Providers/SqlTable/DataGroupMetadataReader.cs`
- `src/DynamicWeb.Serializer/Providers/SqlTable/EcomGroupFieldSchemaSync.cs` — also composes DDL

Validation entry points:

- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs` — config load
- `src/DynamicWeb.Serializer/AdminUI/Commands/SavePredicateCommand.cs` — UI save

## Notes

Not a current vulnerability. Every known deployment has trusted admin-only
access to predicate configs through the DW admin UI. This seed is defense in
depth for the day that assumption breaks.

`FkDependencyResolver` already queries `INFORMATION_SCHEMA.KEY_COLUMN_USAGE` —
the validator can live alongside and share the connection/cache.
