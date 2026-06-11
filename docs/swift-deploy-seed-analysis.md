# Swift content: deploy/seed analysis

Status: **proposal** (2026-06-11). The shipped `swift-starter.json` uses a coarse split —
deploy = the whole site except `/Posts`, seed = `/Posts` + catalog. This document analyses
every content item in the Swift 2.2 baseline and assigns each an underpinned deploy/seed
decision. The headline change: **the coarse split puts the Home page under deploy, so a
re-deploy overwrites the customer's homepage edits.** That is wrong for how Swift projects
actually run.

## 1. What the modes actually do (the facts decisions rest on)

These semantics are implemented and E2E-verified; the decisions below depend on them:

| Behaviour | Deploy | Seed |
|---|---|---|
| Page missing on target | created, full content | created, full content |
| Page exists on target | **overwritten** (source wins, field-level) | **merged** — only fields that are still empty are filled; customer edits always win |
| Page exists on target but not in YAML | left alone — deserialize is an **upsert, never a mirror**; nothing is ever deleted | left alone |
| Links into the other mode's pages | deferred and resolved during that mode's pass (cross-mode link deferral) | same |

Two consequences worth spelling out:

- Customer-**added** pages under a deploy-managed path survive every re-deploy (they are
  simply unmanaged). The deploy risk is only to **edits of pages the YAML contains**.
- A page can be *structurally required* and still live in seed: seed creates it with full
  wiring on first land, and deploy-side links to it resolve via cross-mode deferral. What
  seed gives up is **update propagation** — later solution changes to that page's
  paragraphs do not reach existing environments.

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
| **Posts** (8345) + Reviews / Buying guides / Travel guides (20 articles, 2¶ each) | ~42 | none | **Seed** (unchanged) | The canonical starter-blog example, already proven in the shipped split. |
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
| **Header / Footer** (Desktop/Mobile Header, Desktop/Mobile Footer; 5–8¶ each) | **Deploy** (contested — see §5) | Bound from area settings (HeaderDesktop/Mobile, FooterDesktop/Mobile item fields); structurally the site chrome. *But* customers do edit USP texts, phone numbers and footer columns — this is the one subtree where the tie-break argument for seed is also defensible. Kept in deploy for v2 because of the area-binding wiring and a path-matching caveat (the menu text contains `/`, see §5). |
| **Product Components** (Product List, Product List Card, Product Info) | **Deploy** | PLP/PDP component configuration; partner-iterated, never customer-authored. |
| **Service Pages** (CartService, CartSummary, search results, Related products list/slider, Variant Selector, Favorites service) | **Deploy** | The user's own example of clear deploy candidates: invisible framework endpoints, all NavTag-wired. |
| **Search result page** | **Deploy** | NavTag `ContentSearch`. |

### Emails section

| Subtree | Decision | Rationale |
|---|---|---|
| **System emails** (Order confirmation 30¶, Welcome, Back in stock, Form receipts) | **Deploy** | Transactional templates wired into checkout/user-management/stock-notification settings. Environment-specific bits (sender, recipient) are already carved out at field level (`excludeXmlElementsByType`). Copy edits by customers exist but transactional correctness and propagation win. |
| **Newsletter Emails** (Swift Newsletters Light/Dark example campaigns, Sale 22¶, Announcement 31¶) | **Seed** | Example campaigns the customer duplicates and rewrites — classic starter material. |
| └ **Unsubscribe confirmation page** | **Deploy** | Wired into the newsletter unsubscribe flow; excluded from the seed subtree. |

### Presets section

| Subtree | Decision | Rationale |
|---|---|---|
| **Page presets** (Home pages → Home preset, `IsTemplate`, 20¶) | **Deploy** | Part of the solution's design system: presets are the partner's curated starting points for new pages. Customer-created presets are additions and survive (upsert semantics). |

## 4. Resulting predicate set (v2 proposal)

One deploy predicate keeps the "everything is managed unless carved out" default; the
carve-outs become explicit seed predicates. SqlTable predicates are unchanged (framework
tables deploy, catalog seeds).

```jsonc
// Deploy — Content
{ "name": "Site framework", "mode": "Deploy", "providerType": "Content", "areaId": 3,
  "path": "/", "includeLanguageLayers": true,
  "excludes": [
    "/Home", "/Home Machines", "/About", "/Posts",
    "/Navigation/Secondary Navigation/Find dealers",
    "/Navigation/Footer Navigation/About the shop",
    "/Navigation/Footer Navigation/Help and info",
    "/Newsletter Emails"
  ] }

// Seed — Content (one predicate per customer-owned subtree)
{ "name": "Homepage",            "path": "/Home" }
{ "name": "Homepage (machines)", "path": "/Home Machines" }
{ "name": "About pages",         "path": "/About" }
{ "name": "Starter blog posts",  "path": "/Posts" }
{ "name": "Find dealers",        "path": "/Navigation/Secondary Navigation/Find dealers" }
{ "name": "Footer: about the shop", "path": "/Navigation/Footer Navigation/About the shop" }
{ "name": "Footer: help and info",  "path": "/Navigation/Footer Navigation/Help and info" }
{ "name": "Newsletter examples", "path": "/Newsletter Emails",
  "excludes": ["/Newsletter Emails/Unsubscribe confirmation page"] }
// (all seed Content predicates: mode=Seed, areaId=3, includeLanguageLayers=true,
//  same acknowledgedOrphanPageIds list as today)
```

Net effect versus the shipped split: **~70 customer-content paragraphs across Home, About,
footer legal/help pages, Find dealers and the newsletter examples move from
overwrite-on-redeploy to land-once-then-customer-owned.** Everything wired stays deploy.

## 5. Open items and caveats

1. **Header / Footer is the contested call.** The wiring argument (area bindings) says
   deploy; the ownership argument (USP texts, footer columns) says seed. v2 keeps it in
   deploy. Additionally its menu text contains a literal `/` ("Header / Footer"), and
   predicate paths are menu-text based — splitting it out needs a verified answer for how
   path matching treats embedded slashes before it can move. Revisit after first real
   customer feedback.
2. **Shop PLP/PDP marketing slots.** If customers routinely add banners to the Shop pages,
   those edits sit on deploy-managed pages and would be overwritten. Mitigation today:
   customer banners as *new* paragraphs survive only if deploy YAML doesn't carry the page
   (it does). If this bites, the fix is finer granularity (paragraph-level merge on
   deploy), not moving Shop to seed.
3. **Mode migration on existing targets.** Changing a page's mode does not migrate
   anything by itself: a page that previously landed via deploy already exists on the
   target, and a seed pass will merge (fill-empty) it from then on — which is exactly the
   desired behaviour. No data movement is needed.
4. **E2E contract.** The pipeline's Step 18 asserts the `/Posts` exclusion contract
   specifically. Adopting v2 means extending it: deploy YAML must contain none of the
   eight seed subtrees, seed must ship them all, and the target must land them.
5. **Both modes still ship the area definition only via the whole-area deploy predicate**
   (`path: "/"` owns area properties/item fields) — unchanged by this proposal and
   already enforced by the deserializer's area-state ownership rule.

## 6. Decision record

| # | Decision | Underpinning |
|---|---|---|
| D-1 | Seed wins ties for customer-edited wired pages | Seed creates fully-wired pages on first land; cross-mode links resolve; stomping customer content is the worse failure |
| D-2 | Home, About, footer legal/help, Find dealers, newsletter examples → seed | Litmus test 1 + 4 |
| D-3 | Shop, Customer center, Shopping cart, Swift Setup, System emails, presets, nav scaffold → deploy | Litmus tests 2 + 3 |
| D-4 | Header / Footer stays deploy in v2 | Area-binding wiring + path-slash caveat; revisit |
| D-5 | No third Content mode needed | Upsert (no-delete) semantics already protect customer additions under deploy paths |
