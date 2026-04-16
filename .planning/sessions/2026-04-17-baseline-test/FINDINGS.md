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

(Findings F-04+ will be added as the test proceeds)
