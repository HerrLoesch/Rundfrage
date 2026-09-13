---
description: "Task list for feature 008 — Wunschliste (Wish List)"
---

# Tasks: Wunschliste (Wish List)

**Input**: Design documents from `/specs/008-wishlist/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: MANDATORY (Constitution Principle II). Every behaviour task is preceded by the test that
defines it, and that test MUST be observed failing for the intended reason before implementation
begins. Two tasks are explicitly *guard* tests that pass on first run — both say so, and both exist
to fail later if somebody changes something they should not.

**Organization**: By user story, so each is independently implementable, testable and demonstrable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelisable — different files, no dependency on an incomplete task
- **[Story]**: US1 / US2 / US3, mapping to spec.md's user stories

## Path Conventions

Web application, per plan.md: `backend/src/Rundfrage.Api/`, `backend/tests/`, `frontend/src/`,
`e2e/tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: The cross-feature corrections and the shared vocabulary, done first so no later task
has to work around them.

- [X] T001 [P] Amend feature 007's FR-034 in `specs/007-admin-shell-dashboard/spec.md`: narrow it to
      permit wish-list titles on the dashboard while keeping participant names and poll titles off
      it, with a dated amendment note naming 008 FR-048b (research.md R-6). Do this before any
      dashboard work, so the contradiction never exists in the repository.
- [X] T002 [P] Add the new refusal codes to `backend/src/Rundfrage.Api/Http/ErrorCodes.cs`:
      `target_date_required`, `items_required`, `item_name_required`, `item_name_too_long`,
      `duplicate_item_name`, `wanted_count_invalid`, `too_many_items`, `too_many_places`,
      `count_below_entries`, `item_full`, `wish_list_closed`, `unknown_item`, `description_too_long`
      (data-model.md, "Validation rules and their refusal codes").
- [X] T003 [P] Add the German strings to `frontend/src/locales/de.json`: a new `wish.*` block,
      `nav.wishLists` = "Wunschlisten", and one `error.*` entry per code from T002. No literal
      string may appear in any component later (FR-055).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The clock, the three entities and the schema. Nothing in any user story can be built
before this phase is complete.

**⚠️ CRITICAL**: no user story work begins until the checkpoint below.

- [X] T004 [P] Write failing unit tests for the day boundary in
      `backend/tests/Rundfrage.Api.UnitTests/WishClosingTests.cs`: `EndOfDayUtc` for a winter day, a
      summer day, and the day of a summer-time transition; and that a list is open at 23:59:59 of
      its target day and closed at 00:00:00 of the next, in Europe/Berlin (FR-028a, FR-002).
- [X] T005 Implement `EndOfDayUtc(DateOnly)` in `backend/src/Rundfrage.Api/Time/BerlinClock.cs` and
      refactor `RetentionDeadlineFor` to use it, so there is one expression for "when does this day
      end" rather than two (research.md R-3).
- [X] T006 Extend `backend/tests/Rundfrage.Api.IntegrationTests/SchemaCreationTests.cs` with a
      failing assertion that a fresh installation has `WishLists`, `WishItems` and `WishClaims` with
      their indexes — including the unique index on `(WishItems.WishListId, Name)` that enforces
      FR-008.
- [X] T007 [P] Create the `WishList` entity in
      `backend/src/Rundfrage.Api/Data/Entities/WishList.cs` with the constants `TitleMaxLength` 300,
      `DescriptionMaxLength` 2000, `MaxItems` 100, `MaxPlaces` 1000 (data-model.md).
- [X] T008 [P] Create the `WishItem` entity in
      `backend/src/Rundfrage.Api/Data/Entities/WishItem.cs` with `NameMaxLength` 200,
      `MaxWantedCount` 50, `DefaultWantedCount` 1, and `Position` (data-model.md).
- [X] T009 [P] Create the `WishClaim` entity in
      `backend/src/Rundfrage.Api/Data/Entities/WishClaim.cs` with `DisplayNameMaxLength` 100,
      `ClaimToken` and `SubmittedAt`. No IP address, no user agent, no identity of any kind — repeat
      the comment `PollResponse` carries and say why (Principle IV).
- [X] T010 Configure the three entities in
      `backend/src/Rundfrage.Api/Data/RundfrageDbContext.cs`: `DbSet`s, cascade deletes list → item
      → claim, unique index on `WishLists.ListToken`, unique index on `(WishItems.WishListId, Name)`
      (FR-008 enforced by the database), non-unique index on `WishClaims.ClaimToken` (research.md
      R-2) and on `WishLists.TargetDate`. Deliberately **no** index or uniqueness on `DisplayName`
      (FR-020) — state that in a comment.
- [X] T011 Generate the migration (`dotnet ef migrations add WishLists --project
      src/Rundfrage.Api`) into `backend/src/Rundfrage.Api/Data/Migrations/` and confirm T006 passes.
      No existing table may appear in the diff.
- [X] T012 Write the retention guard test in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishRetentionTests.cs`: after the erasure sweep
      runs with expired polls present, every wish list and claim is still there (FR-037).
      **This test passes on first run.** It exists so that generalising `RetentionService` later
      fails loudly instead of quietly deleting wish lists (research.md R-11).

**Checkpoint**: schema in place, the clock knows when a day ends, and nothing erases a wish list.

---

## Phase 3: User Story 1 — Ask for things and let people claim them (Priority: P1) 🎯 MVP

**Goal**: The whole loop. An operator creates a list with items and quantities, gets a link, and
whoever holds it writes their name against what they will bring — with capacity honoured exactly,
and a personal link to withdraw again.

**Independent Test**: Create a list with one item wanted twice, open the link in a fresh browser
with no session, enter two names, confirm the item then refuses a third while another still accepts
one, then withdraw one through the personal link and confirm the place is claimable again.

### Tests for User Story 1 ⚠️ write first, watch fail

- [X] T013 [P] [US1] Unit tests for creation validation in
      `backend/tests/Rundfrage.Api.UnitTests/WishListValidationTests.cs`: missing title, title and
      description over their limits, missing target date, a past target date **accepted**
      (FR-002a), no items, empty item name, item name too long, duplicate item name, wanted count 0
      / −1 / 51, more than 100 items, and wished places over 1000 with the remaining headroom in the
      refusal; and creation **without** a description succeeding (FR-003) (FR-001…FR-010a).
- [X] T014 [P] [US1] Integration tests for the admin creation route in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishListAdminTests.cs`: `POST
      /api/v1/admin/wish-lists` stores the list and returns a participant token; **an item with no
      stated number is wanted exactly once** (FR-006); each refusal from T013 comes back as its code;
      an unauthenticated request is refused without revealing anything (FR-011). **Also `GET
      /api/v1/admin/wish-lists`**: one row per list carrying `entryCount`, `placeCount`,
      `untakenItemCount`, `completeItemCount` and `closed`, in the order FR-048c fixes with a mix of
      open and closed lists — written before T022, so the projection is defined by a test rather
      than described by one (FR-044, FR-049).
- [X] T015 [P] [US1] Integration tests for the participant read in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishClaimTests.cs`: `GET
      /api/v1/wish-lists/{listToken}` returns title, description, target date, items in the
      operator's order, wanted counts, open places and the names already entered (FR-009, FR-013,
      FR-019); unknown, malformed and deleted tokens produce one identical response (FR-024,
      SC-012); with maintenance mode on the route answers `{"code":"maintenance"}` (FR-025).
- [X] T016 [P] [US1] Integration tests for claiming in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishClaimTests.cs`: a claim is stored and the
      item's open places drop; several items in one submission produce **one** claim token
      (FR-022b); `item_full` once the wanted count is reached, carrying the item's current state
      (FR-017, FR-017b); `unknown_item` for an item from another list; the display-name refusals;
      the same name accepted twice on one item (FR-020); the 11th submission in an hour refused with
      `too_many_requests` while a submission claiming ten items costs **one** of the ten (FR-023,
      SC-002a).
- [X] T017 [P] [US1] Integration tests for the personal link in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishClaimTests.cs`: `GET
      /api/v1/claims/{claimToken}` returns exactly the entries of that submission; withdrawing one
      removes it and opens its place immediately (FR-022a); the token grants nothing else — no other
      entry, no other list, no admin function (FR-022c); after the list is deleted the token answers
      like an unknown one (FR-022e); a withdrawal counts against the rate-limit budget (FR-023a).
- [X] T018 [P] [US1] The concurrency test in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishConcurrencyTests.cs`, modelled on
      `ConcurrentWriteTests`: 100 simultaneous claims aimed at a single open place leave exactly one
      entry, and an item wanted *n* times never holds more than *n* (FR-017a, SC-002). **Write this
      before T020** — capacity cannot be retrofitted.
- [X] T019 [P] [US1] Frontend unit tests in `frontend/tests/unit/WishListView.spec.ts` and
      `frontend/tests/unit/WishListForm.spec.ts` and `frontend/tests/unit/WishListsView.spec.ts`:
      `WishListView.vue` renders open
      places as text, marks a complete item in words rather than by colour alone (FR-018, FR-057),
      shows the visibility notice before the name field (FR-019a), and shows no entry control for a
      complete item, and updates an item's open places after a claim without a manual reload of the
      page (FR-021); `WishListForm.vue` defaults a new item's quantity to 1 (FR-006);
      `WishListsView.vue` lists the stored lists, offers creation from its empty state (FR-042a),
      keeps empty, unreadable and loading visibly distinct (FR-046), and contains **zero
      installation-wide controls** — no maintenance switch, no backup, no restore (FR-047).

### Implementation for User Story 1

- [X] T020 [US1] Implement `WishListService` in `backend/src/Rundfrage.Api/Wishes/WishListService.cs`:
      `Validate` (all rules of T013, including the wished-places cap checked **only** on operator
      writes — FR-010a, research.md R-4) and `CreateAsync`, minting the list token with
      `CapabilityToken.Mint()`.
- [X] T021 [US1] Implement `ClaimService` in `backend/src/Rundfrage.Api/Wishes/ClaimService.cs`:
      claim inside one **immediate** write transaction, reusing the helper shape of
      `ResponseService.SubmitAsync` (research.md R-1); one minted `ClaimToken` shared by every claim
      of the submission (research.md R-2); `WithdrawAsync` by token and claim id. Log identifiers
      and counts only — never a name, an item name or a token.
- [X] T022 [US1] Implement `WishListProjection` in
      `backend/src/Rundfrage.Api/Wishes/WishListProjection.cs`: the summary rows with `entryCount`,
      `placeCount`, `untakenItemCount`, `completeItemCount` and `closed`, ordered open-first by
      nearest target date then closed by most recently closed (FR-048c), computed with grouped
      aggregates and a fixed number of queries (research.md R-12). **No percentage** (research.md
      R-7).
- [X] T023 [US1] Implement the admin routes in
      `backend/src/Rundfrage.Api/Endpoints/Admin/WishListAdminEndpoints.cs`: `POST
      /admin/wish-lists`, `GET /admin/wish-lists`, `GET /admin/wish-lists/{wishListId}` (neutral 404
      on a miss, as `PollAdminEndpoints` does).
- [X] T024 [US1] Implement the participant routes in
      `backend/src/Rundfrage.Api/Endpoints/Public/WishEndpoints.cs`: `GET` and `POST
      /wish-lists/{listToken}`, `GET /claims/{claimToken}`, `DELETE /claims/{claimToken}/{claimId}`.
      `AllowAnonymous`, the existing `RateLimiting.SubmissionPolicy` on the two writes (research.md
      R-9), no shape check before a token lookup, `NeutralNotFound.Result()` on every miss
      (research.md R-13).
- [X] T025 [US1] Register the three services and map both route groups in
      `backend/src/Rundfrage.Api/Program.cs`, following the existing registrations.
- [X] T026 [P] [US1] Create the store `frontend/src/stores/wishLists.ts`: create, list, load one, and
      the participant calls; a 401 from an admin call routes to the sign-in form, as the existing
      stores do.
- [X] T027 [P] [US1] Create `frontend/src/components/wish/WishListView.vue` — the participant page
      for `/w/:listToken`: title, description, target date, items with open places in text, names
      entered, one name field, several items in one submission, the personal link shown afterwards
      through the existing `ShareLink.vue`, the existing `MaintenanceNotice.vue` on 503.
- [X] T028 [P] [US1] Create `frontend/src/components/wish/ClaimView.vue` for `/z/:claimToken`: the
      entries this link covers, a withdraw control per entry with a confirmation naming the item and
      the name, and a plain message when an entry is already gone.
- [X] T029 [P] [US1] Create `frontend/src/components/admin/WishListForm.vue`: title, description,
      target date, and items with their quantities defaulting to 1; revealed on demand, discarded on
      close, kept with its reason on a refusal (FR-042).
- [X] T030 [US1] Create `frontend/src/components/admin/WishListsView.vue` in its first form: the list
      of wish lists with title, target date and participant link, the create action revealing T029,
      and the empty state offering creation directly (FR-042a). Figures come in US3.
- [X] T031 [US1] Add the four routes to `frontend/src/router.ts`: `/admin/wunschlisten` and
      `/admin/wunschlisten/:wishListId` as children of `AdminShell`, `/w/:listToken` and
      `/z/:claimToken` inside `BareShell` (contracts/ui-contract.md §2).
- [X] T032 [US1] Add the "Wunschlisten" entry to `frontend/src/components/admin/AdminNav.vue`,
      between the poll entry and settings, owning both wish-list route names so the area stays
      current on the detail page, with exactly one entry carrying `aria-current` (FR-041).
- [X] T033 [US1] End-to-end journey in `e2e/tests/wish-list-journey.spec.ts`: sign in → create a list
      with an item wanted twice → copy the link → in a context with no session claim twice → the
      third is refused → withdraw through the personal link → the place is free again. Then reload
      `/admin/wunschlisten/:wishListId` directly and confirm the same list is shown without going
      through the list first (SC-008).
- [X] T034 [US1] Extend `e2e/tests/zero-signup.spec.ts`: a participant on `/w/:listToken` sees no
      navigation, no sign-out and zero `a[href*="/admin"]` (FR-026, SC-001).

**Checkpoint**: a group can divide up what needs bringing. This is the MVP and is deployable alone.

---

## Phase 4: User Story 2 — Change the list after it is out (Priority: P2)

**Goal**: Editing a shared list without breaking the link or losing a name, the closed state that
follows the target date, and deletion that only ever happens because the operator said so.

**Independent Test**: Create a list, enter two names, rename the item and raise its count, confirm
both names survive under the new name; attempt to lower the count below two and confirm the refusal
names the two entries; move the target date into the past and confirm the list closes; move it
forward and confirm it reopens; delete the list and confirm the link behaves like an unknown one.

### Tests for User Story 2 ⚠️ write first, watch fail

- [X] T035 [P] [US2] Unit tests in
      `backend/tests/Rundfrage.Api.UnitTests/WishListValidationTests.cs`: lowering a wanted count
      below the claims already made is refused and the refusal carries that number (FR-033);
      lowering to exactly that number is allowed; renaming into an existing name is refused
      (FR-008); raising a count past the 1000-place cap is refused with the remaining headroom
      (FR-010a).
- [X] T036 [P] [US2] Integration tests for editing in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishListAdminTests.cs`: `PATCH` the title,
      description and target date; add an item; rename an item and find its claims under the new
      name (FR-031); raise a count and see places open (FR-032); every edit leaves the list token
      unchanged (FR-035) and removes no claim (FR-036); removing an item destroys exactly its claims
      (FR-034); deleting one claim leaves the rest untouched and opens its place (FR-043a).
- [X] T037 [P] [US2] Integration tests for the closed state in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishClaimTests.cs`, driving the clock through
      the existing `TimeProvider` seam: after the target day ends, the participant read still returns
      everything with `closed: true` (FR-028b); a claim is refused with `wish_list_closed` (FR-028a)
      judged on arrival rather than on what the page showed (FR-028e); a withdrawal is refused the
      same way (FR-028d); the operator can still delete a claim (data-model.md state table); moving
      the target date forward reopens the list with items and claims unchanged (FR-028c).
- [X] T038 [P] [US2] Integration test for deletion in `WishListAdminTests.cs`: `DELETE` removes the
      list, its items and every claim; afterwards the participant link and every personal link
      answer exactly as an unknown link does (FR-037…FR-039, FR-022e).
- [X] T039 [P] [US2] Frontend unit tests in `frontend/tests/unit/WishListDetailView.spec.ts` and
      `frontend/tests/unit/WishListView.spec.ts`: `WishListDetailView.vue` shows the refusal texts for
      `count_below_entries`, `too_many_places` and `duplicate_item_name` with their numbers
      (contracts/ui-contract.md §4), and deleting the last claim leaves the operator on the detail
      page showing its empty state; `WishListView.vue` renders a closed list with no entry form and
      says why (FR-028b).

### Implementation for User Story 2

- [X] T040 [US2] Extend `backend/src/Rundfrage.Api/Wishes/WishListService.cs`: `UpdateAsync`,
      `AddItemAsync`, `UpdateItemAsync` (rename and count, with the T035 rules), `RemoveItemAsync`,
      `DeleteAsync`, `DeleteClaimAsync`.
- [X] T041 [US2] Add the closed check to `backend/src/Rundfrage.Api/Wishes/ClaimService.cs` for both
      writes, evaluated against `BerlinClock` at the moment the request arrives (FR-028a, FR-028e).
      Nothing is stored — no status column, no flag (FR-028c).
- [X] T042 [US2] Add the routes to
      `backend/src/Rundfrage.Api/Endpoints/Admin/WishListAdminEndpoints.cs`: `PATCH` and `DELETE` on
      a list, `POST` items, `PATCH` and `DELETE` on an item, `DELETE` on a claim
      (contracts/openapi.yaml).
- [X] T043 [US2] Create `frontend/src/components/admin/WishListDetailView.vue`: the list's fields
      editable in place, every item with its claims, add / rename / change count / remove item,
      per-claim deletion, the removal confirmation naming how many entries die, and a way back to
      the list.
- [X] T044 [US2] Show the closed state in `frontend/src/components/wish/WishListView.vue` and
      `ClaimView.vue`: everything still shown, marked closed, no entry form and no withdraw control
      (FR-028b, FR-028d).
- [X] T045 [US2] Mark closed lists in `frontend/src/components/admin/WishListsView.vue` and wire
      deletion through the existing `DeleteConfirm.vue`, stating how many entries will be destroyed
      (FR-038).
- [X] T046 [US2] Extend `e2e/tests/wish-list-journey.spec.ts`: rename an item, raise its count, watch
      a lowering refused with the number, delete the list, and confirm the old link is
      indistinguishable from an unknown one.

**Checkpoint**: US1 and US2 both work; a list survives its own plan changing.

---

## Phase 5: User Story 3 — Read the state without opening anything (Priority: P3)

**Goal**: The status figures in the area, and the dashboard's overview of every wish list.

**Independent Test**: With two lists of known items, counts and entries, confirm the entry counts,
the filled share and the untaken-items count in the area and on the dashboard match a hand count;
delete one entry and confirm every affected figure follows.

### Tests for User Story 3 ⚠️ write first, watch fail

- [X] T047 [P] [US3] Integration tests for the figures in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishListAdminTests.cs`: `entryCount`,
      `placeCount`, `untakenItemCount` and `completeItemCount` against a hand-built fixture; they
      follow a claim, a withdrawal, an operator deletion, an item removal and a raised count
      (SC-004); the ordering of FR-048c holds with a mix of open and closed lists.
- [X] T048 [P] [US3] Integration scale test in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishListScaleTests.cs`: at FR-054's documented
      scale — 200 lists holding 200,000 claims — `GET /admin/wish-lists` answers within SC-009's
      budget, and the number of queries does not grow with the number of lists (research.md R-12).
      Follow the shape of `DashboardScaleFixture`.
- [X] T049 [P] [US3] Frontend unit tests in `frontend/tests/unit/WishListsView.spec.ts`: the area's
      row shows entries of places and a filled share
      **rounded down** — 999 of 1000 reads 99 %, never 100 % (research.md R-7) — labels the untaken
      count separately from the share (FR-045a), and says "Vollständig" in words when complete
      (FR-045b); the empty, unreadable and loading states are three distinguishable things and none
      renders a zero (FR-046).
- [X] T050 [P] [US3] Frontend unit tests in `frontend/tests/unit/DashboardView.spec.ts`: one row per
      wish list in the API's
      order, each leading into the area in one action (FR-048d), no create/edit/delete control
      (FR-051), **no participant display name anywhere on the dashboard** (FR-050), the total stated,
      the region bounded and scrolling within itself (FR-048c), and its own empty and unreadable
      states (FR-052).

### Implementation for User Story 3

- [X] T051 [US3] Show the figures in `frontend/src/components/admin/WishListsView.vue`: the counts,
      the rounded-down share, the separately labelled untaken and complete counts, and the
      "Vollständig" wording (FR-045, FR-045a, FR-045b).
- [X] T052 [US3] Add the wish-list region to `frontend/src/components/admin/DashboardView.vue`,
      reading the same `list()` the area reads (research.md R-5) — one row per list, ordered by the
      API, bounded in height, each row a link into the area, re-read on entering the dashboard
      (FR-053).
- [X] T053 [US3] Add the dashboard's wish-list strings to `frontend/src/locales/de.json` if T003 left
      any gap, and confirm no literal string reached a component (FR-055).
- [X] T054 [US3] Extend `e2e/tests/wish-list-journey.spec.ts`: after claiming, the area shows the
      figures and the dashboard names the list with the same numbers (FR-049), and no participant
      name appears on the dashboard.

**Checkpoint**: all three stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T055 [P] Write a failing test in
      `backend/tests/Rundfrage.Api.IntegrationTests/RestoreTests.cs`: a restore preview taken against
      a backup without wish lists, while the installation holds some, reports how many wish lists and
      claims would be lost (research.md R-10).
- [X] T056 Extend the preview in `backend/src/Rundfrage.Api/Data/RestoreService.cs` with the wish-list
      and claim counts, so the confirmation cannot understate what a restore destroys. The restore
      mechanism itself does not change — it already replaces the whole file.
- [X] T057 [P] Show the two new counts in `frontend/src/components/admin/RestorePanel.vue` and add
      their strings to `frontend/src/locales/de.json`.
- [X] T058 [P] Accessibility and layout pass over the new surfaces, asserted in
      `frontend/tests/unit/WishListView.spec.ts` and `frontend/tests/unit/WishListDetailView.spec.ts`:
      keyboard-only operation, a text
      label on every control, visible focus, open/full/closed conveyed in text rather than colour,
      and usability at 375 px with 100 items (FR-056, FR-057, FR-058, SC-010), plus the 375 px check
      in `e2e/tests/wish-list-journey.spec.ts`. Assertions, not a manual pass.
- [X] T059 [P] Update `README.md` with a "Wunschliste" section between "Ergebnisse" and
      "Exportieren": what it is, how the link works, that a list is deleted only manually, and that a
      passed target date closes it.
- [X] T060 Run `quickstart.md` end to end — the curl walkthrough, the migration check and all four
      suites (`frontend: npm run test:unit && npm run typecheck && npm run build`, `backend: dotnet
      test`, `e2e: npm test`) — and record the totals.
      **Done (2026-09-13, after the review below)**: frontend 297 unit tests, typecheck and build
      green; backend 139 unit and 264 integration tests green; **end-to-end 60 tests green**,
      including all four of `wish-list-journey.spec.ts`.
      The earlier note here claimed `.env` held only the password hash and left the suite unrun.
      That was wrong — `.env` carries `E2E_ADMIN_USER` and `E2E_ADMIN_PASSWORD` too. What the suite
      actually needed was the container started with `SUBMISSION_LIMIT_PER_HOUR` raised, now
      written down in `quickstart.md`.

- [X] T062 Review pass over the delivered feature (2026-09-13). Eleven findings, all fixed; the
      four that mattered were only reachable because the end-to-end suite had never been run:
      1. `WishListForm.vue` carried no `data-testid`, while the unit suite waited for
         `wish-list-form`, the e2e helper for the same, and `ui-contract.md` named a third
         spelling. One failing unit test and **all four e2e tests blocked in their first step**.
         Fixed on the form; the contract now names both the action and the form.
      2. `ClaimView.vue` treated the neutral 404 that follows withdrawing the *last* covered entry
         as "this link is unknown", replacing the confirmation with a false warning. Now it keeps
         the page and says the entries are gone (`claim-entries-empty`). Regression test added.
      3. `PATCH /admin/wish-lists/{id}` answered **500** to `null`, `[]`, `5` and `"x"` — the raw
         document read of FR-029 cannot be asked of a non-object. Now `malformed_request`/400,
         with a theory covering all four plus `{}`.
      4. `signIn` was imported and unused in the e2e spec — a `tsc` error.
      5. FR-023a (a withdrawal spends one of the ten) was implemented but untested. Test added.
      6. The unique index on `(WishListId, Name)` was BINARY while the service compares with
         `OrdinalIgnoreCase`, so it backstopped a rule it did not share. `Name` now carries
         `COLLATE NOCASE`; the migration was regenerated. Two tests added, one of them writing
         past the service straight at the index.
      7. `ui-contract.md` demanded `ShareLink.vue` and `DeleteConfirm.vue` in the list rows. The
         code was right and the contract wrong — `PollList.vue` already sets the row pattern — but
         the row *had* dropped that pattern's new-tab `aria-describedby` note and showed a bare
         `/w/...` path instead of the absolute address people paste. Both restored; contract
         amended.
      8. `WishListProjection.ListAsync` read every item row and grouped in memory while its
         comment claimed a grouped aggregate. Now genuinely one grouped query: the 200-list
         scale test went from 0.66 s to **0.01 s**.
      9. A refused rename or count change left the field showing the rejected value while the
         store held the truth. Both fields are restored on refusal. Two tests added.
      10. `duplicate_item_name` did not name what it collided with, as the contract requires.
          Refusals now carry `detail`; the German string uses it.
      11. `stores/wishLists.ts` said "prepended rather than re-fetched" above an `await load()`.
- [X] T061 Re-check the Constitution Check in `specs/008-wishlist/plan.md` against the delivered code
      and fill in a post-implementation section, stating any ordering deviation honestly rather than
      tidying it away (constitution, Development Workflow gate 1).

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (T001–T003)**: no dependencies; all three parallel.
- **Foundational (T004–T012)**: needs Setup. **Blocks every user story.**
- **US1 (T013–T034)**: needs Foundational. No dependency on US2 or US3.
- **US2 (T035–T046)**: needs Foundational; in practice builds on US1's services and components.
- **US3 (T047–T054)**: needs Foundational and T022 (the projection, built in US1). T052 additionally
  needs **T001** — the 007 amendment must land before the dashboard names a wish list.
- **Polish (T055–T061)**: T055–T057 are independent of the user stories and can be done any time
  after Foundational; T058–T061 need the stories they cover.

### Within each story

- Tests first, observed failing, then implementation. **T018 before T021** is the one ordering that
  must not be relaxed: capacity under concurrency cannot be retrofitted.
- Entities → DbContext → migration → services → endpoints → store → components → routes → e2e.

### Parallel opportunities

- T001, T002, T003 together.
- T007, T008, T009 together (three entity files).
- T013–T019 together (seven test files, all failing, before any US1 implementation).
- T026–T029 together (store and three components, different files).
- T035–T039 together; T047–T050 together.
- T055 and T057–T059 together.

---

## Parallel Example: User Story 1

```bash
# All US1 tests first, in parallel — then watch them fail for the right reasons:
Task: "Unit tests for creation validation in backend/tests/Rundfrage.Api.UnitTests/WishListValidationTests.cs"
Task: "Integration tests for the admin creation route in .../WishListAdminTests.cs"
Task: "Integration tests for the participant read in .../WishClaimTests.cs"
Task: "Concurrency test for one open place in .../WishConcurrencyTests.cs"
Task: "Frontend unit tests in frontend/tests/unit/WishListView.spec.ts and WishListForm.spec.ts"

# Then the frontend pieces, in parallel:
Task: "Create frontend/src/stores/wishLists.ts"
Task: "Create frontend/src/components/wish/WishListView.vue"
Task: "Create frontend/src/components/wish/ClaimView.vue"
Task: "Create frontend/src/components/admin/WishListForm.vue"
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US1.
2. **Stop and validate**: a group can divide up what needs bringing, from one link, with no account,
   and no item ever holds more names than are wanted.
3. Deployable as it stands. A list that never closes and cannot be edited is still useful.

### Incremental delivery

- **+ US2**: the list survives its own plan changing, and closes when the day passes.
- **+ US3**: the operator can supervise several lists without opening any.
- **+ Polish**: the restore preview stops understating what it destroys (T055–T057) — do this before
  any deployment where an operator might restore a backup, regardless of which stories have shipped.

### What to watch

- **T018 is the test that matters.** If it is written after `ClaimService`, it will be written to
  pass rather than to define, and SC-002 stops meaning anything.
- **T012 and T055 are guard tests.** T012 passes immediately; T055 must fail first.
- **Do not generalise polls and wish lists into a "survey type".** There is now a second case, which
  is exactly when the temptation appears; research.md records that they share mechanisms, not
  behaviour (Principle III).
