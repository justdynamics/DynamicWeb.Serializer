---
status: partial
phase: 39-seed-mode-field-level-merge
source: [39-VERIFICATION.md, 39-03-SUMMARY.md]
started: 2026-04-23
updated: 2026-04-23
---

## Current Test

[awaiting human testing]

## Tests

### 1. D-15 live Seed-merge acceptance against empty DB
expected: On a freshly-prepared empty CleanDB target, running Deploy (swift2.2-combined Deploy predicate) → tweaking a known target column (e.g. `MenuText` on a known Page) in SSMS → running Seed (swift2.2-combined Seed predicate) produces per-row `Seed-merge: <identity> — N fields filled, M left` log lines, zero `Seed-skip:` lines, customer tweak preserved byte-for-byte in target DB, and `Mail1SenderEmail` populated inside `EcomPayments.PaymentGatewayParameters` XML. A second Seed pass writes zero fields (D-09 intrinsic idempotency).
result: [pending]

## Summary

total: 1
passed: 0
issues: 0
pending: 1
skipped: 0
blocked: 0

## Gaps

(none yet — run the test above when operator prepares an empty DB)
