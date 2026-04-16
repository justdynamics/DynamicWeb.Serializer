# Swift 2.2 Baseline Test — Survey Findings

**Date:** 2026-04-17
**Session:** Autonomous baseline test
**DB:** Swift-2.2 on localhost\SQLEXPRESS

## Swift 2.2 DB state

Swift 2.2 as shipped by DW is CONTAMINATED — it's a demo DB with orphaned content,
test artifacts, and tenant-specific demo data mixed with legitimate baseline scaffolding.

### Areas (from `Area` table)

| AreaId | Name | Status |
|--------|------|--------|
| 3 | Swift 2 | VALID — target of baseline |
| 26 | Digital Assets Portal | VALID but separate product — NOT in baseline |

267 pages orphaned across 5 deleted areas (11, 12, 13, 25, 27) — automatically
excluded by path filter since they're not reachable from a valid Area.

### Top-level pages in Area 3 (124 pages total)

| ID | Name | Active | Baseline verdict |
|----|------|--------|------------------|
| 111 | Sign in | 1 | DEPLOYMENT — auth scaffolding |
| 6860 | Header / Footer | 1 | DEPLOYMENT — site chrome |
| 6869 | Home | 1 | **EXCLUDE** — marketing content |
| 7837 | Newsletter Emails | 1 | SEED — email templates (include, flag for D2) |
| 4897 | Home Machines | 0 | **EXCLUDE** — marketing, inactive |
| 5863 | Product Components | 1 | DEPLOYMENT — product layout scaffolding |
| 7831 | System emails | 1 | DEPLOYMENT/SEED — order confirmation etc. |
| 8385 | Customer center | 1 | DEPLOYMENT + SEED (copy in item fields) |
| 5859 | Service Pages | 0 | DEPLOYMENT — cart/search service pages |
| 5862 | Shop | 1 | DEPLOYMENT — product list/detail page structure |
| 8421 | Shopping cart | 1 | DEPLOYMENT — cart flow |
| 107 | About | 1 | **EXCLUDE** — includes Contact (marketing) |
| 1688 | Search result page | 0 | DEPLOYMENT — search scaffolding |
| 8345 | Posts | 1 | **EXCLUDE** — blog (customer content) |
| 88 | Navigation | 1 | DEPLOYMENT — nav structure |
| 8379 | Page presets | 0 | DEPLOYMENT — page templates |
| 8451 | New Serialized Page | 1 | **EXCLUDE** — test artifact (created 2026-04-03) |

### Tables surveyed

Confirmed to exist in Swift-2.2:

- Content: Page (424), GridRow (1012), Paragraph (1210), UnifiedPermission (123)
- Ecom: Products (2051 — 53% empty names!), Groups (316), Countries, Currencies,
  Languages, Shippings, Payments, CountryText, VatGroups, VatCountryRelations,
  MethodCountryRelation, OrderFlow, OrderStates, OrderStateRules, Shops,
  ShopLanguageRelation, ShopGroupRelation
- Users: AccessUser (54 rows — mix of Admin/Editors/CSR **plus** tenant demo
  groups like "Digital Solutions Bureau", "Nordic Media Group")
- URL: UrlPath (1 redirect: `products-*` → Shop page)
- Schema: ItemType_Swift-v2_* (~80 tables — all handled by ContentProvider)

Confirmed to NOT exist (referenced elsewhere but not in this DB):

- AccessUserGroup (roles live in AccessUser with Type=2/3)
- Query / QueryPublisher (stored as items, handled by ContentProvider)
- UrlProvider / UrlRewrite / SystemFieldDefinition (code-side or stored as items)

### AccessUser tenant-contamination problem

AccessUser Type=2 mixes:

- **Structural groups** (Admin, Editors, CMS Editors, PIM Editors, CSR, Employees)
- **Tenant demo data** (Demo Users, Digital Solutions Bureau, Nordic Media Group,
  Tech Innovations, Enterprise Web Services)

Current serializer has no name-based filter for SqlTable predicates — it's
all-or-nothing per table. This makes AccessUser (and EcomGroups etc.) impossible
to partially serialize. **Logged as D2 finding.**

## Baseline scope decision

Given the above, the v1 baseline will:

1. Include Content (Area 3) with path-excludes for Home/Home-Machines/About/Posts/New-Serialized-Page
2. Include pure reference data: Countries, Currencies, Languages, VAT, OrderFlow, OrderStates
3. Include deployment-configurable Ecom: Payments, Shippings, MethodCountryRelation
4. Include Shop definitions with column-level excludes for env-specific URL/domain
5. Include UrlPath (1 redirect — tiny, deployment)
6. **EXCLUDE entirely**: AccessUser, EcomProducts, EcomGroups, EcomDiscount
   (all either customer-content or tenant-contaminated)

Users, roles, products, product groups, and discounts are explicitly out-of-scope
for the v1 baseline and documented in the reasoning doc.
