# Implementation Plan: Wunschliste (Wish List)

**Branch**: `008-wishlist` | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/008-wishlist/spec.md`

## Summary

A second capability beside date polls: a wish list with a title, a description and a target date,
holding items with quantities. One link, no account, a name against an item — and an item wanted
three times holds exactly three names. The operator edits the list after sharing it, sees how far it
has come, and is the only thing that ever deletes it.

Technically this is the first feature since 002 that adds real domain data: three tables
(`WishLists`, `WishItems`, `WishClaims`), one migration, eight new routes, one new admin area and
two new participant surfaces. Almost every mechanism it needs already exists in this codebase and is
**reused rather than reinvented** — the immediate write transaction that makes capacity safe under
concurrency (R-1), the capability token and the neutral 404 (R-8, R-13), the submission rate limiter
(R-9), the maintenance gate (R-13), the shell and its navigation (007). What is genuinely new is
small: capacity, the derived "geschlossen" state, and one projection read by two screens.

**Two things in this plan are not additive and are called out rather than buried.** First, feature
007's FR-034 forbids titles on the dashboard; 008 FR-048b narrows it to permit wish-list titles, and
amending 007's spec is a task in this feature, not a footnote (R-6). Second, the restore preview
counts polls and responses; once wish lists exist it would understate what a restore destroys, so it
gains two counts (R-10). That second one sits inside this spec's *Out of Scope* by its literal
wording and outside it by its intent — the reasoning is in R-10 and the judgement is stated openly
for the operator of this plan to overrule if they disagree.

**No open risk gates this design.** Unlike 007, the performance claim needs no spike first: FR-054's
scale is two orders of magnitude below what 007 measured (R-12). The scale test is still written.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); TypeScript 5.9 for Vue 3.5
**Primary Dependencies**: Vue 3.5.42, Vuetify 4.2, Pinia 4.0.3, Vue Router 5.3.1, vue-i18n 11.4.10,
Vite 8.2.2; ASP.NET Core on .NET 10 with EF Core 10.0.11 (`Microsoft.EntityFrameworkCore.Sqlite`).
**No new dependency on either side.**
**Storage**: SQLite, single file on a mounted volume. **One migration adding three tables**; no
existing table changes (data-model.md). Cascade deletes from list to item to claim; unique indexes
on `WishLists.ListToken` and `(WishItems.WishListId, Name)`; a non-unique index on
`WishClaims.ClaimToken` and on `WishLists.TargetDate`
**Testing**: Vitest 4.1 + Vue Test Utils 2.5 (frontend unit/component); xUnit (backend unit and
integration, via the existing `ApiFactory`/`SqliteFixture`); Playwright (end-to-end)
**Logging**: Serilog to stdout. New statements carry identifiers and counts only — never a display
name, never an item name, never a token (mirrors `ResponseService`'s logging and 002 FR-043a)
**Target Platform**: Linux server backend, modern browsers
**Performance Goals**: SC-009 — the dashboard's wish-list overview and the area's list on screen
within two seconds at FR-054's scale (200 lists, 200,000 claims). A fixed number of queries
regardless of list count (R-12)
**Constraints**: Capacity may never be exceeded under any interleaving (SC-002, R-1); no stored
"closed" state (FR-028c, R-3); no percentage on the wire (R-7); no participant name on the dashboard
(FR-050); no automatic deletion of anything (FR-037)
**Scale/Scope**: Per list — 100 items, 1000 wished places, wanted count ≤ 50 (FR-010). Installation —
200 lists documented for SC-009, enforced nowhere (FR-054). Backend: 3 entities, 1 migration, 8
routes, ~3 services. Frontend: 4 routes, ~6 components, 1 store, 1 navigation entry

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against `.specify/memory/constitution.md` (v2.0.1).

**Pre-Phase 0**: all gates pass.

- [x] **I. Zero-Signup Participation**: The participant claims an item from `/w/:listToken` with no
      account, no sign-in and no confirmation — zero steps between link and form (FR-014, SC-001).
      Withdrawal is the one capability that cannot work anonymously, and it is solved the way the
      constitution demands: a link-scoped secret (`/z/:claimToken`), never an identity (FR-022,
      R-2). Display names are labels, not identities, and are neither verified nor made unique
      (FR-015, FR-020). The participant surface shows no navigation and no route into `/admin`
      (FR-026).
      *One item deserves naming rather than hiding*: a closed list stops offering the entry form
      (FR-028b). That is not a step placed before the form — it is the form being honestly absent
      once entries can no longer be useful, and the page still shows everything it showed before.
- [x] **II. Test-First Development**: Every task in `tasks.md` will be ordered test-first, and the
      pair to write first is the concurrency test and the capacity code (R-1, R-14): a capacity rule
      retrofitted after the fact is exactly the bug SC-002 exists to prevent. Validation rules,
      the closed-boundary arithmetic, the refusals and the projection each get a failing test before
      their implementation.
- [x] **III. Simplicity & YAGNI**: No new project, layer, service or dependency. Two abstractions
      that *suggested themselves were refused* and the refusals are recorded: a `WishSubmission`
      table for a concept with no attributes of its own (R-2), and a second rate-limit policy where
      the existing one already satisfies the requirement (R-9). No "survey type" abstraction over
      polls and wish lists: there is now a second case, but nothing in either feature is shared
      behaviour waiting to be factored out — they share mechanisms (tokens, transactions, the shell)
      and those are already shared. **Complexity Tracking is empty and is meant to stay that way.**
- [x] **IV. Data Minimization & Operator-Controlled Storage**: A claim stores a display name and a
      moment. No IP address, no user agent, no contact detail, no access record — the rate limiter's
      partition stays in memory (R-9). Every status figure is a count over rows that already exist;
      nothing is recorded to make a statistic possible (FR-050). All data stays in the self-hosted
      file; nothing is transmitted anywhere. **Retention outcome**: the operator's explicit deletion
      (FR-037, FR-040), which the constitution permits as one of its two forms — "an expiry *or* an
      explicit deletion path" — and deletion erases rows rather than hiding them.
- [x] **Technology Constraints**: Entirely within the pinned stack. EF Core supplies the tables and
      the aggregates, Vuetify the components, Vue Router the routes, vue-i18n the German strings.
      No new package on either side.

**Post-implementation re-check (2026-09-13)**: all five gates pass against the delivered code.
**686 tests green** — 294 frontend unit, 139 backend unit, 253 backend integration. The end-to-end
suite is written but was **not run**: it needs the operator's plaintext password, and `.env` holds
only the hash. That is stated here rather than glossed, and the command is in the handover.

- **I. Zero-Signup Participation** — `/w/:listToken` loads the list and its entry form together;
  `WishListView.spec.ts` asserts the form is on the page that loaded, and `zero-signup.spec.ts`
  asserts a wish-list link shows no navigation, no sign-out and zero `a[href*="/admin"]`.
  Withdrawal uses a link-scoped secret (`/z/:claimToken`), never an identity, and
  `WishClaimTests` proves that token reaches nothing else.
- **II. Test-First Development** — every behaviour task was preceded by its failing test, and the
  order that mattered held: `WishConcurrencyTests` was written and watched fail before
  `ClaimService` existed. Three ordering deviations are recorded honestly below.
- **III. Simplicity & YAGNI** — no new project, layer, service or dependency. Both refused
  abstractions stayed refused: there is no submission table (the claim token is a column) and no
  second rate limiter. Complexity Tracking stayed empty.
- **IV. Data Minimization** — `WishClaims` holds a display name, a token and a moment, and
  `SchemaCreationTests` asserts the column list exactly, so an IP or user-agent column cannot be
  added quietly. No figure is stored; every one is a count on read. Retention is the operator's
  explicit deletion, and `WishRetentionTests` guards that the sweep never touches a wish list.
- **Technology Constraints** — nothing introduced outside the pinned stack.

**Three ordering deviations, stated rather than hidden:**

1. `DELETE /admin/wish-lists/{id}` was implemented in US1 (T023) rather than US2 (T042), because
   two US1 requirements — the neutral 404 for a deleted link (FR-024) and a personal link dying
   with its list (FR-022e) — cannot be tested without it.
2. The closed-state checks in `ClaimService` were written during T021 (US1) rather than T041
   (US2), so `WishListEditTests`' closed-state tests were written against existing code. They
   still did their job: they caught that a withdrawal after closing needed its own rule rather
   than inheriting the claim path's.
3. `WishListDetailView.vue` (T043, US2) was created before US2's frontend tasks, because the US1
   router entry points at it and the build does not pass without it.

**Two things the implementation changed outside its own files, both deliberate:**

- `AdminAuthorizationTests` now sends a body for `PATCH` as it already did for `POST` and `PUT`.
  A body-taking endpoint carries a Consumes constraint, so a PATCH without a content type is
  filtered out during endpoint selection and answers 404 — which made the guard report "not
  protected" for a route that is. The guard predates PATCH; it does not any more.
- `AdminNav.spec.ts` asserted exactly three navigation entries. There are four. The test was
  updated deliberately, and still asserts that no entry exists for anything unbuilt (007 FR-004).

**Post-Phase 1 re-check**: all five gates still pass. Three points are worth recording as
*deliberately held* rather than merely satisfied:

- Principle I decided R-2's shape: withdrawal needed a secret, and the cheapest secret that is not
  an identity is a token shared by one submission's rows.
- Principle III decided against the submission table and against a second limiter, in both cases
  where the more elaborate option was the more "correct-looking" one.
- Principle IV is the reason the spec's percentage never reaches the database: the figures are
  counts, computed on read, with no maintained total that five deletion paths could leave stale —
  the same trap 007 R-3 refused.

## Project Structure

### Documentation (this feature)

```text
specs/008-wishlist/
├── plan.md              # This file
├── spec.md              # What and why, with five clarifications
├── research.md          # R-1 … R-14: decisions and rejected alternatives
├── data-model.md        # Three tables, derived values, state transitions, refusal codes
├── quickstart.md        # Developer guide
├── contracts/
│   ├── openapi.yaml     # Eight routes plus the restore-preview addition
│   └── ui-contract.md   # Components, states, test ids, accessibility
├── checklists/
│   └── requirements.md  # Spec quality checklist (passing)
└── tasks.md             # Phase 2 output — created by /speckit-tasks, not by this command
```

### Source code (repository root)

```text
backend/src/Rundfrage.Api/
├── Data/
│   ├── Entities/
│   │   ├── WishList.cs                   # NEW
│   │   ├── WishItem.cs                   # NEW
│   │   └── WishClaim.cs                  # NEW
│   ├── Migrations/<stamp>_WishLists.cs   # NEW — three tables, no existing table touched
│   ├── RundfrageDbContext.cs             # EDIT — three DbSets, indexes, cascades
│   └── RestoreService.cs                 # EDIT — preview counts wish lists and claims (R-10)
├── Wishes/                               # NEW folder, beside Polls/
│   ├── WishListService.cs                # create, edit, delete, validation (FR-001…FR-039)
│   ├── ClaimService.cs                   # claim, withdraw, capacity in one write transaction (R-1)
│   └── WishListProjection.cs             # the summary rows both readers use (R-5, R-12)
├── Endpoints/
│   ├── Admin/WishListAdminEndpoints.cs   # NEW — six admin routes
│   └── Public/WishEndpoints.cs           # NEW — participant and personal-link routes
├── Http/ErrorCodes.cs                    # EDIT — the new refusal codes
├── Time/BerlinClock.cs                   # EDIT — EndOfDayUtc(DateOnly), reused by the deadline
└── Program.cs                            # EDIT — register services, map the two route groups

backend/tests/
├── Rundfrage.Api.UnitTests/
│   ├── WishListValidationTests.cs        # NEW
│   └── WishClosingTests.cs               # NEW — day boundary, summer time
└── Rundfrage.Api.IntegrationTests/
    ├── WishListAdminTests.cs             # NEW — create, edit, refusals, delete
    ├── WishClaimTests.cs                 # NEW — claim, capacity, withdrawal, personal link scope
    ├── WishConcurrencyTests.cs           # NEW — 100 simultaneous claims for one place (SC-002)
    ├── WishListScaleTests.cs             # NEW — SC-009 at FR-054's scale
    ├── WishRetentionTests.cs             # NEW — the sweep leaves wish lists alone (FR-037)
    └── RestoreTests.cs                   # EDIT — the preview counts wish lists (R-10)

frontend/src/
├── components/admin/
│   ├── WishListsView.vue                 # NEW — the area: list, empty/unreadable states
│   ├── WishListForm.vue                  # NEW — revealed on demand
│   └── WishListDetailView.vue            # NEW — items, claims, editing
├── components/wish/
│   ├── WishListView.vue                  # NEW — /w/:listToken, the participant page
│   └── ClaimView.vue                     # NEW — /z/:claimToken, the personal link
├── components/admin/AdminNav.vue         # EDIT — one entry, before settings
├── components/admin/DashboardView.vue    # EDIT — the wish-list overview region
├── stores/wishLists.ts                   # NEW
├── router.ts                             # EDIT — four routes
└── locales/de.json                       # EDIT — wish.*, nav.wishLists, new error.*

e2e/tests/
├── wish-list-journey.spec.ts             # NEW — create → share → claim → refuse → withdraw → status
└── zero-signup.spec.ts                   # EDIT — a wish-list link shows no navigation, no /admin

specs/007-admin-shell-dashboard/spec.md   # EDIT — FR-034 narrowed, amendment dated (R-6, FR-048b)
```

**Structure Decision**: The existing web-application layout is kept unchanged. Backend domain code
goes in a new `Wishes/` folder beside `Polls/` — a sibling, not a shared abstraction over both
(Principle III). Participant components go in `components/wish/` beside `components/poll/`, which
keeps the Principle I surfaces visually separate from the admin ones and keeps
`grep -r components/poll` a meaningful question. No new project and no new test project: the three
existing suites gain files.

## Phase outputs

| Phase | Output | State |
|---|---|---|
| 0 | [research.md](./research.md) — R-1 … R-14, no unresolved unknowns | done |
| 1 | [data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml), [contracts/ui-contract.md](./contracts/ui-contract.md), [quickstart.md](./quickstart.md), `CLAUDE.md` updated | done |
| 2 | `tasks.md` | **not created by this command** — run `/speckit-tasks` |

Task ordering that `/speckit-tasks` must preserve (Principle II, R-14):

1. The migration and entities, then **`WishConcurrencyTests` before `ClaimService`** — capacity is
   the requirement that cannot be retrofitted.
2. Validation tests before `WishListService`; the closed-boundary test before `BerlinClock` changes.
3. The 007 FR-034 amendment (R-6) **before** the dashboard region that depends on it, so the
   contradiction never exists in the repository even briefly.
4. The restore-preview correction (R-10) with its test, independent of the rest.
5. Frontend per user story: US1 (create + claim), US2 (edit + delete), US3 (status + dashboard),
   each independently demonstrable.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

**Empty — no gate is violated, and nothing here needs an exemption.** The two places where a more
elaborate design was available are recorded in research.md as rejections (R-2's submission table,
R-9's second rate limiter) rather than as justified complexity, which is the difference between
this table staying empty and being filled in.
