# Phase 31: Predicate UI Enhancement - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning
**Mode:** Auto-generated (autonomous mode)

<domain>
## Phase Boundary

Admin UI predicate edit screens expose all new v0.5.0 config fields (excludeFields, xmlColumns, excludeXmlElements) for visual configuration. This is the UI layer for features built in Phases 27-30.

</domain>

<decisions>
## Implementation Decisions

### UI Controls
- excludeFields: Textarea input where users type comma-separated or newline-separated field names to exclude
- xmlColumns: Textarea input for SqlTable predicates specifying which columns contain XML (only visible when providerType is SqlTable)
- excludeXmlElements: Textarea input for XML element names to strip from embedded XML blobs
- All three fields are optional — empty means "no filtering"

### DW Admin UI Patterns (from feedback_dw_patterns.md)
- Use DW CoreUI patterns: TextArea for multi-value inputs
- Existing predicate edit screen at SerializerPredicateEditScreen.cs
- Follow existing patterns for field visibility based on providerType (Content vs SqlTable)
- MapPath for file paths, WithReloadOnChange for dialogs, Select value types

### Conditional Visibility
- xmlColumns only shows for SqlTable predicates (not Content)
- excludeFields and excludeXmlElements show for all provider types
- Provider type already determines which fields are visible in the existing predicate edit screen

### Claude's Discretion
- Exact UI layout and field ordering
- Label text and help descriptions for each field
- Whether to use TextArea or a more structured multi-value input component

</decisions>

<code_context>
## Existing Code Insights

### Key Files
- SerializerPredicateEditScreen.cs — existing predicate edit screen (add new fields here)
- SerializerPredicateListScreen.cs — predicate list (may need column updates)
- AddPredicateCommand.cs / UpdatePredicateCommand.cs — command handlers for save
- ConfigLoader.cs — already has three-class mapping for all new fields (Phases 27-28)

### Established Patterns
- DW admin screens use ContentArea, Dialog, and form controls
- Predicate edit screen already has conditional fields based on providerType
- Save commands read form values and update ProviderPredicateDefinition

</code_context>

<specifics>
## Specific Ideas

No specific requirements beyond making the three new config fields editable in the admin UI.

</specifics>

<deferred>
## Deferred Ideas

None.

</deferred>
