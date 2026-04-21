# Swift 2.2 Baseline — Deployment Configuration

**Config file:** `src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json`
**Target:** Azure App Service + SQL Azure, dev → test → QA → prod promotion
**Status:** v1 — deployment data only. Seed content and env-specific config are
deliberately out-of-scope (see "Gaps" below).

---

## Purpose

When a customer adopts DynamicWeb Swift 2.2 as their commerce platform, they
receive a CMS with hundreds of structural pages, item-type definitions, shop
setup, payment gateway definitions, country/currency/VAT reference data, and
the URL-rewriting infrastructure that makes the storefront work. All of that
is **deployment-driven** — it must be identical across dev/test/QA/prod for
the storefront to function consistently. It must never drift based on
end-user edits.

This baseline captures exactly that slice: what DW *would ship* to a new
customer as "here's your working Swift 2.2 starting point." Version-controlling
it means:

- A fresh Azure SQL database can be seeded with `deserialize --config
  swift2.2-baseline.json` and result in a working storefront before a single
  product is added.
- Upgrades ship as config diffs reviewed in a PR, not database scripts run
  from a DBA's laptop.
- Environment drift becomes impossible for baseline data — if test diverges
  from prod, `serialize` shows it in the next commit.

## The three-bucket split

Every piece of data in Swift 2.2 belongs to one of three buckets.

| Bucket | Who owns it | Behavior on deploy | In baseline? |
|--------|-------------|-------------------|--------------|
| **DEPLOYMENT** | Developer / DW template | Overwrite target (source wins) | **YES** |
| **SEED** | Developer initially, end-user thereafter | Apply once if absent; never overwrite | Partial (see gaps) |
| **ENVIRONMENT** | Ops / infrastructure | Never in baseline; per-env config | No |

**DEPLOYMENT** data is what this baseline is for. Payment method *definitions*,
currency codes, country lists, VAT rates, shop structure, service-page
scaffolding, item-type schemas. If the dev changes it, every env follows.

**SEED** data is what's delivered once to bootstrap a fresh install and then
belongs to the customer. Customer Center welcome copy, FAQ body text,
newsletter email templates. The current serializer **does not have a
safe mode for this** — deserializing would overwrite customer edits. See
`D2-SEED-CONTENT-MODE` in the companion improvement plan.

**ENVIRONMENT** data is per-instance infrastructure: the shop's live domain,
payment gateway API keys, Azure storage CDN hostnames, Google Tag Manager
IDs, analytics tokens. This stays in Azure App Service configuration or
Azure Key Vault — never in git.

## The Swift 2.2 contamination problem

DW ships Swift 2.2 with test/demo contamination:

- 267 orphaned pages in 5 deleted areas (invisible in admin UI, non-renderable)
- 1,091 products with empty names in non-primary languages
- Mixed tenant-demo user groups ("Nordic Media Group", "Digital Solutions
  Bureau") alongside legitimate structural groups ("Admin", "Editors", "CSR")
- Test pages like "New Serialized Page" created during prior serializer
  development

For this baseline, we take the approach of **explicit inclusion**: the
content predicate names exactly which subtree to include and excludes the
known-marketing / known-test paths. We do NOT attempt to clean up the source
— DW owns that upstream.

## Predicate-by-predicate reasoning

### Content — Swift 2 (deployment)

**Provider:** `Content` (must route through ContentProvider, not SqlTable —
see memory: `feedback_content_not_sql.md`)

**Included:** Area 3 "Swift 2", full subtree rooted at `/`.

**Excluded paths:**

| Path | PageId | Why excluded |
|------|--------|--------------|
| `/Home` | 6869 | Marketing landing page with brand-specific copy, hero banners, promotional content. Every customer replaces this immediately. |
| `/Home Machines` | 4897 | Machine-specific marketing subtree. Inactive (`PageActive=0`) and tied to the demo "Swift Machines" retailer persona. |
| `/About` | 107 | Contains a `Contact` subpage with the demo contact form. Customer-specific by definition (their address, phone, email). |
| `/Posts` | 8345 | Blog/news content. Customer-authored. Wrong bucket for source control. |
| `/New Serialized Page` | 8451 | Test artifact from prior serializer development (created 2026-04-03). Not part of any Swift template. |

**Excluded area columns** (env-specific):

- `AreaDomain`, `AreaDomainLock` — the live domain binding per environment
- `AreaNoindex`, `AreaNofollow`, `AreaRobotsTxt`, `AreaRobotsTxtIncludeSitemap` — SEO
  directives often differ between non-prod (noindex) and prod (indexed)
- `AreaCdnHost` — CDN binding differs per env (local vs. Azure Front Door vs.
  prod Akamai)
- `AreaCookieWarningTemplate` — regulatory template may differ by region
- `GoogleTagManagerID` — per-env GTM container

**Excluded XML elements** inside item/paragraph XML payloads:

- `EmptyCartRedirectPage`, `ShoppingCartLink` — these hold page-ID references
  that can shift across envs; excluded so baseline stays portable.

### Pure reference data (no env variation, safe-to-overwrite)

- **EcomCountries** — ISO country list. Identical everywhere.
- **EcomCountryText** — translated country names per language. Deployment of
  translations.
- **EcomCurrencies** — currency codes + rates. Note: live rates are often
  updated by a daily job; treating them as deployment-managed is a simplification.
  If a customer has a live FX feed, they should exclude this predicate and
  manage currencies out-of-band.
- **EcomLanguages** — supported languages for the storefront. Adding a language
  is a deploy-level change.

### VAT / tax configuration

- **EcomVatGroups** — VAT rate definitions (e.g., "Standard 25%"). Deployment.
- **EcomVatCountryRelations** — which VAT group applies to which country.
  Deployment.

### Shop structure

- **EcomShops** — the shop definition row. Excludes `ShopAutoId` (identity
  column, not meaningful across environments) and `ShopCreated` (per-instance
  timestamp). Everything else is structural.
- **EcomShopLanguageRelation** — which languages this shop supports.
- **EcomShopGroupRelation** — which product groups belong to which shop.
  (Note: product *groups* themselves are customer content, NOT in this
  baseline. The relation is included in case a customer wires their groups to
  a shop — the relation itself defines shop scope, not the groups.)

### Payment & shipping

- **EcomPayments** — payment method rows. `xmlColumns` marks
  `PaymentGatewayParameters` and `PaymentCheckoutParameters` as embedded XML
  so they serialize readably. `ShopAutoId` excluded.

  **IMPORTANT:** Payment *gateway credentials* (API keys, merchant IDs) live
  inside `PaymentGatewayParameters` XML per env. Customers MUST exclude the
  credential-holding XML elements or manage them via Azure Key Vault after
  deploy. See gap `D2-CREDENTIAL-EXCLUSION`.

- **EcomShippings** — shipping method rows. Same XML structure, same
  gateway-credentials caveat.

- **EcomMethodCountryRelation** — which payment/shipping method is available
  in which country.

### Order state machine

- **EcomOrderFlow** — order flow definitions (e.g., "Standard B2C").
  `nameColumn: OrderFlowName` for readable file names.
- **EcomOrderStates** — state definitions ("Cart", "Placed", "Paid",
  "Shipped"). `nameColumn: OrderStateName`.
- **EcomOrderStateRules** — allowed transitions between states.

### URL rewriting

- **UrlPath** — redirect rules (e.g., `products-*` → Shop page). One row in
  Swift 2.2, but customers often add more; baseline captures whatever DW ships.

## What's NOT in the baseline (and why)

| Excluded | Reason |
|----------|--------|
| `AccessUser` | Mix of structural groups and tenant-demo data. Cannot be cleanly separated without WHERE-clause support in SqlTable predicates. See `D2-SQL-WHERE`. |
| `EcomProducts` | Customer catalog content. Also contains contamination (1091 empty names, 16 variant orphans for deleted PROD106). |
| `EcomGroups` | Customer-managed product taxonomy. |
| `EcomDiscount`, `EcomDiscountTranslation` | Customer-managed promotions. |
| `EcomProductCategoryFieldValue`, `EcomVariantOptionsProductRelation` | Product-attached data. Follows products. |
| Area 26 "Digital Assets Portal" | Separate product, not part of the Swift 2.2 storefront baseline. |
| Newsletter/system email **content** | Body copy is seed content. Page structure is captured via Content predicate; end-user editable body text will be overwritten on deploy — **known issue**, see gap `D2-SEED-CONTENT-MODE`. |

## Pre-existing source-data bugs caught by Phase 37 validators

The Swift 2.2 database as shipped contains several stale-data issues that Phase 37's
validators surface at serialize time. All are upstream data problems, not serializer
bugs. Cleaned on 2026-04-21 via `tools/swift22-cleanup/` (see that directory's
README for run order).

### Three column-name mistakes (caught by SqlIdentifierValidator)

The pre-Phase-37 `swift2.2-baseline.json` referenced three columns that do not exist
on the actual Swift 2.2 schema. SqlIdentifierValidator (Phase 37-03) now catches
these at config load before any SQL is generated:

| Wrong name (old baseline) | Correct name | Location |
|---------------------------|--------------|----------|
| `VatName`                 | `VatGroupName` | EcomVatGroups predicate `nameColumn` |
| `ShopAutoId`              | `PaymentAutoId` | EcomPayments predicate `excludeFields` |
| `ShippingParameters`      | `ShippingServiceParameters` | EcomShippings predicate `xmlColumns` |

Fixed in the current `swift2.2-combined.json`. Any future baseline author who
mistypes a column name will be rejected at config load with a validator error
pointing at the exact `predicate.name` and `field` — no silent-drop behavior.

### Five orphan page references (caught by BaselineLinkSweeper)

Five `Default.aspx?ID=N` references in the source data point at pages that no
longer exist in area 3 (Swift 2). Cleaned by `tools/swift22-cleanup/01-null-orphan-page-refs.sql`:

| Page ID | Where referenced | Reason |
|---------|------------------|--------|
| 8308    | Paragraph item-field links | page deleted from DB |
| 149     | Paragraph item-field links | page deleted from DB |
| 15717   | Paragraph item-field links | paragraph anchor (not an orphan — see below) |
| 295     | Paragraph item-field links | page deleted from DB |
| 140     | Paragraph item-field links | page deleted from DB |

### Orphan areas + soft-deleted pages (found via direct DB analysis)

- **267 orphan-area pages** — pages whose `PageAreaID` refers to an Area that no
  longer exists. Deleted by `tools/swift22-cleanup/03-delete-orphan-areas.sql`.
- **238 soft-deleted pages** — pages with `PageDeleted = 1` still present in the
  DB. Deleted by `tools/swift22-cleanup/04-delete-soft-deleted-pages.sql`.

Both scripts are idempotent: re-running on an already-cleaned DB affects zero
rows. The counts above are from the 2026-04-21 run against the shipped Swift 2.2
instance at port 54035.

### Temporary: `acknowledgedOrphanPageIds: [15717]` on the Content predicate

After running the cleanup scripts, one `Default.aspx?ID=4897#15717` reference
remains. `15717` is a **paragraph anchor**, not a page ID — BaselineLinkSweeper
incorrectly treats it as a page reference. Phase 38 B.5 fixes the sweeper to
validate paragraph anchors against the serialized paragraph set. Until B.5 lands,
the `acknowledgedOrphanPageIds: [15717]` entry on the Content predicate is required
to prevent the sweep from escalating the false-positive. **Remove this entry once
B.5 ships** (tracked in Phase 38 D-38-16 as the final cleanup step before restoring
`strictMode: true` default).

## Azure deployment assumptions

This baseline assumes the following Azure setup:

1. **Azure App Service** hosts Dynamicweb with the serializer plugin deployed
   alongside the site.
2. **Azure SQL Database** holds the DW schema. The baseline YAML is
   deserialized into a freshly-provisioned DB as part of the first deploy.
3. **Azure Key Vault** holds payment gateway credentials, storage connection
   strings, and other secrets. These are injected as App Service config at
   startup — never in the baseline.
4. **Azure DevOps / GitHub Actions** runs the deploy pipeline. The deserialize
   step runs in the same pipeline stage as code deployment, so code + data
   stay in sync.
5. **Subsequent deploys** re-apply the baseline, which **overwrites** target
   data (source-wins). This is correct for pure deployment data in this
   baseline; not currently safe for seed content (see gaps).

## Known gaps (input to D2)

All of these are tracked in the D2 improvement plan:

- `D2-SEED-CONTENT-MODE` — No "apply-if-absent" deserialize. Customer page
  copy will be overwritten. **Blocks this baseline from being deployed to a
  live prod without a manual review step.**
- `D2-SQL-WHERE` — SqlTable predicates can't filter rows. Forces
  all-or-nothing inclusion for tables that mix structural + tenant data
  (AccessUser, EcomGroups).
- `D2-CREDENTIAL-EXCLUSION` — No way to exclude nested XML children by name
  pattern or schema. Payment gateway credentials can only be excluded by
  full XML-element name (`excludeXmlElements`).
- `D2-INACTIVE-PAGES` — No `includeInactive` flag on Content predicates.
  Currently the Content provider includes inactive pages silently.

See: `.planning/phases/{phase#}-d2-baseline-improvements/PLAN.md` (created
at end of test session) for the full improvement plan.

## How to use this baseline

### Serialize (capture current state of Swift 2.2 → YAML)

```bash
# Swap the config into the Swift 2.2 instance
cp src/DynamicWeb.Serializer/Configuration/swift2.2-baseline.json \
   wwwroot/Files/Serializer.config.json

# Trigger serialize via admin UI (Settings → Serializer → Run Serialize)
# Or via API:
curl -X POST https://localhost:54035/Admin/TokenAuthentication/authenticate \
  -d '{"username":"Administrator","password":"Administrator1"}'
# ... then use bearer token to call the serialize endpoint
```

Output lands in `wwwroot/Files/Serializer/SerializeRoot/`. Commit that tree
to the repo under `baselines/Swift2.2/` alongside this config.

### Deserialize (apply baseline → fresh Azure SQL)

1. Ensure DW schema is in place (run DW installer or schema scripts).
2. Drop `swift2.2-baseline.json` into the new instance's
   `wwwroot/Files/Serializer.config.json`.
3. Copy the baseline YAML tree into `wwwroot/Files/Serializer/SerializeRoot/`.
4. Trigger deserialize via admin UI or API. Watch the log for warnings
   (especially cache-invalidation warnings — see `SerializerOrchestrator.cs:156`
   for the guardrail I added in commit `a3d3140`).
5. Verify the frontend renders Customer Center, cart, and sign-in pages.

### Upgrade workflow

Changed a shop definition in dev?

1. Run serialize on the dev instance with this baseline config.
2. Commit the diff in `baselines/Swift2.2/`.
3. Promote through test → QA → prod by running deserialize in each pipeline stage.
4. Payment credentials and domains don't move — they stay in Key Vault per env.
