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

The next five sections expand each of these.

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
