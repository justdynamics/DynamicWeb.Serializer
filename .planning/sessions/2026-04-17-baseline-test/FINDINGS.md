# Findings — Swift 2.2 Baseline Test

Accumulating pain points as I go. Each finding becomes input for D2.

## Severity scale

- **P0** — blocks the test entirely; fix now
- **P1** — works around-able but genuinely inconvenient
- **P2** — cosmetic / nice-to-have
- **DESIGN** — a structural gap, not a bug

---

## F-01: Seed-content vs deployment-data split (DESIGN)

**The headline finding.** Confirmed by user upfront.

The serializer has one deserialize mode: `source-wins` (overwrite target with
source). This is correct for deployment data (payment methods, currencies, shop
structure). It is *wrong* for seed content (Customer Center page body text,
FAQ copy, email templates) — deserializing a baseline onto a live prod env
wipes customer edits.

**Gap:** No `apply-if-absent` mode. No way to say "this predicate's rows/pages
are one-time seeds, skip if the target already has them."

Proposed solutions in D2:
- Per-predicate `deserializeMode: source-wins | if-absent | skip`
- Or: separate top-level `seed:` section in config (only runs on first deploy)
- Or: hash-based idempotence (don't touch rows whose target hash differs from
  the last-known source hash — customer edits are preserved)

---

## F-02: Swift 2.2 contamination and the `excludes` problem (DESIGN)

DW ships Swift 2.2 contaminated — test pages, orphaned pages in deleted areas,
tenant-demo user groups, failed-import product remnants, 140 empty-ItemId
GridRows.

The Content predicate handles path-based excludes well (`/Home`, `/Home-Machines`).
But SqlTable predicates are **all-or-nothing** — can't exclude `AccessUser WHERE
UserName NOT IN (...)` or `EcomProducts WHERE ProductId NOT LIKE 'Imported%'`.

This forces the baseline to exclude entire tables that contain both structural
data (Admin role, Editors role) and demo data (Nordic Media Group, Demo Users).

**Gap:** SqlTable predicates need a `where` clause or `includeFilter` / `excludeFilter`
with column=value matching. Proposed D2 spec:

```json
{
  "name": "AccessUser-Roles",
  "providerType": "SqlTable",
  "table": "AccessUser",
  "where": "AccessUserType = 3 AND AccessUserUserName IN ('Admin','PIMadmin')"
}
```

Security concern: see SEED-002 (SQL identifier whitelist) — a `where` clause
escalates the attack surface. Mitigation: parameterize values, allowlist column
names, disallow semicolons/comments.

---

## F-03: Home Machines — inactive pages still serialized? (P2, tentative)

Page 4897 "Home Machines" is `PageActive=0`. It's reachable at path level but
marked inactive. Current Content predicate does not filter by Active flag.

Low priority — user may legitimately want inactive pages (draft content) in
the baseline. But worth exposing as an option: `includeInactive: true | false`
on Content predicates. Default false would match intuition.

---

## F-04: Serializer does NOT clean stale output from prior runs (P1 / DESIGN)

**Observation:** First serialize run with the new baseline config wrote to
`SerializeRoot/` but left directories from the prior config in place. The
prior config didn't exclude `/About`, `/Posts`, `/Home Machines`, `/New
Serialized Page` — after re-running with a config that DOES exclude them,
the output on disk still contained all four stale subtrees because the
serializer never removed them.

**Impact:** If a deployment pipeline commits the SerializeRoot tree after a
config-change that tightens excludes, stale files remain in the repo. Worse,
on a fresh target the deserializer will re-create excluded pages from those
stale files. The exclude only works correctly if the output tree is always
manually wiped before serialize — which no CI/CD pipeline will remember.

**Proposed:** Serialize should write to a temp dir and swap, OR should track
files-written-this-run in a manifest and delete any file not in the manifest
post-swap. The latter is safer (preserves unrelated files if SerializeRoot is
shared). Manifest approach is how build tools handle this (e.g., webpack's
`cleanOutdated` option).

## F-05: SqlTable `excludeFields` doesn't cover payment credentials (P1 / DESIGN)

**Observation:** `EcomPayments` has columns like `PaymentGatewayMD5Key`,
`PaymentMerchantNum`, `PaymentGatewayId` that hold per-environment credentials.
These are row-level fields (not embedded XML), so `excludeXmlElements` doesn't
apply. The only way to exclude them is `excludeFields: ["PaymentGatewayMD5Key",
...]`. The baseline config omits these excludes because Swift 2.2 ships with
empty values — but a customer who fills them in pre-serialize will leak
credentials to their git repo.

**Proposed:** Ship a **default credential-column registry** per well-known
DW table. When the predicate targets `EcomPayments`, auto-exclude a known
list of credential columns unless the user explicitly opts in. Same for
`EcomShippings` (ShippingServiceParameters XML holds DHL/UPS tokens),
shop-specific API settings, etc.

## F-06: EcomShops env-specific search-index columns leak (P2)

**Observation:** `ShopIndexRepository: "ProductsBackend"` and `ShopIndexName:
"Products.index"` serialize through. These are env-specific pointers to the
search infrastructure (different on Azure dev vs prod). Can be excluded via
`excludeFields`, but nothing in the docs flags this.

**Proposed:** Same fix as F-05 — a default "env-specific column registry"
for `EcomShops` that callers can override.

## F-07: UrlPath and cross-environment page ID references (P1)

**Observation:** `UrlPathRedirect: "Default.aspx?ID=5862"` contains a hard
numeric page ID. On CleanDB, the numeric ID for the Shop page will likely be
different. The SqlTable provider has no InternalLinkResolver pass for these.
Also `UrlPathAreaId: 3` — if CleanDB's Swift 2 area has a different AreaId,
this breaks.

Also: `UrlPathVisitsCount: 0` — runtime counter, should not be in baseline
(will overwrite live counts on deploy). This is another credentials-style
"runtime-only field" that needs exclusion.

**Proposed:**
- Extend InternalLinkResolver to handle `Default.aspx?ID=N` patterns in
  SqlTable string columns, not just ContentProvider page item fields.
- Add `UrlPathVisitsCount` to a runtime-column registry that auto-excludes.
- For `UrlPathAreaId` — needs cross-env area identity (probably AreaId →
  Area GUID lookup at serialize, GUID → AreaId resolve at deserialize).

## F-08: Baseline content references user groups (CSR, AuthenticatedFrontend) that aren't in baseline (P1)

**Observation:** Page permissions serialize the owner by group name:
`owner: "CSR", ownerType: group`. On a CleanDB target that lacks the CSR
group (because AccessUser is excluded from baseline), PermissionMapper must
either skip silently, warn, or fail.

Need to verify what it does on the actual deserialize.

**Proposed:**
- Add a predicate for "essential user groups" that whitelists by name:
  `AccessUser WHERE AccessUserType=2 AND AccessUserUserName IN ('Admin',
  'Editors', 'CMS Editors', 'PIM Editors', 'CSR', 'Employees')`. Requires
  `where` clause support (see F-02 / D2-SQL-WHERE).
- OR: PermissionMapper must degrade gracefully with a warning when the
  owner can't be resolved, and strict-mode should convert to an error
  (ties into SEED-001).

## F-09: EcomVatGroups with empty VatName produce `_unnamed.yml` and `_unnamed [d41d8c].yml` files (P2)

**Observation:** I set `nameColumn: "VatName"` on EcomVatGroups for readable
filenames. But some rows have empty/null VatName, producing files named
`_unnamed.yml` and `_unnamed [d41d8c].yml` (with a hash suffix to disambiguate).

The hash-suffix mechanism works (no collisions), but the filename provides
zero semantic information. For baseline review in PR, these unnamed rows
are opaque.

**Proposed:** Fall back to the PK column when `nameColumn` is empty/null for
a given row. So `VatId=5` with empty VatName becomes `5.yml` instead of
`_unnamed.yml`.

## F-10: Most cache service type names can't be resolved (P1 / DESIGN)

**Observation:** The deserialize log shows:
```
Cache type not found: Dynamicweb.Ecommerce.International.CountryService (skipping)
Cache type not found: Dynamicweb.Ecommerce.Orders.PaymentService (skipping)
```
...and similar for every service listed in the baseline's `serviceCaches`.
The string-based `serviceCaches` config is essentially dead code right now —
the type-name→runtime-type lookup fails silently for most entries. Already
noted in the existing memory `project_dw_area_cache.md` about AreaService.

**Impact:** Post-deploy, customers must manually restart the app or clear
caches another way for the updated currencies/payments/etc. to become visible.
Silent failure looks like success in the log summary.

**Proposed:**
- A hand-coded service-name registry keyed by short name (e.g.
  "CountryService" → `typeof(Dynamicweb.Ecommerce.International.CountryService)`)
  populated via a static map, not AddInManager
- OR a direct-ClearCache mechanism following the AreaService pattern from
  the existing memory
- Either way: make it an ERROR (not silent skip) when a name can't be
  resolved, so misconfig surfaces

## F-12: Content deserialize hard-fails on any target-schema column mismatch (FIXED IN THIS RUN)

**Observation:** Swift 2.2 has Area columns `AreaHtmlType`, `AreaLayoutPhone`,
`AreaLayoutTablet` that CleanDB lacks. The old deserializer built `UPDATE
[Area] SET` with every source column — SQL Server rejected with "Invalid
column name", and the entire content deserialize (all pages, gridrows,
paragraphs) was skipped.

**Fix landed (commit `f0bfbba`):** `GetTargetAreaColumns()` caches target
schema once per run; `WriteAreaProperties` / `CreateAreaFromProperties`
now skip columns missing on target and log one warning per column.

**Still open (carry to D2):** the same pattern likely applies to ItemType
tables, Page extension tables, and Paragraph tables. A comprehensive fix
needs to factor schema-tolerance into every write path, not just Area.

## F-14: YamlDotNet returns date/bool/int as strings, SQL conversion was fragile (FIXED IN THIS RUN)

**Observation:** `Dictionary<string, object>` round-trips through YamlDotNet
as strings for scalar values (no type inference without per-field mapping).
SQL Server's implicit conversion of `"2021-01-04T15:53:06.0730000"` into
`datetime` failed: "Conversion failed when converting date and/or time
from character string."

**Fix landed (commit `f0bfbba`):** `CoerceForColumn(name, value)` looks up
the target column's `DATA_TYPE` and, when the incoming value is a string,
parses to DateTime / bool / int / long as appropriate. Falls back to
passing the raw value (which preserves current behavior for columns whose
types the helper doesn't know about).

**Still open (carry to D2):** same comment as F-12 — only the Area write
path is covered. Page/Paragraph/ItemType writes likely have the same
latent issue; it didn't surface during this run because those go through
DW's C# APIs (`SavePage`, `ItemService`), not raw SQL. Worth auditing.

## F-15: Missing design templates / grid row JSON files (P1 / DESIGN)

**Observation:** Deserialize logged 3+ warnings about missing templates:
```
WARNING: Page layout template 'Swift-v2_PageNoLayout.cshtml' not found...
WARNING: Grid row definition '1ColumnEmail.json' not found...
WARNING: Grid row definition '2ColumnsEmail.json' not found...
```

CleanDB's `/Files/Templates/Designs/Swift-v2/` tree is not identical to
Swift 2.2's. Swift 2.2 has custom/newer templates that CleanDB lacks.

**Impact for Azure deploy:** The baseline defines content that references
template files on disk. Those files are NOT part of the baseline. They must
be copied alongside as part of the code deploy (or an asset sync).

**Proposed:** D2 item — **template dependency manifest**. On serialize,
scan all `layout`, `template`, `moduleSystemName` references in content
and record the template paths used. On deserialize, validate each path
exists before writing pages. Missing templates should be a WARNING with
instructions: "add template X to Files/Templates/Designs before re-running
deserialize".

Longer-term: a `Templates` predicate that serializes the actual template
files (cshtml + json + assets) into the baseline. Scope creep, but fits
the "baseline is everything needed to recreate the site" promise.

## F-17: Cross-environment page ID references resolve lossy (P1)

**Observation:** ContentDeserializer logged ~10 `WARNING: Unresolvable page
ID NNNN in link` — `Default.aspx?ID=NNNN` references inside item fields
and shortcuts where the numeric page ID no longer matches on target (9579,
9575, 6869, 8308, 107, 9536, 9633, 9568, ...).

Some of these are real (e.g. `107` was Swift 2.2's About page, excluded
from baseline, so CleanDB can't find it). Others are contamination
(pointing to deleted pages). Some are stale IDs from when CleanDB pages
had different numbers.

**Proposed (already captured in F-07 for UrlPath — broaden here):**
- Log the *source* of each unresolvable link (which page/paragraph/item
  field contains it) so the baseline owner can fix at source
- Offer a "strict mode" where unresolvable links fail the deserialize
  (ties into SEED-001 strict-mode deserialize)
- For the baseline, a sweep-pass to identify and remove `Default.aspx?ID=N`
  links pointing to excluded pages before commit (would belong in a
  pre-commit hook or a new `/Admin/Api/SerializerValidateBaseline` endpoint)

## F-18: FK constraint re-enable fails on contaminated source data (P2)

**Observation:** `WARNING: Could not re-enable FK constraints for
[EcomShopGroupRelation]: The ALTER TABLE statement conflicted with the
FOREIGN KEY constraint "DW_FK_EcomShopGroupRelation_EcomShops"`. The
relation's ShopId `SHOP19` doesn't exist in EcomShops on target — but
it ALSO doesn't exist in source (per Swift 2.2 analysis report, SHOP19
is a pre-existing orphan).

**Impact:** The table is left with FKs disabled on target after a
successful-looking deserialize. Subsequent writes bypass referential
integrity until someone notices and manually re-enables.

**Proposed:** Pre-write validation — for every SqlTable row, verify FK
targets exist in the YAML tree (or current DB) before issuing INSERT/MERGE.
Fail-fast with a clear error when contaminated. Today the orchestrator
silently ships broken data.

## F-19: Baseline size (1570 files, ~1.5 MB text) — reviewable? (P2)

**Observation:** A PR that diffs the whole `baselines/Swift2.2/` tree is
~1570 files. A developer reviewing a baseline change cannot read this
line-by-line.

**Proposed (nice-to-have):**
- A `serialize --summary` mode that writes a single `BASELINE-DIFF.md`
  alongside the YAML, listing changes (rows added/updated/removed per
  predicate) in human-readable form
- PR template automation: GitHub Action that runs a diff-vs-main and
  comments the summary on the PR

