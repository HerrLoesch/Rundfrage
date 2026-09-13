# Phase 1 Data Model: Wunschliste (Wish List)

**Feature**: 008-wishlist | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md) |
**Decisions**: [research.md](./research.md)

Three new tables, one migration, no change to any existing table. Code identifiers are English;
the interface strings are German (research R-8).

---

## Entities

### `WishList` → table `WishLists`

The occasion. Owns its items; deleting it destroys the items and their claims.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` (v7) | Key |
| `Title` | `string` | Required, ≤ 300 (`TitleMaxLength`) — FR-001, FR-010 |
| `Description` | `string?` | ≤ 2000 (`DescriptionMaxLength`) — FR-003, FR-010 |
| `TargetDate` | `DateOnly` | Required — FR-002. Past dates permitted — FR-002a |
| `ListToken` | `string` | Required, 22 chars, **unique index** — the participant capability (FR-012) |
| `CreatedAt` | `DateTime` (UTC) | Set from `BerlinClock.Now` |
| `Items` | `List<WishItem>` | Cascade delete |

**Indexes**: unique on `ListToken` (it is the lookup key on every participant request);
non-unique on `TargetDate` (the overview orders by it — research R-7, R-12).

**Deliberately absent**: any retention deadline, any expiry, any `ClosedAt`. FR-037 forbids the
first two; FR-028c forbids the third. Closedness is derived — see *Derived values* below.

---

### `WishItem` → table `WishItems`

One thing wished for. Its `WantedCount` is the number of claims it can hold.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` (v7) | Key |
| `WishListId` | `Guid` | FK → `WishLists`, cascade |
| `Name` | `string` | Required, ≤ 200 (`NameMaxLength`), **unique within the list, NOCASE** — FR-008 |
| `WantedCount` | `int` | ≥ 1, ≤ 50 (`MaxWantedCount`), default 1 — FR-005, FR-006, FR-007 |
| `Position` | `int` | Entry order; the display order for operator and participant alike — FR-009 |
| `Claims` | `List<WishClaim>` | Cascade delete |

**Indexes**: unique on `(WishListId, Name)` — FR-008 enforced by the database, not only by the code
that happens to check. Non-unique on `WishListId` follows from the FK.

The `Name` column carries `COLLATE NOCASE`, because `WishListService` compares names with
`OrdinalIgnoreCase` and a BINARY column would let the index accept `"Kuchen"` beside `"kuchen"` —
backstopping a rule it does not share. The two still differ past ASCII: SQLite's NOCASE folds A–Z
only, so `"Äpfel"`/`"äpfel"` is caught by the service and not by the index. That is the service
being *stricter* than the index, which is the safe direction — the index can never admit what the
service refuses.

**Cross-entity rule (FR-010, FR-010a)**: the sum of `WantedCount` over one list's items is ≤ 1000.
Enforced in `WishListService.Validate` on create, on adding an item and on raising a count — never
on the participant path.

---

### `WishClaim` → table `WishClaims`

One name against one item.

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` (v7) | Key |
| `WishItemId` | `Guid` | FK → `WishItems`, cascade |
| `DisplayName` | `string` | Required, ≤ 100 (`DisplayNameMaxLength`) — FR-015, FR-010 |
| `ClaimToken` | `string` | Required, 22 chars, **non-unique index** — shared by every claim of one submission (FR-022b, research R-2) |
| `SubmittedAt` | `DateTime` (UTC) | |

**Indexes**: non-unique on `ClaimToken` (the personal link resolves through it); the FK index on
`WishItemId` carries the capacity count. **No index on `DisplayName` and no uniqueness on it** —
FR-020 expects duplicates, exactly as `PollResponse.DisplayName` already documents.

**Carries no** IP address, user agent, contact detail or identity of any kind (Principle IV). The
rate limiter's partition key stays in memory and is written nowhere (FR-023, research R-9).

---

### "Submission" — a grouping, not a table

The spec's *Submission* entity is the set of claims sharing one `ClaimToken`. It has no row of its
own: research R-2 records why a table for a concept with no attributes beyond a token and a moment
was rejected. Everything FR-022b requires is a query:

```text
the entries this personal link covers  ==  WishClaims WHERE ClaimToken = {token}
```

---

## Relationships

```text
WishList 1 ──< WishItem 1 ──< WishClaim
   │                              │
   │ cascade                      │ grouped by ClaimToken (no table)
   └──────────── cascade ─────────┘
```

Deleting a list deletes its items and their claims (FR-039). Deleting an item deletes its claims
(FR-034). Deleting one claim touches nothing else (FR-043a).

---

## Derived values — computed, never stored

| Value | Expression | Requirement |
|---|---|---|
| `IsClosed` | `clock.Now > clock.EndOfDayUtc(list.TargetDate)` | FR-028a, FR-028c |
| `PlaceCount` | `Σ item.WantedCount` | FR-045.2 |
| `EntryCount` | `count(claims of the list)` | FR-045.1 |
| `OpenPlaces(item)` | `item.WantedCount − count(item.Claims)` | FR-017, FR-018 |
| `UntakenItemCount` | `count(items with no claim)` | FR-045.4 |
| `CompleteItemCount` | `count(items where claims == WantedCount)` | FR-045.5 |
| `IsComplete` | `EntryCount == PlaceCount` | FR-045b |
| Filled share | `EntryCount / PlaceCount`, **formatted in the interface, rounded down** | FR-045.3, research R-7 |

The API carries the counts; it never carries the percentage (research R-7). Every figure above is a
count over rows that already exist — nothing is recorded to make a statistic possible (FR-050,
Principle IV).

---

## State transitions

A wish list has exactly two states and one boundary, and the boundary is the clock:

```text
          TargetDate ends (23:59:59 Europe/Berlin)
  offen ─────────────────────────────────────────▶ geschlossen
    ▲                                                   │
    └───────── operator moves TargetDate forward ────────┘
                        (FR-028c)

  Either state ── operator deletes ──▶ gone (FR-037; nothing else removes a list)
```

| Operation | offen | geschlossen |
|---|---|---|
| Read the list through `/w/:listToken` | yes | yes (FR-028b) |
| Claim an item | yes, if the item has an open place | refused, `wish_list_closed` (FR-028a) |
| Withdraw through `/z/:claimToken` | yes | refused, `wish_list_closed` (FR-028d) |
| Operator edits (title, description, date, items, counts) | yes | yes (FR-029) |
| Operator deletes a claim or the list | yes | yes (FR-043a, FR-037) |

---

## Validation rules and their refusal codes

Added to `Http/ErrorCodes.cs`; every code has a German string in `error.*` (FR-055).

A refusal carries `code`, and — where one applies — `limit` (the number that makes it actionable)
or `detail` (the word that does: today only the item name a duplicate collides with). `detail`
carries operator-written text only, never a participant's display name (FR-050).

| Code | Raised when | Requirement |
|---|---|---|
| `title_required`, `title_too_long` | reused from 002 | FR-001, FR-010 |
| `description_too_long` | description > 2000 | FR-010 |
| `target_date_required` | no target date | FR-002 |
| `items_required` | creation with no item | FR-004 |
| `item_name_required`, `item_name_too_long` | name empty / > 200 | FR-005, FR-010 |
| `duplicate_item_name` | name already used in this list | FR-008 |
| `wanted_count_invalid` | count < 1, > 50, or not whole | FR-007, FR-010 |
| `too_many_items` | more than 100 items | FR-010 |
| `too_many_places` | wished places would exceed 1000; carries the remaining headroom | FR-010, FR-010a |
| `count_below_entries` | lowering below the claims already made; carries that number | FR-033 |
| `unknown_item` | a claim naming an item that belongs to another list | FR-017 |
| `item_full` | claim against an item with no open place | FR-017, FR-017a |
| `wish_list_closed` | claim or withdrawal after the target day | FR-028a, FR-028d |
| `display_name_required`, `display_name_too_long` | reused from 002 | FR-015, FR-010 |
| `not_found` (neutral) | unknown, malformed or deleted list/claim token | FR-024, research R-13 |
| `malformed_request` | a PATCH body that is valid JSON but not an object (`null`, `[]`, `5`) | — |
| `too_many_requests` | reused from 002 | FR-023 |

---

## What this model does **not** add

- **No retention deadline and no place in the sweep.** `RetentionService` stays poll-only, and an
  integration test asserts the sweep leaves wish lists untouched (FR-037, research R-11).
- **No export/import document.** Out of scope; the whole-installation backup covers the new tables
  because it copies the file (research R-10 covers the one consequence that is *not* free).
- **No participant record, access log or visit count.** Principle IV.
- **No change to `Poll`, `CandidateDay`, `PollResponse` or `DayAnswer`.**
