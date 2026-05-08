# Feature Landscape: Full Page Fidelity (v0.4.0)

**Domain:** DynamicWeb 10 page-level content serialization completeness
**Researched:** 2026-04-02

## Table Stakes

Features required for "full fidelity" -- deserialized pages must be functionally identical to source.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| SEO meta (Title, Description, Keywords, Canonical) | Pages lose search rankings without SEO | Low | 4 direct Page string properties |
| NavigationTag | Frontend nav templates target pages by tag | Low | Single string property |
| ShortCut + ShortCutRedirect | Page redirects break without these | Low | String + bool; ShortCut needs link resolution |
| Visibility flags (Hidden, AllowSearch, ShowInSitemap, AllowClick) | Content visibility policy | Low | 4 bool properties |
| Scheduled visibility (ActiveFrom, ActiveTo) | Timed content breaks without dates | Low | 2 DateTime? properties |
| SSL mode | HTTPS enforcement per page | Low | Int enum property |
| Robots directives (Noindex, Nofollow, Robots404) | SEO robots control | Low | 3 bool properties |
| URL config (UrlIgnoreForChildren, UrlUseAsWritten, ExactUrl) | URL routing breaks without these | Low | 2 bools + 1 string |
| Responsive visibility (HideForPhones/Tablets/Desktops) | Device-specific page hiding | Low | 3 bool properties |
| ColorSchemeId | Visual theming per page | Low | String property |
| PageNavigationSettings (ecom nav) | Ecom product navigation breaks without | Medium | 8-property nested object; ProductPage needs link resolution |
| Timestamp preservation (CreatedDate, UpdatedDate) | Audit trail, content age | Medium | No API; requires direct SQL read/write |

## Differentiators

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Area ItemType fields (header/footer) | Complete website structure sync | Medium | Page ID refs in item fields need GUID resolution |
| EcomProductGroupField schema sync | Ecom group custom columns exist before data import | Medium | Ordering dependency between provider runs |
| Link resolution in NavigationSettings.ProductPage | ProductPage refs survive cross-env | Low | Reuse InternalLinkResolver |
| Link resolution in ShortCut | Redirect targets survive cross-env | Low | Reuse InternalLinkResolver |

## Anti-Features

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| PagePassword | Security risk in YAML files on disk | Skip |
| Approval workflow state | Environment-specific runtime state | Skip |
| CopyOf references | Numeric IDs, rarely used | Skip |
| A/B experiment state (HasExperiment) | Transient runtime state | Skip |
| ~15 deprecated DW7/8 fields | Unused in DW10 Swift templates | Skip |
| PageCreationRules | Content creation workflow, env-specific | Skip |
| Template management fields (IsTemplate, TemplateDescription) | Not content data | Skip |

## Feature Dependencies

```
ShortCut serialization -----------> InternalLinkResolver (existing)
NavSettings.ProductPage ----------> InternalLinkResolver (existing)
Area ItemType fields -------------> InternalLinkResolver (for page ID refs)
EcomProductGroupField schema -----> SqlTableProvider (existing, phase 15)
Timestamp preservation -----------> New DirectSqlHelper class
All page properties --------------> SerializedPage DTO + ContentMapper
```

## MVP Recommendation

All features are in-scope for v0.4.0. Suggested ordering:

1. **Page properties batch** -- all ~28 missing scalar properties (LOW complexity, bulk of "fidelity")
2. **PageNavigationSettings** -- nested object with link resolution (MEDIUM)
3. **ShortCut link resolution** -- extend existing InternalLinkResolver
4. **Timestamp preservation** -- direct SQL helper
5. **Area ItemType fields** -- expand SerializedArea, GUID resolution
6. **EcomProductGroupField ordering** -- ensure schema before data
