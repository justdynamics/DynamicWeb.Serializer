---
phase: 23-full-page-properties-navigation-settings
plan: 01
subsystem: serialization
tags: [yaml, dto, page-properties, navigation-settings, ecommerce, content-mapper]

requires:
  - phase: prior-milestones
    provides: SerializedPage DTO, ContentMapper.MapPage(), YamlDotNet config, test infrastructure
provides:
  - 4 sub-record DTOs (Seo, UrlSettings, Visibility, NavigationSettings)
  - SerializedPage extended with ~30 page properties
  - ContentMapper.MapPage() populates all new properties from DW Page
  - MapNavigationSettings() helper with UseEcomGroups guard
affects: [23-02, content-deserializer, link-resolution]

tech-stack:
  added: []
  patterns: [sub-record DTOs for logical property groupings, boolean init defaults for backward compat, nullable DateTime for optional dates, enum-to-string serialization via ToString()]

key-files:
  created:
    - src/DynamicWeb.Serializer/Models/SerializedSeoSettings.cs
    - src/DynamicWeb.Serializer/Models/SerializedUrlSettings.cs
    - src/DynamicWeb.Serializer/Models/SerializedVisibilitySettings.cs
    - src/DynamicWeb.Serializer/Models/SerializedNavigationSettings.cs
  modified:
    - src/DynamicWeb.Serializer/Models/SerializedPage.cs
    - src/DynamicWeb.Serializer/Serialization/ContentMapper.cs
    - tests/DynamicWeb.Serializer.Tests/Models/DtoTests.cs
    - tests/DynamicWeb.Serializer.Tests/Infrastructure/YamlRoundTripTests.cs
    - tests/DynamicWeb.Serializer.Tests/Fixtures/ContentTreeBuilder.cs

key-decisions:
  - "Sub-objects (Seo, UrlSettings, Visibility) always serialized; NavigationSettings only when UseEcomGroups=true"
  - "Allowclick, Allowsearch, ShowInSitemap, ShowInLegend default to true matching DW Page field initializers"
  - "ActiveFrom/ActiveTo as nullable DateTime to distinguish unset from explicit values"
  - "PermissionType added as flat int property on SerializedPage"

patterns-established:
  - "Sub-record pattern: group related properties into sub-records for YAML readability"
  - "Boolean init defaults: match DW source field initializers to prevent backward compat breakage"
  - "Conditional sub-object serialization: NavigationSettings null when feature disabled"

requirements-completed: [PAGE-01, ECOM-01]

duration: 3min
completed: 2026-04-03
---

# Phase 23 Plan 01: Page Properties + Navigation Settings DTO and Mapper Summary

**4 sub-record DTOs + 16 flat properties on SerializedPage, ContentMapper.MapPage() extended to serialize all ~30 missing page properties including ecommerce NavigationSettings**

## Performance

- **Duration:** 3 min
- **Started:** 2026-04-03T17:00:19Z
- **Completed:** 2026-04-03T17:03:19Z
- **Tasks:** 2
- **Files modified:** 9

## Accomplishments
- Created 4 sub-record DTOs organizing ~30 page properties into logical groups (SEO 7 props, URL 4 props, Visibility 3 props, Navigation 8 props)
- Extended SerializedPage with 16 flat scalar properties + 4 sub-object references with correct DW-matching defaults
- Extended ContentMapper.MapPage() to populate all new properties from DW Page objects
- Added MapNavigationSettings() helper that returns null when UseEcomGroups is false
- 9 new unit tests covering DTO defaults, YAML round-trip for all sub-objects, and backward compatibility

## Task Commits

Each task was committed atomically:

1. **Task 1: Create sub-record DTOs and extend SerializedPage** - `a0e5e80` (feat)
2. **Task 2: Extend ContentMapper.MapPage()** - `e2f938f` (feat)

## Files Created/Modified
- `src/DynamicWeb.Serializer/Models/SerializedSeoSettings.cs` - SEO sub-record (MetaTitle, MetaCanonical, Description, Keywords, Noindex, Nofollow, Robots404)
- `src/DynamicWeb.Serializer/Models/SerializedUrlSettings.cs` - URL settings sub-record (UrlDataProviderTypeName, UrlDataProviderParameters, UrlIgnoreForChildren, UrlUseAsWritten)
- `src/DynamicWeb.Serializer/Models/SerializedVisibilitySettings.cs` - Visibility sub-record (HideForPhones, HideForTablets, HideForDesktops)
- `src/DynamicWeb.Serializer/Models/SerializedNavigationSettings.cs` - Navigation settings sub-record (UseEcomGroups, ParentType, Groups, ShopID, MaxLevels, ProductPage, NavigationProvider, IncludeProducts)
- `src/DynamicWeb.Serializer/Models/SerializedPage.cs` - Extended with 16 flat properties + 4 sub-object references
- `src/DynamicWeb.Serializer/Serialization/ContentMapper.cs` - MapPage() extended + MapNavigationSettings() helper
- `tests/DynamicWeb.Serializer.Tests/Models/DtoTests.cs` - 3 new tests (boolean defaults, nullable DateTime, null sub-objects)
- `tests/DynamicWeb.Serializer.Tests/Infrastructure/YamlRoundTripTests.cs` - 6 new tests (flat props, Seo, UrlSettings, Visibility, NavigationSettings, backward compat)
- `tests/DynamicWeb.Serializer.Tests/Fixtures/ContentTreeBuilder.cs` - BuildPageWithAllProperties() helper

## Decisions Made
- Sub-objects (Seo, UrlSettings, Visibility) always serialized for all pages; NavigationSettings only when UseEcomGroups=true
- Allowclick, Allowsearch, ShowInSitemap, ShowInLegend default to true matching DW Page field initializers
- ActiveFrom/ActiveTo as nullable DateTime to distinguish "not set in YAML" from explicit values
- PermissionType added as flat int on SerializedPage (simple enum-backed integer)
- DisplayMode and ParentType serialized as string via ToString() for YAML readability

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None - all properties are fully wired to DW Page source properties via ContentMapper.

## Next Phase Readiness
- DTO and mapper complete; plan 23-02 can implement ContentDeserializer property assignment and link resolution
- All sub-objects round-trip through YAML correctly
- Backward compatibility verified: old YAML without new properties deserializes with correct DW defaults

## Self-Check: PASSED

- All 6 key source files: FOUND
- Commit a0e5e80: FOUND
- Commit e2f938f: FOUND
- 26/26 DtoTests + YamlRoundTripTests: PASSED

---
*Phase: 23-full-page-properties-navigation-settings*
*Completed: 2026-04-03*
