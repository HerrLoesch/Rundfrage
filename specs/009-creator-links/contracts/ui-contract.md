# UI Contract: Ersteller-Links

**Feature**: 009-creator-links | **Date**: 2026-09-13 | **Spec**: [spec.md](../spec.md)

What the interface must do, in terms a test can assert. Test ids are part of this contract: the
end-to-end suite addresses elements by `data-testid`, and renaming one is a breaking change.

Measurements — container widths, gaps, heading sizes, card and field styling — are **not** in this
contract. They belong to [`specs/design-system.md`](../../design-system.md), which these surfaces
follow rather than restate.

---

## 1. Navigation (FR-043)

`AdminNav.vue` gains one entry, in the middle section, after "Wunschlisten".

| Position | Label (catalogue key) | Route names owned | `data-testid` |
|---|---|---|---|
| 1 | `nav.dashboard` | `dashboard` | `nav-dashboard` |
| 2 | `poll.listTitle` | `polls`, `poll-answers` | `nav-polls` |
| 3 | `nav.wishLists` | `wish-lists`, `wish-list` | `nav-wish-lists` |
| **4** | **`nav.creators` = "Ersteller"** | **`creators`** | **`nav-creators`** |
| 5 | `nav.settings` | `settings` | `nav-settings` |

- Settings stays last; nothing may appear after it (007 FR-003, 009 FR-047).
- Ersteller is a **capability** of the installation, not a setting of it — which is why it sits in
  the middle section beside the two feature areas rather than inside Einstellungen.
- Exactly one entry carries `aria-current="page"` at any time.
- The area has **one** route, so unlike entries 2 and 3 it owns a single name. There is no detail
  destination: an Ersteller is a name, a link and two counts, which is a row, not a page.

## 2. Routes

| Address | Route name | Component | Session |
|---|---|---|---|
| `/admin/ersteller` | `creators` | `CreatorsView.vue` | required |
| `/e/:creatorToken` | `creator` | `CreatorSurface.vue` (in `BareShell`) | **none** |

**One public route, and only one** (FR-028d). `CreatorSurface.vue` lives in `BareShell` beside
`/u/:pollToken`, `/w/:listToken`, `/a/:editToken` and `/z/:claimToken` — no navigation drawer, no
sign-out, no link into `/admin` (FR-029, 007 FR-009).

> **Do not give the creator surface child routes.** A reviewer comparing this with
> `/admin/terminfindungen/:pollId` will see an inconsistency with feature 007 and be tempted to fix
> it. The asymmetry is the decision: 007 gave the operator addresses because a session costs nothing
> to put in a URL; here the address *is* the credential (spec Q5, research R-9). Opening a poll's
> answers must not change the address, must not add a history entry, and must not add a query
> parameter (FR-028e).

No navigation guard, for the reason `router.ts` already records: the server is the authority.

## 3. The Ersteller area — `CreatorsView.vue`

Opens on the list (FR-045). Creating is an action that reveals a form, not a form that is always
open (007 FR-014h); leaving the area discards an unconfirmed entry (007 FR-014j).

| Element | `data-testid` | Requirement |
|---|---|---|
| The list | `creator-list` | FR-044 |
| One row | `creator-row` | FR-044 |
| Row: name | `creator-name` | FR-001 |
| Row: created date | `creator-created` | FR-044 |
| Row: polls owned | `creator-poll-count` | FR-044 |
| Row: wish lists owned | `creator-wish-list-count` | FR-044 |
| Row: the link, copyable | `creator-link` | FR-044 |
| Row: "kein gültiger Link" state | `creator-no-link` | FR-044a |
| Reveal the creation form | `creator-create-open` | FR-045 |
| The creation form | `creator-form` | FR-001 |
| Rename | `creator-rename` | FR-017 |
| Reissue the link | `creator-reissue` | FR-016 |
| Revoke the link | `creator-revoke` | FR-018 |
| Delete the Ersteller | `creator-delete` | FR-020a |
| Empty state, offering creation | `creator-empty` | FR-046 |
| Storage-unavailable state | `creator-unavailable` | FR-046 |

**States that must be distinguishable** (FR-046, 007 FR-017): *no Ersteller exists* says so in
words and offers creating one; *the data cannot be read* says so and shows no list. Neither is ever
rendered as a zero.

### 3a. Revoke and delete must not look alike (FR-020b)

This is the highest-risk piece of interface in the feature, because one action destroys nothing and
the other destroys everything an Ersteller made.

| | Revoke (`creator-revoke`) | Delete (`creator-delete`) |
|---|---|---|
| Confirmation | states the link will stop working and **that nothing it owns will be removed** | names the Ersteller and states **both counts** that will be destroyed |
| Confirmation testid | `creator-revoke-confirm` | `creator-delete-confirm` |
| Visual weight | ordinary action | destructive, and the only destructive one in the row |
| Placement | never adjacent in the DOM to the delete control | — |
| After it | the row stays, with `creator-no-link` and its counts | the row is gone |

The delete confirmation additionally says that content cannot be kept by handing it to the operator,
and that an export must be taken first if anything is to be kept (FR-020c). `DeleteConfirm.vue` is
reused rather than a new confirmation component written.

The cap refusal (FR-009, FR-009a) must state the limit, how many exist, **and that a place is freed
by deleting an Ersteller rather than by revoking one**. Test id `creator-limit-refusal`.

## 4. The creator surface — `CreatorSurface.vue` (`/e/:creatorToken`)

One page. Two lists. No dashboard, no aggregate over the two (FR-028b).

| Element | `data-testid` | Requirement |
|---|---|---|
| The holder's name | `creator-greeting` | FR-007 |
| What the link grants, stated before sharing | `creator-link-warning` | FR-058 |
| Poll list | `creator-polls` | FR-024 |
| Wish-list list | `creator-wish-lists` | FR-025 |
| Create a poll | `creator-new-poll` | FR-022 |
| Create a wish list | `creator-new-wish-list` | FR-023 |
| Poll answers, opened in place | `creator-poll-answers` | FR-024, FR-028d |
| Wish-list detail, opened in place | `creator-wish-list-detail` | FR-025, FR-028d |
| Export one's own poll | `creator-export` | FR-028 |
| Empty poll list | `creator-polls-empty` | US1 scenario 4 |
| Empty wish-list list | `creator-wish-lists-empty` | US1 scenario 4 |

**Must not exist on this surface, at all** (FR-028a, FR-028b, FR-029, FR-030, SC-004): any file
input or upload control; any import control; any dashboard, tile or figure summarising both lists;
any maintenance, backup, restore or settings control; any link into `/admin`; any Ersteller
management.

`PollForm.vue`, `WishListForm.vue` and the existing results/detail components are **reused**, driven
by the creator store instead of the admin stores. A second way of rendering a poll would be a second
place for features 002, 004, 006 and 008 to be kept true.

**Reload behaviour** (FR-028f): returns to the top with both lists shown; an unconfirmed form is
discarded; nothing already saved is affected.

## 5. The admin surfaces that gain an owner (FR-039)

| Surface | Change | `data-testid` |
|---|---|---|
| `PollList.vue` row | owner shown | `poll-owner` |
| `PollAnswersView.vue` header | owner shown | `poll-owner` |
| `WishListsView.vue` row | owner shown | `wish-list-owner` |
| `WishListDetailView.vue` header | owner shown | `wish-list-owner` |
| `DashboardView.vue` overview row | owner shown | `dashboard-wish-list-owner` |

`null` renders as the operator's own (catalogue key `creator.ownerSelf`), never as an empty cell —
an empty cell reads as missing data rather than as "mine".

**Every admin action stays available on every row whatever the owner** (FR-040, SC-005a). No control
is hidden, disabled or relabelled on grounds of ownership.

## 6. Text, accessibility and layout

- All new strings come from `de.json` (FR-054). New top-level key: `creator`. New `nav.creators`.
- Every control keyboard-operable, text-labelled, with visible focus (FR-055).
- Whether a link is current or has been replaced is conveyed **in text**, never by colour alone
  (FR-057) — `creator-no-link` carries words.
- The creator surface is usable at 375 px (FR-056, SC-011).
- The owner column must not push the poll or wish-list rows into horizontal scrolling at 375 px.

## 7. Stores

One new Pinia store, `creator.ts`, for the creator surface: it holds the token from the route, both
lists, and the open disclosure. It is **separate from** `polls.ts` and `wishLists.ts`, which are the
operator's and talk to `/admin/**`; sharing a store would be sharing a URL prefix, and the first
mistake would be a creator request going to an admin endpoint.

`creators.ts` — the operator's store for the Ersteller area — is the second new store, beside
`polls.ts`, `wishLists.ts`, `dashboard.ts`, `maintenance.ts` and `session.ts`.
