# Swift content: deploy/seed analysis

`swift-starter.json` ships the split documented here; the E2E pipeline asserts the full
seed-subtree contract and the round-trip is verified end to end. This document analyses
every content item in the Swift 2.2 baseline and assigns each an underpinned deploy/seed
decision. The principle behind it: **a re-deploy must never overwrite the customer's
content surfaces (the homepage above all), while everything platform-wired must stay
identical on every environment.**

## 1. What the modes actually do (the facts decisions rest on)

These semantics are implemented and E2E-verified; the decisions below depend on them:

| Behaviour | Deploy | Seed |
|---|---|---|
| Page missing on target | created, full content | created, full content |
| Page exists on target | **overwritten** (source wins, field-level) | **merged** — only fields that are still empty are filled; customer edits always win |
| Page exists on target but not in YAML | left alone — deserialize is an **upsert, never a mirror**; nothing is ever deleted | left alone |
| Links into the other mode's pages | deferred and resolved during that mode's pass (cross-mode link deferral) | same |

Three consequences worth spelling out:

- Customer-**added** pages under a deploy-managed path survive every re-deploy (they are
  simply unmanaged). The deploy risk is only to **edits of pages the YAML contains**.
- A page can be *structurally required* and still live in seed: seed creates it with full
  wiring on first land, and deploy-side links to it resolve via cross-mode deferral. What
  seed gives up is **update propagation** — later solution changes to that page's
  *existing* paragraphs do not reach existing environments.
- **Structure self-heals in both modes.** Create-if-missing applies at every level — page,
  grid row, paragraph — independent of mode (the merge/overwrite strategies only govern
  *updates*). A seed-managed page whose row or paragraph is missing on the target gets it
  back on the next pass, and **new** rows/paragraphs the solution adds to a seed page do
  propagate. Seed is therefore already a "partial" mode: presence and structure are
  guaranteed, content inside is customer-owned.

## 2. Decision framework

Four litmus tests, applied per subtree:

1. **Who edits it after go-live?** Routine content/marketing edits by the customer → seed.
2. **Does the platform reference it by wiring?** Navigation tags, module attach points,
   checkout/login flows, pages referenced by id from area settings or app configs → deploy.
3. **Must solution updates propagate?** If a partner iterating on the solution needs the
   change to land on every environment (checkout fix, component config) → deploy.
4. **Is it example material?** Starter/demo content the customer is expected to replace,
   duplicate, or delete → seed.

**Tie-break** (page is both wired *and* customer-edited, e.g. Home): **seed wins.**
Rationale: seed still creates the page fully wired on first deploy, links to it keep
resolving, and the customer's ownership of their primary content surfaces is the higher
value. Structural redesigns ship through templates/design Files, not through content
paragraphs, so the lost update propagation is acceptable. The reverse mistake (deploy
stomping a customer's homepage) is a production incident.

A third bucket exists and stays out of scope by *not having a predicate*: environment data
(orders, users, logs, consents) plus the field-level carve-outs already in the starter
config (`excludeFields`, `excludeXmlElementsByType` for domains, API keys, mail
recipients).

## 3. Inventory and decisions

Page counts and paragraph counts (¶) from the verified Swift 2.2 baseline (Area 3,
120 pages).

### Navigation section (the public site)

| Subtree | ¶ | Wiring | Decision | Rationale |
|---|---|---|---|---|
| **Home** (6869) | 24 | frontpage binding (area-level, env-owned anyway) | **Seed** | The customer's primary marketing surface; first thing they edit. Must exist on day one — seed creates it. Re-deploys must never stomp it. |
| **Home Machines** (4897, inactive) | 23 | none | **Seed** | Inactive demo alternative homepage; example material to repurpose or delete. |
| **Shop** (5862) + Product List / Product Details | 1+5+6 | NavTag `Shop`, `ProductListPage`, `ProductDetailPage`; catalog module paragraphs | **Deploy** | Pure commerce wiring; paragraphs are app configuration, not copy. PLP/PDP fixes must propagate. (Customer banner tweaks on PLP are the known middle case — see §5.) |
| **About** (107) + Contact + Thank you | 16+5+1 | contact form → form receipt emails (deploy) | **Seed** | Marketing copy and the contact flow's visible content. Cross-mode links to email pages resolve via deferral. |
| **Posts** (8345) + Reviews / Buying guides / Travel guides (20 articles, 2¶ each) | ~42 | none | **Seed** | The canonical starter-blog example. |
| **Navigation** (88) structure + **Express Buy** | 5 | NavTag `ExpressBuyPage` | **Deploy** | The navigation scaffolding (Secondary/Footer Navigation folders, Languages → Preferences) is solution structure; Express Buy is a wired feature page. |
| └ **Find dealers** (1247) | 1 | none | **Seed** | Customer-owned store-locator content under the nav scaffold. |
| └ **Footer Navigation / About the shop** (About us, Terms, Employees, Privacy policy) | 0–4 | none | **Seed** | Legal and company copy — quintessential customer content. A deploy must never overwrite an updated privacy policy. |
| └ **Footer Navigation / Help and info** (FAQ, Delivery, Cookie notice) | 6–7 | none | **Seed** | Same: help copy the customer maintains. |
| └ **Footer Navigation / Languages** (+ Preferences) | 0 | language selector flow | **Deploy** | Framework page backing the language switcher. |

### Customer center section

| Subtree | Decision | Rationale |
|---|---|---|
| **Sign in** (+ Forgot password, Create user profile, confirmations) | **Deploy** | Login/registration flow; module paragraphs; solution fixes must propagate. |
| **Customer center** (Overview, Account, CSR, My orders/carts/quotes/wallet/favorites/profile/addresses…) | **Deploy** | The entire self-service portal is wired (NavTags `SavedCardsPage`, `ViewProfile`, `EditProfile`) and never customer-authored. |
| **Shopping cart** (Cart, Empty cart, Checkout anonymous/user, Quote checkout, Add credit card) | **Deploy** | Checkout is the most correctness-critical flow in the solution; NavTag `AddCreditCardPage`. |

### Swift Setup section

| Subtree | Decision | Rationale |
|---|---|---|
| **Header / Footer** (Desktop/Mobile Header, Desktop/Mobile Footer; 5–8¶ each) | **Seed** | Customers routinely edit USP texts, phone numbers and footer columns. The chrome is bound from area settings (HeaderDesktop/Mobile, FooterDesktop/Mobile item fields), and that wiring is safe in seed: seed guarantees presence and self-heals structure (§1), the area bindings ship at deploy and resolve via cross-pass link deferral, and embedded-`/` path matching ("Header / Footer") is covered by unit test. |
| **Product Components** (Product List, Product List Card, Product Info) | **Deploy** | PLP/PDP component configuration; partner-iterated, never customer-authored. |
| **Service Pages** (CartService, CartSummary, search results, Related products list/slider, Variant Selector, Favorites service) | **Deploy** | The user's own example of clear deploy candidates: invisible framework endpoints, all NavTag-wired. |
| **Search result page** | **Deploy** | NavTag `ContentSearch`. |

### Emails section

| Subtree | Decision | Rationale |
|---|---|---|
| **System emails** (Order confirmation 30¶, Welcome, Back in stock, Form receipts) | **Deploy** | Transactional templates wired into checkout/user-management/stock-notification settings. Environment-specific bits (sender, recipient) are already carved out at field level (`excludeXmlElementsByType`). Copy edits by customers exist but transactional correctness and propagation win. |
| **Newsletter Emails** root + **Unsubscribe confirmation page** | **Deploy** | The folder scaffold and the unsubscribe page are wired into the newsletter flow. |
| └ **Swift Newsletters - Light / - Dark** (example campaigns, Sale 22¶, Announcement 31¶) | **Seed** | Example campaigns the customer duplicates and rewrites — classic starter material. The carve sits at the folder level (not the root) so the unsubscribe page ships via deploy — every page must be covered by exactly one mode. |

### Presets section

| Subtree | Decision | Rationale |
|---|---|---|
| **Page presets** (Home pages → Home preset, `IsTemplate`, 20¶) | **Deploy** | Part of the solution's design system: presets are the partner's curated starting points for new pages. Customer-created presets are additions and survive (upsert semantics). |

## 4. The predicate set

One deploy predicate keeps the "everything is managed unless carved out" default; the
carve-outs become explicit seed predicates. SqlTable predicates are unchanged (framework
tables deploy, catalog seeds).

```jsonc
// Deploy — Content
{ "name": "Site framework", "mode": "Deploy", "providerType": "Content", "areaId": 3,
  "path": "/", "includeLanguageLayers": true,
  "excludes": [
    "/Home", "/Home Machines", "/About", "/Posts",
    "/Header / Footer",
    "/Navigation/Secondary Navigation/Find dealers",
    "/Navigation/Footer Navigation/About the shop",
    "/Navigation/Footer Navigation/Help and info",
    "/Newsletter Emails/Swift Newsletters - Light",
    "/Newsletter Emails/Swift Newsletters - Dark"
  ] }

// Seed — Content (one predicate per customer-owned subtree)
{ "name": "Homepage",            "path": "/Home" }
{ "name": "Homepage (machines)", "path": "/Home Machines" }
{ "name": "Site chrome",         "path": "/Header / Footer" }
{ "name": "About pages",         "path": "/About" }
{ "name": "Starter blog posts",  "path": "/Posts" }
{ "name": "Find dealers",        "path": "/Navigation/Secondary Navigation/Find dealers" }
{ "name": "Footer: about the shop", "path": "/Navigation/Footer Navigation/About the shop" }
{ "name": "Footer: help and info",  "path": "/Navigation/Footer Navigation/Help and info" }
{ "name": "Newsletter examples (light)", "path": "/Newsletter Emails/Swift Newsletters - Light" }
{ "name": "Newsletter examples (dark)",  "path": "/Newsletter Emails/Swift Newsletters - Dark" }
// (all seed Content predicates: mode=Seed, areaId=3, includeLanguageLayers=true,
//  same acknowledgedOrphanPageIds list as today)
```

The bottom line: **well over a hundred customer-content paragraphs across Home, About,
the site chrome (Header / Footer), footer legal/help pages, Find dealers and the
newsletter examples land once and are customer-owned from then on.** Everything wired
deploys identically to every environment.

## 5. Notes and caveats

1. **Why the chrome can live in seed.** "Must exist or the solution breaks" is satisfied
   by seed itself: presence and structure self-heal at page/row/paragraph level
   (create-if-missing is mode-independent, §1) while customers keep their chrome edits.
   Path matching is pure string prefix with a `/`-boundary, both sides built from the same
   menu texts, so the literal `/` in "Header / Footer" matches correctly (unit-tested in
   `ContentCoverageEvaluatorTests`).
2. **Shop PLP/PDP marketing slots.** If customers routinely add banners to the Shop pages,
   those edits sit on deploy-managed pages and would be overwritten. Mitigation today:
   customer banners as *new* paragraphs survive only if deploy YAML doesn't carry the page
   (it does). If this bites, the fix is finer granularity (paragraph-level merge on
   deploy), not moving Shop to seed.
3. **Changing a page's mode needs no data migration.** A page that already exists on the
   target keeps existing; a seed pass merges (fill-empty) it from then on — exactly the
   desired behaviour.
4. **E2E contract.** The pipeline's Step 18 asserts the full split: deploy YAML contains
   none of the seed subtrees, seed ships them all, the target lands them, and the
   unsubscribe page ships via deploy.
5. **The area definition ships only via the whole-area deploy predicate**
   (`path: "/"` owns area properties/item fields), enforced by the deserializer's
   area-state ownership rule.

## 6. Decision record

| # | Decision | Underpinning |
|---|---|---|
| D-1 | Seed wins ties for customer-edited wired pages | Seed creates fully-wired pages on first land; cross-mode links resolve; stomping customer content is the worse failure |
| D-2 | Home, About, footer legal/help, Find dealers, newsletter examples → seed | Litmus test 1 + 4 |
| D-3 | Shop, Customer center, Shopping cart, Swift Setup, System emails, presets, nav scaffold → deploy | Litmus tests 2 + 3 |
| D-4 | Header / Footer → seed | Seed self-heals structure (presence guaranteed); embedded-slash path matching verified; customer owns chrome copy |
| D-5 | No third Content mode needed | Upsert (no-delete) semantics already protect customer additions under deploy paths |
