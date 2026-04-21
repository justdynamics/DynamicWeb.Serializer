# Swift 2.2 Baseline — Per-Environment Configuration

**Paired with:** [Swift2.2-baseline.md](Swift2.2-baseline.md) (deployment data)
**Audience:** A new customer adopting the Swift 2.2 baseline, asking
"what do I configure per environment?"
**Status:** v1

---

## Relationship to the DEPLOYMENT/SEED/ENVIRONMENT split

This document covers the **ENVIRONMENT** bucket of the three-bucket split defined
in the Swift 2.2 baseline workflow (DEPLOYMENT / SEED / ENVIRONMENT). Each bucket
has a distinct ownership boundary:

| Bucket | Owned by | Captured in | Example contents |
|--------|----------|-------------|------------------|
| **DEPLOYMENT** | Baseline author | YAML under `baselines/<baseline>/_content/` and predicate-scoped SQL tables | Page tree, item types, grid rows, shop/payment/shipping definitions, VAT groups |
| **SEED** | Baseline author initially, end-user thereafter | YAML under `baselines/<baseline>/_sql/` via Seed-mode predicates | Newsletter body copy, FAQ text, customer-editable welcome content |
| **ENVIRONMENT** | Per-env operator | Filesystem config + Azure Key Vault + per-env Area fields (not serialized) | GlobalSettings.config, secrets, AreaDomain, GoogleTagManagerID |

See [Swift2.2-baseline.md § The three-bucket split](Swift2.2-baseline.md#the-three-bucket-split)
for the DEPLOYMENT/SEED boundaries and how they flow through the serializer's
Deploy and Seed modes. This document enumerates what lives in the ENVIRONMENT
bucket — the bits that the serializer **never** captures, and that therefore
must be configured on each target host.

---

## Purpose

The Swift 2.2 baseline carries **deployment data** (page tree, item types, grid
rows, ecommerce reference tables). Everything else — runtime config, secrets,
infrastructure bindings — is explicitly **out of scope** and must be configured
per environment at deploy time. This document lists those environment-specific
concerns and points to where they live.

If you treat the serializer as the single source of truth for a DW install and
forget the ENVIRONMENT bucket, your deploy will look correct in admin but the
storefront URL will not resolve, the payment gateways will be unauthenticated,
and the Swift design-system templates will be absent from the Files volume.
This document is the checklist that closes those gaps.

## What is NOT in the baseline and why

1. **GlobalSettings.config + Friendly URLs** — differs per env (domain, locale,
   routing rules). Filesystem state under `/Files/`, not DB state.
2. **Azure Key Vault secrets** — payment gateway credentials, storage keys,
   analytics tokens. Never in YAML, never in git.
3. **Per-env Area fields** — `AreaDomain`, `AreaCdnHost`, `GoogleTagManagerID`,
   SEO noindex flags. Excluded by the Swift 2.2 Content predicate.
4. **Swift templates filesystem** — git clone from upstream, not a NuGet package.
   Lives under `/Files/Templates/Designs/Swift/`.
5. **Azure App Service app settings** — bind Key Vault refs at startup via
   `@Microsoft.KeyVault(...)` syntax.
6. **DW NuGet version alignment** — source and target DBs must be migrated by
   the same DW schema version. Drift produces legitimate strict-mode warnings.

The next six sections expand each of these.

## GlobalSettings.config

File: `/Files/GlobalSettings.config` on the DW host's filesystem.

Key findings from the 2026-04-20 E2E round-trip:

- The Swift 2.2 source host has Friendly URL routing `/en-us/` enabled via
  `<Globalsettings><Routing><FriendlyUrls enabled="true" ...></Routing></Globalsettings>`.
  Without this on the target, page URLs fall back to `Default.aspx?ID=N` form
  and frontend rendering becomes verbose and partially broken (menus continue
  to emit friendly-URL hrefs that the router cannot resolve).
- `GlobalSettings.config` is **filesystem state**, not DB state. The serializer
  does NOT capture or deserialize it. The 2026-04-20 session documented this as
  the single biggest gotcha in a fresh-target deploy.
- **Customer action:** Copy `/Files/GlobalSettings.config` from source to target
  (or re-create per env). Include the `<FriendlyUrls>` section if the baseline
  was authored on a Friendly-URL host.

## Azure Key Vault secrets

Secrets that must NOT be in baseline YAML:

- Payment gateway API keys (Stripe, Adyen, QuickPay, etc.)
- Storage account connection strings
- Third-party service tokens (SendGrid, Mailchimp, analytics providers)
- Admin user credentials (bootstrap only; rotate immediately after first deploy)

The serializer has no mechanism to serialize these today (see
[Swift2.2-baseline.md § Known gaps](Swift2.2-baseline.md#known-gaps-input-to-d2)
`D2-CREDENTIAL-EXCLUSION`). If the baseline YAML ever contains a value that
looks like a secret, that is a bug — file it against the XML-children-exclusion
gap.

Pattern: Azure Key Vault references in App Service app settings using
`@Microsoft.KeyVault(VaultName=<vault>;SecretName=<name>)` syntax. App Service
identity must have `get`/`list` rights on the Vault. See the Azure App Service
config pattern section below.

## Per-env Area fields

The Swift 2.2 baseline excludes the following Area-level fields per deploy
(listed in `swift2.2-combined.json` under the Content predicate `excludeFields`
and `excludeAreaColumns`):

- `AreaDomain` — environment-specific hostname (e.g. `shop-test.example.com`
  vs `shop.example.com`)
- `AreaDomainLock` — per-env routing policy (redirect vs. allow)
- `AreaNoindex` / `AreaNofollow` / `AreaRobotsTxt` / `AreaRobotsTxtIncludeSitemap`
  — per-env SEO policy (staging usually noindexes; production indexes)
- `GoogleTagManagerID` — per-env analytics property
- `AreaCdnHost` — per-env CDN binding (if any)
- `AreaCookieWarningTemplate` — regulatory template may differ by region

**Customer action:** Set these directly in the target DB via DW admin (Settings
→ Websites → Area fields) after the first deploy, or via a post-deploy SQL
script that runs after `SerializerDeserialize`. Subsequent deploys will not
overwrite these values because the Content predicate explicitly excludes them.

## Swift templates filesystem

The Swift design system is shipped via `git clone https://github.com/dynamicweb/Swift`,
**not** as a NuGet package. The cloned `Files/Templates/Designs/Swift` folder lives
on the host's filesystem and is NOT serialized as part of the baseline.

Phase 38 B.1/B.2 investigation confirmed that the three "missing templates" seen
during the 2026-04-20 E2E round-trip (`1ColumnEmail`, `2ColumnsEmail`,
`Swift-v2_PageNoLayout.cshtml`) are **stale references in the source DB**, not
missing files. Upstream Swift never shipped them. Cleanup at
`tools/swift22-cleanup/05-null-orphan-template-refs.sql` will null those stale
references in the Swift 2.2 source DB once the script lands (Phase 38 Wave 3).

**Customer action:** After baseline deploy, ensure the Swift git clone is current:

```bash
cd /Files/Templates/Designs
git clone https://github.com/dynamicweb/Swift
# or, for an existing clone:
cd /Files/Templates/Designs/Swift && git pull
```

The Files volume (Azure Files share or App Service local storage) must be
writable by the DW process and must persist across App Service restarts. If
the volume is ephemeral, the Swift templates disappear on every restart and
render the entire storefront blank.

## Azure App Service config pattern

Recommended layout (pattern reference:
[Swift2.2-baseline.md § Azure deployment assumptions](Swift2.2-baseline.md#azure-deployment-assumptions)):

- **Azure App Service** hosts DW; `web.config` stays in-repo with placeholder
  connection strings that App Settings override at runtime.
- **Key Vault** holds all secrets; App Service managed identity has `get`/`list`
  rights on the Vault.
- **App Settings** reference Key Vault via
  `@Microsoft.KeyVault(VaultName=<vault>;SecretName=<name>)`. DW reads them as
  regular environment variables.
- **Connection strings** point at per-env SQL Azure instances
  (dev/test/QA/prod).
- **Custom domains + SSL** bound to App Service; SSL cert in Key Vault with
  auto-renewal. Not part of baseline.
- **/Files volume** persisted on Azure Files or App Service local storage —
  holds `GlobalSettings.config`, Swift templates, media uploads. Synced
  separately from baseline deploys (typically via a storage-account sync step
  in the deploy pipeline).
- **Deploy pipeline** runs `dotnet publish` + deserialize in sequence: code
  first (so the serializer DLL is in place), YAML tree copy second, then
  `POST /Admin/Api/SerializerDeserialize?mode=deploy` to apply the baseline.

## DW NuGet version alignment

**TL;DR:** Run the same DW NuGet version on the host that exports the baseline
and the host that imports it. Otherwise the serializer will emit legitimate
`source column [Area].[X] not present on target schema` warnings that in strict
mode escalate to errors — and the errors are correctly flagging the drift, not
a bug in the serializer.

### The pattern

DW ships its schema as C# `UpdateProvider` classes inside the
`Dynamicweb.*` NuGet packages. When a DW host starts, any pending update
providers run in order and mutate the DB schema — adding columns, backfilling
data, dropping obsolete columns, etc.

Different DW NuGet versions ship different sets of update providers. A DB that
was bootstrapped on an older DW version and never upgraded will have schema
shape from that older version; a DB bootstrapped on a newer version will have
the newer shape. The serializer does not reconcile schema differences beyond
logging them — it routes the source column set through the target column set
and logs a warning for any source column the target lacks (via
`TargetSchemaCache.LogMissingColumnOnce`).

### The specific Swift 2.2 case (Phase 38 B.3 finding, 2026-04-21)

The Swift 2.2 source DB contains three legacy columns on the `Area` table that
have been **dropped from DW core** in a schema update between the Swift 2.2
bootstrap version and the current DW 10.24.7 release:

| Column              | Data type     | Known use |
| ------------------- | ------------- | --------- |
| `AreaHtmlType`      | `nvarchar(10)` | Legacy DW rendering mode selector; unused in current DW. |
| `AreaLayoutPhone`   | `nvarchar(255)` | Legacy mobile-specific template path; superseded by responsive templates. |
| `AreaLayoutTablet`  | `nvarchar(255)` | Legacy tablet-specific template path; superseded by responsive templates. |

### Expanded scope — 7 additional drift columns observed (Phase 38.1 B.3.1, 2026-04-21)

The Phase 38 live E2E round-trip surfaced **seven more drift columns**
across `EcomGroups` and `EcomProducts` that were not in the original
B.3 investigation's Area-only scope. The `TargetSchemaCache`
warn-and-skip mechanism handles them identically to the three Area
columns above — all row data writes successfully; strict mode then
escalates the warning count at end-of-run.

| Source Column                              | Table                | Known use                                                |
| ------------------------------------------ | -------------------- | -------------------------------------------------------- |
| `GroupPageIDRel`                           | `EcomGroups`         | Legacy group→page relation; superseded by navigation FK. |
| `ProductPeriodId`                          | `EcomProducts`       | Legacy subscription-period FK; feature removed.          |
| `ProductVariantGroupCounter`               | `EcomProducts`       | Legacy variant-group cache column; replaced by live count. |
| `ProductPriceMatrixPeriod`                 | `EcomProducts`       | Legacy matrix-pricing period FK; superseded by PriceMatrix module. |
| `ProductOptimizedFor`                      | `EcomProducts`       | Legacy storefront optimization hint; removed. |
| `MyVolume`                                 | `EcomProducts`       | Legacy custom product field; not part of DW core shape. |
| `MyDouble`                                 | `EcomProducts`       | Legacy custom product field; not part of DW core shape. |

Source: `.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-05-e2e-results.md` §"Deserialize Seed strict mode escalations".

Resolution is the same as the three Area columns: align DW NuGet
versions between source and target hosts (preferred) OR proactively
drop the columns on the source DB after confirming they are empty.
The serializer still does **not** ship a `knownEnvSchemaDrift`
allowlist (rejected in Phase 38 B.3 and reaffirmed in Phase 38.1
B.3.1 D-38.1-08); operational version alignment is the supported
path.

Investigation notes:
`.planning/phases/38-production-ready-baseline-hardening-retroactive-tests-for-37/38-03-b3-investigation.md`.

Verified findings:

- CleanDB (bootstrapped on a newer DW version) has **none** of the three
  columns.
- Swift 2.2 has all three columns but **zero non-empty values** across both
  Area rows.
- No current DW NuGet DLL contains these column names in any read or write
  code path (binary grep across all 67 `Dynamicweb.*` packages).
- Both hosts run effectively the same DW core schema version (same set of
  `UpdateProvider` providers in `Updates` table, apart from peripheral modules
  like Forum and GLS shipping on Swift 2.2 only).

Conclusion: the columns are **legacy DW schema carried forward on Swift 2.2 as
empty placeholders**. They are not Swift-specific extensions.

### Customer action: align DW NuGet versions

Because DW Suite is distributed as a NuGet package, version alignment is a
one-line change in the host `.csproj` + a host restart (DW runs pending
schema updates at startup). Recommended order:

1. Identify the newer DW NuGet version. Usually the target is already on it,
   because it was bootstrapped last.
2. On the older host (usually the source/export), update the
   `<PackageReference Include="Dynamicweb.Suite" Version="..." />` line in the
   host csproj to match.
3. `dotnet publish` and restart the host. DW will run any pending
   `UpdateProvider` classes, including those that drop legacy columns like the
   three above.
4. Re-run the serializer export. The warnings disappear because the source
   schema is now aligned with the target schema.

### Optional: source-side cleanup without version upgrade

If a DW version bump is not feasible on the source host, and the specific
legacy columns carry no data, a customer can proactively drop them from the
source DB:

```sql
-- ONLY IF the columns are confirmed empty via:
--   SELECT COUNT(*) FROM [Area] WHERE AreaHtmlType IS NOT NULL AND AreaHtmlType <> '';
-- etc.
ALTER TABLE [Area] DROP COLUMN [AreaHtmlType];
ALTER TABLE [Area] DROP COLUMN [AreaLayoutPhone];
ALTER TABLE [Area] DROP COLUMN [AreaLayoutTablet];
```

This is a customer-operations concern; the serializer does not automate it.
The recommended path is always a DW version bump because it applies every
legacy-migration consistently instead of hand-dropping individual columns.

### What the serializer does NOT do

- It does **not** silently skip schema-drift columns in strict mode. The
  `WARNING: source column [Table].[Col] not present on target schema` lines
  are routed through `StrictModeEscalator` and in strict mode are accumulated
  into a `CumulativeStrictModeException` at end-of-run. This is intentional:
  schema drift is a signal that deployment hosts are mis-aligned and the
  deploy result is non-deterministic, and strict mode surfaces that loudly.
- It does **not** ship a `knownEnvSchemaDrift` allowlist field on predicates
  or config (considered during Phase 38 B.3; rejected because the correct fix
  is operational — a drift allowlist normalizes what should stay visible, and
  would mask future real drift-as-data-loss scenarios). If a future use case
  genuinely requires allowlisting specific drift, file a new phase that
  weighs the correctness trade-off.
