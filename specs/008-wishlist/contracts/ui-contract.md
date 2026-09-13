# UI Contract: Wunschliste (Wish List)

**Feature**: 008-wishlist | **Date**: 2026-09-13 | **Spec**: [spec.md](../spec.md)

What the interface must do, in terms a test can assert. Test ids are part of this contract: the
end-to-end suite addresses elements by `data-testid`, and renaming one is a breaking change.

Measurements — container widths, gaps, heading sizes, card and field styling — are **not** in this
contract. They belong to the application-wide system in [`specs/design-system.md`](../../design-system.md),
which these surfaces follow rather than restate. A wish-list page that sets its own width is a bug
in this feature, not a decision of it.

---

## 1. Navigation (FR-041)

`AdminNav.vue` gains one entry between "Terminfindungen" and "Einstellungen".

| Position | Label (catalogue key) | Route name | `data-testid` |
|---|---|---|---|
| 1 | `nav.dashboard` | `dashboard` | `nav-dashboard` |
| 2 | `poll.listTitle` | `polls`, `poll-answers` | `nav-polls` |
| **3** | **`nav.wishLists`** = "Wunschlisten" | **`wish-lists`, `wish-list`** | **`nav-wish-lists`** |
| 4 | `nav.settings` | `settings` | `nav-settings` |

- Settings stays last; nothing may appear after it (007 FR-003, 008 FR-047).
- The entry owns **both** its route names, so the area stays marked current while one list's
  detail is shown — the same correction `AdminNav` already documents for `poll-answers`.
- Exactly one entry carries `aria-current="page"` at any time.

## 2. Routes

| Address | Route name | Component | Session |
|---|---|---|---|
| `/admin/wunschlisten` | `wish-lists` | `WishListsView.vue` | required |
| `/admin/wunschlisten/:wishListId` | `wish-list` | `WishListDetailView.vue` | required |
| `/w/:listToken` | `wish-list-public` | `WishListView.vue` (in `BareShell`) | **none** |
| `/z/:claimToken` | `claim` | `ClaimView.vue` (in `BareShell`) | **none** |

The two participant routes live in `BareShell` beside `/u/:pollToken` — no navigation drawer, no
sign-out, no link into `/admin` (FR-026, 007 FR-009). No navigation guard: the server is the
authority on the session, as `router.ts` already explains.

## 3. The wish-list area — `WishListsView.vue`

Opens on the list (FR-042). Per row (`data-testid="wish-list-row"`):

- title, target date, a **"Geschlossen"** marker when `closed` (FR-028b),
- `entryCount` **of** `placeCount` and the filled share as a rounded-down percentage (R-7),
- untaken items and complete items, each separately labelled — the two readings of the request
  must not be confusable (FR-045a),
- **"Vollständig"** in words when `entryCount == placeCount` (FR-045b), not only 100 %,
- the participant link as `PollList.vue` renders a poll's: a bare anchor carrying the **absolute**
  address as its text, with the new-tab note beside it as the link's `aria-describedby` description
  (FR-016a, FR-017). Deliberately **not** `ShareLink.vue` — that component is a success banner
  announcing one newly minted link, and two hundred of them stacked down a list is not what it is
  for. It stays where it belongs: the creation form and the detail view.
- delete, reachable in one action, behind a confirmation naming how many entries die (FR-038).
  Its own dialog rather than `DeleteConfirm.vue`, for one word: that component counts in
  "Antworten", and what dies with a wish list is Zusagen. FR-038 asks the confirmation to say what
  will be destroyed, and saying it with the wrong noun is not saying it.

States, all three distinguishable and none rendered as zeros:

| State | Requirement | `data-testid` |
|---|---|---|
| No list exists | says so **and offers creating from the empty state** | `wish-lists-empty` |
| Storage unreachable | says unreachable, shows no figure | `wish-lists-unavailable` |
| Loading | neither of the above | `wish-lists-loading` |

Creating is an action (`data-testid="wish-create-toggle"`) that reveals `WishListForm.vue`
(`data-testid="wish-list-form"` on the form itself). **Two ids, not one**: the suites wait for the
form to appear, and an id on the button alone cannot tell "revealed" from "not revealed". The form
is never expanded before it is asked for, closing or leaving discards what was typed, and a
rejected form stays open with the entry intact and the reason shown (FR-042, mirroring 007
FR-014h–k).

## 4. One wish list — `WishListDetailView.vue`

Its own address, reloadable (FR-043). Carries:

- the list's title, description and target date, each editable in place (FR-029),
- every item with its wanted count, its claims by name, and per-claim deletion (FR-043a),
- add item, rename item, change wanted count, remove item (FR-030–FR-034),
- removing an item confirms with the number of entries it destroys,
- a way back to the list.

Refusals are shown where they happen, in words from the catalogue:

| Code | German message says |
|---|---|
| `count_below_entries` | how many names are already entered, so the operator knows what to remove first |
| `too_many_places` | how many places remain of the 1000 |
| `duplicate_item_name` | which item it collides with — the refusal carries that name as `detail` |

Deleting the last claim leaves the operator on this page showing its empty state — it does not
bounce back to the list (mirrors 007 FR-014f).

## 5. The participant page — `WishListView.vue` (`/w/:listToken`)

**Zero steps between the link and the form** (Principle I, FR-014). One page shows:

- title, description, target date,
- every item in the operator's order (FR-009), each with wanted count, **open places in text**
  (`"noch 2 von 3 frei"`) and the names already entered (FR-019),
- a full item marked **"Vollständig"** in text with no entry control (FR-018) — the state must not
  depend on colour alone (FR-057),
- one name field and per-item selection, submitted together in one action (FR-016),
- the visibility notice *before* the name field: the name is visible to everyone holding the link
  (FR-019a) — reuse the wording pattern of `participate.visibilityNotice`.

**The order is the order of the question**: the name is asked for first (`claim.stepName` —
"Wie heißt du?"), then what the person brings (`claim.title` — "Was bringst du mit?"). The two are
numbered **1** and **2**; the digits are `aria-hidden`, because the headings already say what each
step asks and the order a screen reader needs is carried by the document.

This is a **reordering on one page, never a wizard**: FR-013 puts everything on the page the link
opens and FR-014 forbids any step between the link and the entry. Both steps and the submit control
live inside a single `form` (`wish-claim-form`), so the document order is
`wish-name` → `wish-items` → `wish-submit`, and two unit tests hold it there.

After submitting: the entry appears with the updated open places without a manual reload (FR-021),
and the **personal link is shown with a plain instruction to keep it** (FR-022), through the same
`ShareLink.vue`.

When `closed` is true: everything above is still shown, the page says the list is closed and names
the target date, and **no entry form is rendered** (FR-028b) — no `wish-claim-form`, no step
numbers, and no selection column, since nothing on a closed list is claimable and an empty column
would only indent the text for no reason. `wish-items` stays, because everything it showed is
still shown. Withdrawal lives only on the personal
link (§6) and is refused there while the list is closed (FR-028d).

| Refusal | Shown as |
|---|---|
| `item_full` | the item filled up, with its current state beside it (FR-017b) |
| `wish_list_closed` | the list has closed; the page re-reads and re-renders as closed |
| `too_many_requests` | too many entries were sent, retry later (reuses `error.too_many_requests`) |
| 404 | one wording for unknown, malformed and deleted alike (SC-012) |
| 503 | the existing `MaintenanceNotice.vue`, unchanged (FR-025) |

## 6. The personal link — `ClaimView.vue` (`/z/:claimToken`)

Shows exactly the entries this submission made, with the list's title and target date, and one
withdraw control per entry (FR-022, FR-022c). Withdrawing asks for confirmation naming the item and
the name, then removes it and says the place is free again.

- Grants nothing else: no other entry, no other list, no admin function (FR-022c).
- An entry already gone — withdrawn twice, or deleted by the operator — says so and offers no
  restore.
- While the list is closed, withdrawal is refused with the closed wording and the entries are shown
  read-only (FR-028d).

## 7. The dashboard — `DashboardView.vue`

Gains a **wish-list region** below the existing poll figures, visibly its own region (FR-048).

- One row per wish list (`data-testid="dashboard-wish-list-row"`): title, target date,
  open/closed, entries, filled share (FR-048a).
- Order comes from the API and is not re-sorted in the browser: open first by nearest target date,
  then closed most-recently-closed first (FR-048c).
- States the total number of wish lists, is **bounded in height and scrolls within itself**, so at
  200 rows the poll figures are still reachable (FR-048c).
- Each row leads to that list in the wish-list area in one action (FR-048d). The region offers no
  create, edit or delete (FR-051).
- **No participant name appears anywhere on the dashboard** (FR-050) — an assertion, not a habit.
- No wish list: says so in words (`dashboard-wish-lists-empty`). Unreadable: says so and shows no
  figure (`dashboard-wish-lists-unavailable`). Never zeros for either (FR-052).
- Re-read on entering the dashboard, so a change made in the area shows without a manual reload
  (FR-053).

## 8. Text, accessibility and layout

- Every string from `locales/de.json` — new `wish.*` block, new `error.*` entries, `nav.wishLists`.
  No literal in any component (FR-055).
- Keyboard-operable throughout, text labels on every control, focus visibly marked (FR-056).
- Open/full/closed conveyed in text, never by colour alone (FR-057).
- Usable at 375 px with 100 items: the entry form for an item reachable without disproportionate
  scrolling (FR-058).

## 9. Store

One new Pinia store, `stores/wishLists.ts`, following `stores/polls.ts`:

- admin: `list()`, `load(id)`, `create()`, `update()`, `addItem()`, `updateItem()`, `removeItem()`,
  `remove(id)`, `deleteClaim()`,
- participant state lives in the participant components, as `stores/answering.ts` does for polls,
- a 401 from any admin call routes to the sign-in form, exactly as the existing stores do (007
  FR-011).

The dashboard reads the same `list()` as the area (research R-5), so the two cannot disagree.
