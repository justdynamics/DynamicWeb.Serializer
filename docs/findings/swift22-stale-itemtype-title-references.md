# Swift v2 Test Baseline — Stale ItemType "Field for title" References

**Status:** non-breaking observability finding
**Observed in:** Swift 2.2 reference install (DW 10.23.9, Swift-v2 design/ItemType set)
**Observed on:** 2026-04-21
**Runtime impact:** none — site renders normally; admin surfaces the stale reference but does not error
**Observability impact:** strict-mode baseline round-trip flags 47 warnings; masks a subset of genuine orphaned page references

---

## Summary

Several Swift-v2 ItemType definitions declare `fieldForTitle="Title"` on the `<item>` root element, but do **not** include a `<field systemName="Title">` in the field collection. The admin UI renders these ItemTypes' "Title for new items" setting with a `Field for title` dropdown pointing at a field that does not exist on the type.

This is visible in the Dynamicweb admin without any tooling. It is also surfaced by a strict-mode deserialization round-trip of the Swift 2.2 baseline into a clean database, which produces 47 "Unresolvable page ID … in link" warnings and 10 "Could not load PropertyItem for page …" warnings for related stale references.

The site runs fine with these references in place — the ItemType still works, paragraphs still render. The title-field lookup fails silently and DW falls back to the item's display name. The finding is about **data hygiene in the shipped Swift-v2 ItemType templates**, not about a functional regression.

## Concrete example — Swift-v2_Button

File: `wwwroot/Files/System/Items/ItemType_Swift-v2_Button.xml`

```xml
<item name="Button" systemName="Swift-v2_Button"
      fieldForTitle="Title"    <!-- declares a "Title" field as the item title -->
      title="" inherits="">
  <fields>
    <field systemName="FirstButton" ... />     <!-- FirstButton -->
    <field systemName="SecondButton" ... />    <!-- SecondButton -->
    <!-- no Title field -->
  </fields>
  ...
</item>
```

The `fields` collection holds only `FirstButton` and `SecondButton`. No field is named `Title`. When the admin UI reads the ItemType settings, the "Field for title" dropdown shows "Title" — a value that cannot be selected because the field does not exist on the type.

### How to reproduce in admin

1. In the Swift 2.2 admin, go to **Settings → Item types → Swift-v2_Button** (or equivalent navigation).
2. Open the **Settings** tab for the ItemType.
3. Inspect **Title for new items → Field for title**: it reads `Title`, but the field picker's choices are `FirstButton` and `SecondButton`.
4. The referenced value is not present in the choice list — a visually identifiable stale reference.

## Scope across Swift-v2 templates

Scanning the Swift-v2 ItemType set (`ItemType_Swift-v2_*.xml`):

| Metric | Count |
|---|---:|
| ItemTypes declaring `fieldForTitle="Title"` | **91** |
| ItemTypes in that set that lack a `Title` field (stale reference) | **7** |

### The seven affected ItemTypes

| # | ItemType (systemName) | Fields on type | Referenced title field |
|---|---|---|---|
| 1 | `Swift-v2_Button` | `FirstButton`, `SecondButton` | `Title` (missing) |
| 2 | `Swift-v2_BreadcrumbNavigation` | `ShowProductInBreadcrumb` | `Title` (missing) |
| 3 | `Swift-v2_ImpersonationBar` | `Link` | `Title` (missing) |
| 4 | `Swift-v2_MenuRelatedContent` | `NavigationRoot`, `MaxEndNodes`, `ShowAllLinkLabel` | `Title` (missing) |
| 5 | `Swift-v2_PageProperties` | `Icon`, `SubmenuType`, `AppearanceInNavigation`, … | `Title` (missing) |
| 6 | `Swift-v2_PostList` | `ParentPage` | `Title` (missing) |
| 7 | `Swift-v2_SearchField` | `SearchResultsPage` | `Title` (missing) |

The remaining 84 of the 91 do have a matching `Title` field and are not affected.

A similar pattern may exist for other `fieldForTitle="<name>"` values (`Author` on Blockquote, `LinkText` on EmailViewInBrowser, `Height` on EmailSpacer, `User` on Employee, `Link` on EmailMenu_Item, etc.); those were not audited in this pass because the 47 deserialization warnings were all traceable to the seven above.

## Related strict-mode round-trip symptom

A baseline round-trip (Swift 2.2 → YAML → Swift CleanDB) under `strictMode: true` logs the following against the admin API:

- **47 warnings**: `Could not load PropertyItem for page <guid>` and `Unresolvable page ID N in link`
- **Distinct numeric "page IDs"** that appear in the warnings (from the link-resolution pass of the serializer's internal link rewriter): `1, 2, 4, 16, 19, 21, 23, 33, 34, 37, 40, 41, 42, 44, 48, 60, 97, 98, 104, 113`
- **10 PropertyItem GUIDs** on pages whose property-item instance row is missing (separate but related class)

These numeric IDs are not page IDs in the traditional sense — they appear because the deserializer's link-resolution pass walks every ItemType link-typed field via `item.SerializeTo()`, which converts Dynamicweb's internal numeric link storage back into `Default.aspx?ID=N` form. Seven of these conversions target the stale ItemType title-field lookups described above; the rest target genuinely orphaned pages in the Swift 2.2 seed data.

Under `strictMode: false` the round-trip succeeds with these as informational warnings. Under `strictMode: true` they escalate to failure and the Deserialize Deploy call returns HTTP 400. Live frontends are unaffected either way.

## Why this is non-breaking

- The ItemType system reads `fieldForTitle` only when composing an item's display title. If the referenced field does not exist, DW falls back to the item's `systemName` or the paragraph/page's own display name. No render failure.
- The "Title for new items" admin feature uses the same lookup. A stale reference means the auto-title-from-field behavior simply does not populate; editors can still type a title manually.
- Paragraphs, pages, and areas using the affected ItemTypes continue to save, render, and serialize without error.

## Suggested remediation

Lowest-impact fix (per-ItemType, one-line XML edit):

```diff
-  <item ... systemName="Swift-v2_Button" ... fieldForTitle="Title" ...>
+  <item ... systemName="Swift-v2_Button" ... fieldForTitle="" ...>
```

This removes the reference without changing any other ItemType behaviour. The "Field for title" dropdown in admin then reads empty, matching the Accordion / Card / Email-Icons / Email-Menu / Email-Orderlines / Email-Spacer set that already ship with `fieldForTitle=""`.

Alternative: add a `Title` text field to each of the seven types. Heavier change (adds a new editable field to every instance, touches the paragraph editor surface) and not aligned with the existing design — the affected types derive their title from their purpose (e.g., `FirstButton` for Button), not from a user-entered title.

Either approach removes the admin-UI stale-reference display and quiets the seven corresponding strict-mode deserialization warnings.

## Environment

| | |
|---|---|
| Dynamicweb version | 10.23.9 |
| Design package | Swift v2 |
| Host paths | `wwwroot/Files/System/Items/ItemType_Swift-v2_*.xml` |
| Database | Swift-2.2 (SQL Server / `localhost\SQLEXPRESS`) |

## Reproducer — command-line scan

From a shell pointed at the Swift 2.2 host's Files directory:

```bash
# List ItemTypes that declare fieldForTitle="Title" but have no Title field
cd "<host>/wwwroot/Files/System/Items"
for f in ItemType_Swift-v2_*.xml; do
  if grep -q 'fieldForTitle="Title"' "$f" && ! grep -q '<field[^/]*systemName="Title"' "$f"; then
    echo "$f"
  fi
done
```

Expected output on a clean Swift v2 install: the 7 XML filenames listed above.

## Contact

Reported by the DynamicWeb.Serializer baseline round-trip test harness (strict-mode deserialization). Standalone reproducer included above does not require the serializer — it is an XML-only check of the shipped Swift v2 templates.
