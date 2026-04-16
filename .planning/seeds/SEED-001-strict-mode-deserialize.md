---
id: SEED-001
status: dormant
planted: 2026-04-17
planted_during: v0.4.0 (completed)
trigger_when: Serializer is wired into CI/CD or automated deployment pipelines
scope: Small
---

# SEED-001: Strict-mode deserialize (fail on first error)

## Why This Matters

`ContentDeserializer` currently catches + logs + continues in ~6 places (plus
~13 more across providers/AdminUI commands). That behavior is correct for
interactive use — a partial sync with warnings is often what the operator wants
when iterating locally.

It is *wrong* for automation. A CI job that runs deserialize will exit 0 on a
silently-skipped page, a permission mapping that didn't resolve, or a
service-cache invalidation that was never wired (now surfaced as a warning by
the recent orchestrator change, but still doesn't fail the run). The build
passes, the deployment "succeeds", and the drift goes undetected.

A `--strict` flag flips the posture: log the warning *and* throw. The orchestrator
aggregates into a non-zero exit, giving CI a real signal.

## When to Surface

**Trigger:** When the serializer is wired into CI/CD or automated deployment pipelines.

Present this seed during `/gsd-new-milestone` when the milestone scope mentions
any of:

- CI/CD integration, GitHub Actions, automated deploys
- Release/deployment workflow hardening
- Production-facing sync (not just dev-box experimentation)
- Any scenario where a silent partial deserialize would cause drift to go undetected

## Scope Estimate

**Small** — a few hours.

- Add `StrictMode` bool to `SerializerOrchestrator.DeserializeAll(...)` signature
- Thread through to `ContentProvider` / `SqlTableProvider` / `ContentDeserializer`
- Convert the ~6 catch-and-continue sites in `ContentDeserializer` to throw-after-log when strict
- Add config key (`strictMode`) in `Serializer.config.json` and corresponding admin UI checkbox
- Add `--strict` to any CLI/API entry point
- Document trade-off in README (strict for CI, lenient for interactive)
- Unit test: one permission-resolution failure produces non-zero exit under strict, zero under lenient

## Breadcrumbs

Catch-and-continue sites worth converting:

- `src/DynamicWeb.Serializer/Serialization/ContentDeserializer.cs` — 6 `catch (Exception)` sites
- `src/DynamicWeb.Serializer/Providers/Content/ContentProvider.cs` — 2 catches
- `src/DynamicWeb.Serializer/Providers/SerializerOrchestrator.cs` — 2 catches (cache invalidation best-effort, schema sync)
- `src/DynamicWeb.Serializer/Serialization/PermissionMapper.cs` — permission resolution warnings
- `src/DynamicWeb.Serializer/Serialization/InternalLinkResolver.cs` — unresolved link warnings

Related config surface:

- `src/DynamicWeb.Serializer/Configuration/SerializerConfiguration.cs`
- `src/DynamicWeb.Serializer/AdminUI/Screens/SettingsEditScreen.cs`

## Notes

Related to the recent `CacheInvalidator == null` warning added to
`SerializerOrchestrator.cs` — that change surfaces the misconfiguration as a log
line, but under `strictMode: true` it should also fail the run.

Lenient remains the default. Strict is opt-in so interactive users don't
regress.
