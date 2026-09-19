---
description: "Task list for feature 009 — Ersteller-Links (Creator Links)"
---

# Tasks: Ersteller-Links (Creator Links)

**Input**: Design documents from `/specs/009-creator-links/`
**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md),
[data-model.md](./data-model.md), [contracts/](./contracts/)

**Tests**: MANDATORY (Constitution Principle II). Every behaviour task is preceded by the test that
defines it, and that test MUST be observed failing **for the intended reason** before implementation
begins. Four tasks are explicitly *guard* tests that pass on their first run — each says so, and
each exists to fail later if somebody changes something they should not.

**Organization**: By user story, so each is independently implementable, testable and demonstrable.

> **Revision 2 (2026-09-14, after `/speckit-analyze`).** Two Principle II ordering violations were
> corrected: the isolation filter and the cap of 100 were both implemented phases before their
> tests. `OwnerScopeTests` (T009) and `CreatorLimitTests` (T008) now sit in Phase 2 ahead of the code
> they define, and the route-level isolation suite stays in Phase 4 where it belongs. Six coverage
> gaps were closed — rename, the backup round-trip, session refusal on the new admin routes, export
> equality, the creator edit/delete happy path, and import ownership. Task numbers changed; if you
> were working against revision 1, re-read the phase you are in.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelisable — different files, no dependency on an incomplete task
- **[Story]**: US1 / US2 / US3 / US4, mapping to spec.md's user stories

## Path Conventions

Web application, per plan.md: `backend/src/Rundfrage.Api/`, `backend/tests/`, `frontend/src/`,
`e2e/tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: The vocabulary and the one-line hardening, done first so no later task has to work
around them.

- [X] T001 [P] Add this feature's refusal codes to `backend/src/Rundfrage.Api/Http/ErrorCodes.cs`:
      `creator_name_required`, `creator_name_too_long`, `creator_name_duplicate` (carries `detail` =
      the colliding name), `creator_limit_reached` (carries `limit` = 100). Named constants, for the
      reason that file already records: a code existing in only one of the two places is a blank
      message in front of an operator.
- [X] T002 [P] Add the German strings to `frontend/src/locales/de.json`: a new top-level `creator`
      key and `nav.creators` = "Ersteller". Include the revoke and delete confirmation texts, which
      must differ in substance and not only in wording (FR-020b) — the revoke text states that
      **nothing it owns will be removed**, the delete text names both counts and says that an export
      must be taken first if anything is to be kept (FR-020c). No literal string may appear in any
      component — `frontend/tests/unit/noLiteralStrings.spec.ts` already enforces this and will fail
      on any string that skips the catalogue (FR-054).
- [X] T003 [P] Add `Referrer-Policy: same-origin` beside the existing cache-control logic in
      `backend/src/Rundfrage.Api/Program.cs`, and assert it in
      `backend/tests/Rundfrage.Api.IntegrationTests/StaticFileCachingTests.cs`. Defence in depth for
      a credential that now lives in a URL (research R-10) — write the assertion first.
- [X] T004 [P] Add `CREATOR_WRITE_LIMIT_PER_HOUR` to `.env.example` and to the end-to-end
      environment, raised there for the same reason `SUBMISSION_LIMIT_PER_HOUR` is raised: one
      machine legitimately exceeds the production number during a full run (research R-5).

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: no user story work may begin until this phase is complete. Ownership and the access
filter are what every later task builds on, and the migration is the one step that touches an
operator's existing data.

**Two tests here define behaviour that used to be written first and tested much later** — the
isolation filter (T009 before T015) and the cap of 100 (T008 before T017). Both are reachable
without any endpoint, so there is no excuse for deferring them, and both are the kind of defect that
looks like a working feature until somebody looks.

### Tests first ⚠️ write, watch fail

- [X] T005 [P] Write `backend/tests/Rundfrage.Api.IntegrationTests/MigrationOnPopulatedDatabaseTests.cs`:
      apply the new migration to a database **already holding** polls, responses, wish lists and
      claims; assert every row survives, that each poll and wish list is afterwards owned by the
      operator (`CreatorId IS NULL`), and that no participant, edit, list or claim token changed
      (FR-013, SC-009). Against a populated database, not an empty one — "the migration ran" and
      "the rows survived" are different claims (data-model §3).
- [X] T006 [P] Write `backend/tests/Rundfrage.Api.UnitTests/CreatorValidationTests.cs`: name required,
      name at most 100 characters, name distinct case-insensitively, and the refusal payloads of
      T001 including `detail` and `limit` (FR-001, FR-002, FR-003).
- [X] T007 [P] Write the SQLite behaviour test in
      `backend/tests/Rundfrage.Api.IntegrationTests/SchemaCreationTests.cs`: a `UNIQUE` index on a
      nullable column accepts many `NULL`s, so many revoked Ersteller coexist (research R-3). This
      asserts a **SQLite-specific guarantee the schema depends on**; it must not rest on folklore.
- [X] T008 [P] Write `backend/tests/Rundfrage.Api.IntegrationTests/CreatorLimitTests.cs` against
      `CreatorService` directly, **before T017 writes it**: the 100th is created and the 101st refused
      naming the limit; revoking does **not** free a place and deleting does; a deleted Ersteller's
      name becomes available again; and concurrent creation at the boundary never produces 101
      (FR-004, FR-005, FR-009, FR-009a, FR-002, SC-006, research R-11). The concurrency case is the
      reason this cannot wait for an endpoint — it is the half that a later test would find already
      broken in production.
- [X] T009 [P] Write `backend/tests/Rundfrage.Api.IntegrationTests/OwnerScopeTests.cs` against
      `OwnerScope` directly, **before T015 writes it**: with polls and wish lists owned by two
      Ersteller and by the operator, `Polls()` and `WishLists()` return that Ersteller's and nothing
      else; an expired poll of theirs is invisible to them exactly as it is to the operator (FR-014);
      and a wish list of theirs is never filtered by retention (008 FR-037). This is the feature's
      central safety property and it is reachable with no endpoint in existence (FR-032, FR-033,
      research R-1). The route-level suite that proves the same promise through HTTP is T041.

### Implementation

- [X] T010 Create `backend/src/Rundfrage.Api/Data/Entities/Creator.cs`: `Id`, `Name` (≤ 100),
      `LinkToken` (**nullable** — absent means revoked), `CreatedAt`, and the `MaxCreators = 100`
      constant. Document in XML remarks why there is no password, no contact detail, no `RevokedAt`
      and no audit field (data-model §1, FR-003, FR-040b, Principle IV).
- [X] T011 [P] Add `public Guid? CreatorId` to `backend/src/Rundfrage.Api/Data/Entities/Poll.cs`,
      with the remark that **`NULL` means the operator** and that SQL's three-valued logic makes
      `CreatorId != @id` exclude the operator's rows (FR-011, FR-012, research R-2).
- [X] T012 [P] Add the same nullable `CreatorId` to
      `backend/src/Rundfrage.Api/Data/Entities/WishList.cs` (FR-011, FR-012).
- [X] T013 Configure the schema in `backend/src/Rundfrage.Api/Data/RundfrageDbContext.cs`: the
      `Creators` set; unique `NOCASE` index on `Name`; unique index on `LinkToken`; non-unique
      indexes on `Polls.CreatorId` and `WishLists.CreatorId`; and **`OnDelete(DeleteBehavior.Cascade)`
      stated explicitly on both relationships**. Record in a comment that EF Core's default for an
      optional relationship is `ClientSetNull`, which would hand a deleted Ersteller's content to the
      operator — the outcome FR-020c forbids (research R-4).
- [X] T014 Generate the `AddCreators` migration in
      `backend/src/Rundfrage.Api/Data/Migrations/`. **No data migration**: FR-013 is satisfied by the
      column being nullable, and adding a step would be a step that can go wrong on somebody's
      production volume (data-model §3). Run T005 and watch it pass.
- [X] T015 Create `backend/src/Rundfrage.Api/Creators/OwnerScope.cs` — **the access filter** — until
      T009 passes. A scoped service exposing only `Polls()` and `WishLists()`, composed as
      data-model §5 specifies: polls compose with `RetentionService.LivePolls()` so ownership changes
      nothing about retention (FR-014); wish lists compose with no retention filter at all, matching
      the note `WishEndpoints.LoadAsync` already carries. Document that creator handlers are **not**
      given `RundfrageDbContext`, which is what makes FR-033 structural rather than a review comment.
- [X] T016 Extract `backend/src/Rundfrage.Api/Data/SqliteWriteTransaction.cs` from
      `ClaimService.BeginWriteTransactionAsync`, and have `ClaimService` call
      it. **Refactor only — no behaviour change**, guarded by the tests that caller already has; run
      `WishClaimTests` and `WishConcurrencyTests` before and after and compare. Second concrete use
      is Principle III's threshold for extraction (research R-11).
- [X] T017 Create `backend/src/Rundfrage.Api/Creators/CreatorService.cs` until T008 passes: create
      (name validation, token minting via `CapabilityToken.Mint()`, and the cap of 100 checked
      **inside** `SqliteWriteTransaction` so count-then-insert cannot race), rename, reissue, revoke,
      delete (FR-004, FR-005, FR-009, FR-016, FR-017, FR-018, FR-020, FR-020a). Log the creator id
      and counts only — never the token, never the name (research R-13, FR-037).
- [X] T018 [P] Create `backend/src/Rundfrage.Api/Creators/CreatorProjection.cs` producing
      `CreatorSummary` per contracts/openapi.yaml: name, created date, `hasLink` derived from the
      token being present, the token when present, and the two owned counts (FR-044, FR-044a).
- [X] T019 Register `OwnerScope`, `CreatorService` and `CreatorProjection` in
      `backend/src/Rundfrage.Api/Program.cs` as a **sibling** of the poll and wish services, not an
      abstraction over them — the note `Program.cs` already carries for feature 008 applies verbatim.

**Checkpoint**: ownership exists, the filter exists and is proven, nobody can reach either yet.

---

## Phase 3: User Story 1 — Hand somebody a link and let them make their own things (Priority: P1) 🎯 MVP

**Goal**: the operator issues a named link; its holder creates their own date polls and wish lists
through it, with no account and no password, and the participant links those produce work.

**Independent Test**: create one Ersteller link, open it in a fresh browser with no session, create
one wish list and one date poll through it, and confirm both produce working participant links that
an unrelated browser can answer.

### Tests for User Story 1 ⚠️ write first, watch fail

- [X] T020 [P] [US1] Write `backend/tests/Rundfrage.Api.IntegrationTests/CreatorSurfaceTests.cs`: a
      valid link returns the holder's name and their two lists with no session and no cookie; an
      unknown, malformed and revoked token each return the **byte-identical** neutral payload
      (FR-006, FR-007, FR-008). Assert there is no shape check short-circuit before the lookup — a
      malformed token must not be measurably faster than an unknown one (002 research R-4). Assert
      the surface carries no dashboard and no figure aggregating the two lists (FR-028b).
- [X] T021 [P] [US1] Write
      `backend/tests/Rundfrage.Api.IntegrationTests/CreatorCreationTests.cs` — the whole of what a
      holder may do with their own content. Create: a poll and a wish list made through a creator
      link are owned by that Ersteller, obey every limit of features 002 and 008 unchanged, and
      produce participant links that answer for a caller holding neither the creator link nor the
      operator password (FR-022, FR-023, FR-014). Read: answers, results, per-day summary, items,
      entries and status figures (FR-024, FR-025). **Edit**: title, description, target date, add
      item, rename item, raise and lower a wanted count, all through `/e/{token}` (FR-026).
      **Delete**: a poll, a wish list, one response, one claim (FR-027). **Export**: the document a
      creator receives for their own poll is **byte-for-byte** the document the operator receives for
      the same poll (FR-028, SC-004a).
- [X] T022 [P] [US1] Write `backend/tests/Rundfrage.Api.IntegrationTests/CreatorMaintenanceTests.cs`:
      with maintenance mode on, every creator route answers `{"code":"maintenance"}` and nothing is
      created (FR-050). **Guard test — expected to pass on its first run**, because the route prefix
      is what satisfies the requirement; it exists to fail the day somebody moves the creator group
      under `/api/v1/admin` and silently grants it that group's exemption (research R-6).
- [X] T023 [P] [US1] Write `backend/tests/Rundfrage.Api.IntegrationTests/CreatorRateLimitTests.cs`:
      60 writes in an hour from one source are accepted and the 61st is refused with the wait, the
      refused request changed nothing, reads are never refused, and — the part that is a framework
      behaviour this code depends on and does not own — the creator budget and the participant
      submission budget are **separate buckets for the same address** (FR-036, FR-036c, SC-010a,
      research R-5). Assert also that no ownership quota exists: one Ersteller may create far more
      than the rate limit allows in an hour, across several windows (FR-036a).
- [X] T024 [P] [US1] Write `backend/tests/Rundfrage.Api.IntegrationTests/CreatorLoggingTests.cs`:
      over a run exercising every creator route including its refusals, no log line contains a
      creator token or a creator name (FR-037, SC-013), and **no log line records who performed an
      admin action** (FR-040b), and **no request source is written to the log or to storage** by the
      new rate-limit policy (FR-036d). Extend the existing redaction fixtures rather than building a
      second log-capturing harness.
- [X] T025 [P] [US1] Write `frontend/tests/unit/CreatorSurface.spec.ts`: the
      greeting shows the name, both lists render, each empty list says so and offers creating
      directly, each list carries enough per-item summary to find something without opening it
      (FR-028g), the page states what the link grants **before** anything can be shared (FR-058),
      and the surface exposes **no** file input, no import control, no dashboard and no link into
      `/admin` (US1 scenarios 3, 4, 11, 12).

### Implementation for User Story 1

- [X] T026 [US1] Add the operator's `POST /admin/creators` to
      `backend/src/Rundfrage.Api/Endpoints/Admin/CreatorAdminEndpoints.cs` and map the group in
      `Program.cs`. Creation only — the rest of the management surface is US4. The session
      requirement is inherited from the admin group; do not repeat it per route (002 FR-048).
- [X] T027 [US1] Create `backend/src/Rundfrage.Api/Endpoints/Creator/CreatorEndpoints.cs` with the
      `/e/{creatorToken}` group and its endpoint filter resolving the token once per request into the
      scoped creator context (research R-1). Mount it in `Program.cs` **beside the participant
      routes, not under `/admin`** — and add a comment saying why, pointing at T022.
- [X] T028 [US1] Implement `GET /e/{creatorToken}` returning `CreatorSurface` — the holder's name and
      both lists, read exclusively through `OwnerScope` (FR-028d, FR-032).
- [X] T029 [P] [US1] Add the owner parameter to
      `backend/src/Rundfrage.Api/Polls/PollService.CreateAsync` and implement
      `POST /e/{creatorToken}/polls`, reusing feature 002's validation and refusal codes unchanged
      (FR-022).
- [X] T030 [P] [US1] Add the owner parameter to
      `backend/src/Rundfrage.Api/Wishes/WishListService.CreateAsync` and implement
      `POST /e/{creatorToken}/wish-lists`, reusing feature 008's validation and refusal codes
      unchanged (FR-023).
- [X] T031 [US1] Implement the read routes in `Endpoints/Creator/CreatorEndpoints.cs`:
      `GET /e/{creatorToken}/polls/{pollId}` returning feature 002/004/006's results payload, and
      `GET /e/{creatorToken}/wish-lists/{wishListId}` returning feature 008's detail payload
      (FR-024, FR-025).
- [X] T032 [US1] Implement the wish-list editing routes under `/e/{creatorToken}` in
      `Endpoints/Creator/CreatorEndpoints.cs` — patch list, add item, patch item, delete item —
      delegating to the existing `WishListService` methods (FR-026).
- [X] T033 [US1] Implement the deletion routes under `/e/{creatorToken}` in
      `Endpoints/Creator/CreatorEndpoints.cs`: a poll, a wish list, one response, one claim — each
      with the same confirmations and behaviour the operator gets (FR-027).
- [X] T034 [US1] Implement `GET /e/{creatorToken}/polls/{pollId}/export` in
      `Endpoints/Creator/CreatorEndpoints.cs`, reusing `PollExport` unchanged so the document is
      byte-for-byte the operator's (FR-028, SC-004a).
- [X] T035 [US1] Add `CreatorWritePolicy` at 60 per hour to
      `backend/src/Rundfrage.Api/Http/RateLimiting.cs`, configurable through the variable of T004,
      following the existing policy's construction so the request source is never written anywhere
      (FR-036d). Apply `.RequireRateLimiting(...)` in `Endpoints/Creator/CreatorEndpoints.cs` to
      every route that changes something and to **none** that only reads (FR-036c).
- [X] T036 [P] [US1] Add the creator route `/e/:creatorToken` to `frontend/src/router.ts`, inside
      `BareShell` beside the participant surfaces. **Exactly one route — no children, no query
      parameters, no `router.replace`** (FR-028d, FR-028e). Add the comment explaining the deliberate
      departure from 007 FR-014a, so a reviewer finds the reasoning before they "fix" the asymmetry
      (research R-9).
- [X] T037 [P] [US1] Create `frontend/src/stores/creator.ts`: the token from the route, both lists,
      and which disclosure is open. Separate from `polls.ts` and `wishLists.ts`, which talk to
      `/admin/**` — sharing a store would be sharing a URL prefix (ui-contract §7).
- [X] T038 [US1] Add the creator calls to `frontend/src/api/client.ts`, all under `/e/{token}`.
- [X] T039 [US1] Create `frontend/src/components/creator/CreatorSurface.vue` per ui-contract §4: the
      greeting, the FR-058 warning about what the link grants, both lists, and a poll's answers and a
      wish list's detail opening **in place** as component state (FR-028d). Reuse `PollForm.vue`,
      `WishListForm.vue` and the existing results and detail components rather than writing second
      ones.
- [X] T040 [US1] Add the `creator-*` test ids of ui-contract §4 to
      `frontend/src/components/creator/CreatorSurface.vue` and confirm reload behaviour: back to the
      top, both lists shown, an unconfirmed form discarded, nothing saved affected (FR-028f).

**Checkpoint**: a link can be issued and used end to end. This is the MVP.

---

## Phase 4: User Story 2 — Each link shows its holder their own and nothing else (Priority: P2)

**Goal**: two links, each showing only its own content; neither reaching the other's, the operator's,
or any installation-wide control.

**Independent Test**: issue two links, create one poll and one wish list through each, then confirm
from each link that only its own two items are listed and that a request naming the other's
identifiers is refused indistinguishably from one naming something that never existed.

### Tests for User Story 2 ⚠️ write first, watch fail

- [X] T041 [US2] Write `backend/tests/Rundfrage.Api.IntegrationTests/CreatorIsolationTests.cs` — the
      same promise T009 proved at the filter, now proved through HTTP on every route. With content
      owned by two Ersteller and by the operator: each link lists only its own; for every identifier
      belonging to another owner — poll, wish list, response, claim, export, and every edit and
      delete route — the response is byte-identical to the response for an identifier that never
      existed; and no count or figure anywhere on the surface includes anybody else's content
      (FR-032, FR-033, FR-034, FR-035, SC-002, SC-003). Table-driven over every operation in
      contracts/openapi.yaml, so a route added later without a case is visible.
- [X] T042 [P] [US2] Write
      `backend/tests/Rundfrage.Api.IntegrationTests/CreatorSurfaceRefusalTests.cs`: with a valid
      creator token, direct requests — skipping the interface entirely — to maintenance mode, the
      backup, the restore, settings, poll import, the dashboard and every `/admin/creators` route are
      all refused; **zero routes reachable with a creator token accept an uploaded file**; and
      holding the token establishes no operator session (FR-021, FR-028a, FR-029, FR-030, FR-031,
      SC-004).
- [X] T043 [P] [US2] Write the participant-surface guard in
      `e2e/tests/creator-journey.spec.ts`: a participant opening a link for an Ersteller's poll sees
      exactly feature 002's surface — no Ersteller name, no other content, no route into any creator
      or admin surface (FR-015, FR-048, US2 scenario 7). **Guard test — expected to pass on its
      first run**, because nothing in this feature touches that surface; it exists to fail if
      somebody later leaks an owner into it.
- [X] T044 [P] [US2] Extend `frontend/tests/unit/CreatorSurface.spec.ts`: the
      store's state never holds another owner's content, so nothing can be revealed by a rendering
      change alone.

### Implementation for User Story 2

- [X] T045 [US2] Route every read and write in
      `backend/src/Rundfrage.Api/Endpoints/Creator/CreatorEndpoints.cs` through `OwnerScope`, and
      remove any `RundfrageDbContext` parameter from every creator handler. A handler with no
      unscoped queryable in reach cannot leak (FR-033, research R-1). Run T041 and watch it pass.
- [X] T046 [US2] Make every cross-owner miss in
      `backend/src/Rundfrage.Api/Endpoints/Creator/CreatorEndpoints.cs` answer
      `NeutralNotFound.Result()` — never 403, never a message, never a distinguishing status
      (FR-034). Audit each handler against T041's table.
- [X] T047 [US2] Confirm SC-008: run `e2e/tests/date-poll-journey.spec.ts`,
      `e2e/tests/wish-list-journey.spec.ts` and `e2e/tests/results-summary.spec.ts` **unchanged** and
      record that they pass. Any edit needed to make them pass is a defect in this feature, not in
      the test.

**Checkpoint**: the isolation promise holds against direct requests, not only in the interface.

---

## Phase 5: User Story 3 — The operator still sees everything, and sees whose it is (Priority: P3)

**Goal**: every poll and wish list in the admin area regardless of owner, each row naming its owner,
with every admin action available whoever owns it.

**Independent Test**: with content owned by the operator and by two Ersteller, confirm the admin poll
list and wish-list list show all of it, that each row names its owner, and that the dashboard's
figures equal a hand count across all owners.

### Tests for User Story 3 ⚠️ write first, watch fail

- [X] T048 [P] [US3] Extend `backend/tests/Rundfrage.Api.IntegrationTests/PollListingTests.cs` and
      `backend/tests/Rundfrage.Api.IntegrationTests/WishListAdminTests.cs`: every poll and wish list
      is listed regardless of owner, each carries its owner's id and name, and the operator's own
      carry `null` for both (FR-038, FR-039, SC-005).
- [X] T049 [P] [US3] Write the parity suite in
      `backend/tests/Rundfrage.Api.IntegrationTests/WishListAdminTests.cs` and
      `backend/tests/Rundfrage.Api.IntegrationTests/PollListingTests.cs`: every admin action
      available on the operator's own content is available and behaves identically on an Ersteller's
      — with **zero refusals attributable to ownership** — and after any action short of deletion the
      content is still owned by the same Ersteller (FR-040, FR-040a, SC-005a, SC-005b). Assert also
      that the operator can export **and import** whatever the owner, and that **a poll the operator
      imports is owned by the operator** (FR-053).
- [X] T050 [P] [US3] Extend `backend/tests/Rundfrage.Api.IntegrationTests/DashboardTests.cs`: the
      figures count every owner's content, the wish-list overview rows name their owner, and **no
      participant display name appears anywhere on the dashboard** (FR-041, FR-042, 008 FR-050).
- [X] T051 [P] [US3] Add a guard test to
      `backend/tests/Rundfrage.Api.IntegrationTests/RetentionTests.cs` asserting `RetentionService`
      and the hourly sweep are unaffected by ownership: an Ersteller's poll expires on its deadline
      exactly as the operator's does, and an Ersteller's wish list never expires at all (FR-014).
      **Guard test — expected to pass on its first run.**

### Implementation for User Story 3

- [X] T052 [P] [US3] Add `CreatorId` and `CreatorName` to `PollSummary` in
      `backend/src/Rundfrage.Api/Endpoints/Admin/PollAdminEndpoints.cs` and to the poll listing query
      (FR-038, FR-039).
- [X] T053 [P] [US3] Add the same two fields to `WishListSummary` in
      `backend/src/Rundfrage.Api/Wishes/WishListProjection.cs`, and add the owner-scoped overload
      there (research R-8) so the area, the dashboard and the creator surface read **one** projection
      — which is what makes FR-047 and FR-049 structural rather than test-maintained.
- [X] T054 [US3] Set the owner to the operator on import in
      `backend/src/Rundfrage.Api/Polls/PollImport.cs`, so an imported poll is unambiguously the
      operator's (FR-053). Import remains an operator capability that no Ersteller can reach
      (FR-028a), which is why there is no owner parameter to pass.
- [X] T055 [US3] Render the owner in `frontend/src/components/admin/PollList.vue`,
      `PollAnswersView.vue`, `WishListsView.vue`, `WishListDetailView.vue` and `DashboardView.vue`
      per ui-contract §5, and cover it in the existing component specs for those files. `null`
      renders as the operator's own via `creator.ownerSelf`, **never as an empty cell** — an empty
      cell reads as missing data rather than as "mine".
- [X] T056 [US3] Confirm across `frontend/src/components/admin/PollList.vue` and
      `WishListsView.vue` that no admin control is hidden, disabled or relabelled by ownership, and
      that the new column does not push rows into horizontal scrolling at 375 px (FR-040,
      ui-contract §6).

**Checkpoint**: the operator's view is complete and attributed.

---

## Phase 6: User Story 4 — Manage the set of links (Priority: P4)

**Goal**: list, rename, reissue, revoke, delete, and the enforced cap of 100 — with revocation and
deletion unmistakably different actions.

**Independent Test**: create Ersteller up to the cap and confirm the next is refused naming the
limit; reissue one link and confirm the old URL stops working while its content survives; revoke one
and confirm its URL stops working while its polls and wish lists remain listed and its participant
links keep answering; then delete it and confirm the confirmation named both counts and the content
is gone.

> The cap itself was proved in Phase 2 (T008), against the service. This phase proves it through the
> endpoint and covers everything else the operator can do to an Ersteller.

### Tests for User Story 4 ⚠️ write first, watch fail

- [X] T057 [US4] Write `backend/tests/Rundfrage.Api.IntegrationTests/CreatorRevocationTests.cs` —
      the suite that carries the spec's most consequential clarification. **Revoke destroys
      nothing**: across any number of revocations the count of polls, wish lists, responses and
      claims is unchanged and every participant link keeps working (FR-018, FR-020, SC-006a). A
      revoked Ersteller stays listed with its counts and no working link (FR-044a). **Reissue**: a
      new link reaches exactly what it owned before, the previous URL becomes indistinguishable from
      one that never existed within one request, and zero items are lost or gained (FR-016, SC-007).
      **Rename**: the new name is shown wherever that Ersteller appears, the link is unchanged and
      every poll and wish list it owns is untouched (FR-017). **Delete destroys exactly the right
      things**: the Ersteller, its polls and wish lists and everything beneath them, and nothing
      belonging to any other owner — and the content is *destroyed*, not reassigned to the operator.
      No poll or wish list is destroyed without a confirmation that stated its count beforehand
      (FR-019, FR-020a, FR-020c, SC-006b, research R-4).
- [X] T058 [P] [US4] Extend
      `backend/tests/Rundfrage.Api.IntegrationTests/AdminAuthorizationTests.cs` with all five
      `/admin/creators` routes: each is refused without an operator session, and the refusal reveals
      nothing about whether the named Ersteller exists (FR-010). The existing suite already
      enumerates the admin surface; this keeps the new routes from being the ones nobody checked.
- [X] T059 [P] [US4] Write `frontend/tests/unit/CreatorsView.spec.ts` per
      ui-contract §3 and §3a: every row field renders including the no-link state (FR-044, FR-044a);
      the area opens on the list and creating reveals a form (FR-045); the empty and unavailable
      states are distinguishable and neither is a zero (FR-046); the revoke and delete confirmations
      differ in substance (FR-019, FR-020b); they are not adjacent in the DOM; only the deletion
      announces destruction; and the cap refusal states that a place is freed by deleting rather than
      revoking (FR-009a). Extend `frontend/tests/unit/AdminNav.spec.ts` and
      `frontend/tests/unit/adminRoutes.spec.ts` in the same task: the "Ersteller" entry sits fourth,
      **Einstellungen remains last with nothing after it**, exactly one entry carries
      `aria-current="page"`, and `/admin/ersteller` resolves (FR-043, FR-047).
- [X] T060 [P] [US4] Extend `backend/tests/Rundfrage.Api.IntegrationTests/RestoreTests.cs` and
      `BackupTests.cs`: the restore preview counts the Ersteller it will destroy (FR-052); a backup
      taken **before** this feature — with no `Creators` table — still previews and restores,
      counting zero rather than refusing (data-model §9); and a **round trip works** — back up,
      delete an Ersteller, restore, and the original creator URL authorises again against the content
      it owned (FR-051). The preview's counts and the round trip are different claims and the suite
      makes both.

### Implementation for User Story 4

- [X] T061 [US4] Complete `backend/src/Rundfrage.Api/Endpoints/Admin/CreatorAdminEndpoints.cs`:
      `GET /admin/creators`, `PATCH /admin/creators/{id}` (rename), `POST /admin/creators/{id}/link`
      (reissue), `DELETE /admin/creators/{id}/link` (revoke), `DELETE /admin/creators/{id}` (delete).
      Revoke is addressed as the **link sub-resource** deliberately, so it cannot read as a variant of
      deleting the Ersteller (FR-020b, contracts/openapi.yaml).
- [X] T062 [P] [US4] Create `frontend/src/stores/creators.ts` — the operator's view of the Ersteller
      list — and add its calls to `frontend/src/api/client.ts`.
- [X] T063 [US4] Create `frontend/src/components/admin/CreatorsView.vue` per ui-contract §3: opens on
      the list; creating reveals a form rather than sitting permanently open; leaving discards an
      unconfirmed entry; the empty state offers creating directly; and "none exist" is visibly
      different from "the data cannot be read", neither rendered as a zero (FR-045, FR-046).
- [X] T064 [US4] Implement the two confirmations in
      `frontend/src/components/admin/CreatorsView.vue` per ui-contract §3a, reusing
      `DeleteConfirm.vue` rather than writing a second confirmation component. Revoke and delete must
      not be adjacent, and only delete carries destructive weight (FR-019, FR-020b).
- [X] T065 [US4] Add the `nav.creators` entry to `frontend/src/components/admin/AdminNav.vue` and the
      `/admin/ersteller` route to `frontend/src/router.ts`. Fourth position, **before** Einstellungen;
      settings stays last and nothing may appear after it (FR-043, FR-047). The entry owns a single
      route name — there is no detail destination, because an Ersteller is a row, not a page.
- [X] T066 [US4] Add `CreatorsInBackup` and `CreatorsLost` to `RestorePreview` and read them in
      `backend/src/Rundfrage.Api/Data/RestoreService.ReadCountsAsync`, through the same tolerant
      table-presence check `ReadWishCountsAsync` uses, and render them in
      `frontend/src/components/admin/RestorePanel.vue` (FR-052).

**Checkpoint**: every user story is complete and independently demonstrable.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T067 [P] Write `backend/tests/Rundfrage.Api.IntegrationTests/CreatorScaleTests.cs`: at 100
      Ersteller sharing the installation-wide scales of 007 FR-028c and 008 FR-054, an Ersteller's own
      lists are produced within two seconds and the operator's poll and wish-list areas stay inside
      the budgets those features already set (SC-010, FR-036b). Reuse `DashboardScaleFixture`'s
      seeding approach rather than a second fixture.
- [X] T068 [P] Write `e2e/tests/creator-journey.spec.ts` covering the quickstart hand-walk: issue,
      use, revoke, reissue, delete — including that the private-window session never signs in, and
      that exactly one address on the surface carries the token with zero history entries added while
      opening and closing both disclosures (SC-011a, SC-001).
- [X] T069 [P] Accessibility and layout pass on
      `frontend/src/components/creator/CreatorSurface.vue` and
      `frontend/src/components/admin/CreatorsView.vue`: keyboard-only completion, text labels,
      visible focus, and the link state conveyed **in text** rather than by colour (FR-055, FR-056,
      FR-057, SC-011). Verify the creator surface at 375 px against `specs/design-system.md`.
- [X] T070 [P] Verify SC-012 in `frontend/tests/unit/CreatorsView.spec.ts`: "no
      Ersteller exists" and "the data cannot be read" produce visibly different results and neither is
      ever shown as a zero.
- [X] T071 Update `specs/009-creator-links/quickstart.md` if any path in its file map moved during
      implementation, so the developer guide matches the repository rather than the plan.
- [X] T072 Run the full suite — `dotnet test backend/Rundfrage.slnx`, `npm run test:unit`,
      `npx playwright test` — and the linter and formatter. A failing or skipped test blocks the
      merge; disabling one to unblock is prohibited (constitution, Development Workflow gate 2).

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)** — no dependencies, start immediately.
- **Phase 2 (Foundational)** — depends on Phase 1. **Blocks every user story.**
- **Phase 3 (US1)** — depends on Phase 2. The MVP.
- **Phase 4 (US2)** — depends on Phase 3: the route-level isolation suite needs routes to aim at.
- **Phase 5 (US3)** — depends on Phase 2 only. Can run in parallel with Phases 3–4.
- **Phase 6 (US4)** — depends on Phase 2; T061 assumes T026's endpoint file exists.
- **Phase 7 (Polish)** — depends on the stories it covers.

### Within each story

Tests are written and observed failing before the implementation they define. Entities before the
schema; the schema before the migration; the migration before anything reads through it; services
before endpoints; endpoints before the components that call them.

### The orderings that are not negotiable

- **T009 before T015.** The isolation filter's test is reachable without any endpoint, so there is no
  reason to defer it — and isolation written first and tested later is isolation retrofitted, whose
  failure mode is a leak that looks like a working feature.
- **T008 before T017.** Same argument for the cap of 100, including its concurrency case.
- **T041 before T045.** The route-level suite must exist before the handlers are rewired through
  `OwnerScope`, or the rewiring has nothing proving it landed.

### Parallel opportunities

- T001–T004 all parallel.
- T005–T009 all parallel (five different test files); T011 and T012 parallel; T018 parallel with T017.
- T020–T025 all parallel (six different files).
- T029 and T030 parallel; T036 and T037 parallel.
- T042–T044 parallel; T048–T051 parallel; T052 and T053 parallel.
- T058–T060 parallel; T067–T070 parallel.
- **Phase 5 (US3) can be staffed alongside Phases 3–4** — it touches the admin surfaces, which the
  creator work does not.

---

## Parallel Example: Phase 2 tests

```bash
Task: "MigrationOnPopulatedDatabaseTests — the rows survive and are the operator's"  # T005
Task: "CreatorValidationTests — name rules and refusal payloads"                     # T006
Task: "SchemaCreationTests — many NULLs in a unique index"                            # T007
Task: "CreatorLimitTests — 100/101, revoke vs delete, and the race"                   # T008
Task: "OwnerScopeTests — the filter returns one owner's content and no other's"       # T009
```

---

## Implementation Strategy

### MVP first (User Story 1 only)

1. Phase 1 (Setup) → Phase 2 (Foundational) → Phase 3 (US1).
2. **Stop and validate**: walk the quickstart's steps 1–5 by hand.
3. At that point the feature does its job: somebody can be handed a link and make things with it.

### Incremental delivery

1. Setup + Foundational → ownership exists and is proven, unreachable.
2. + US1 → a link can be issued and used. **MVP.**
3. + US2 → the isolation promise is proven through HTTP on every route.
4. + US3 → the operator's view is complete and attributed.
5. + US4 → links can be listed, renamed, replaced, revoked and deleted.

**US2 is not optional polish.** It is second in priority because it is a property of what US1 builds
rather than a separate surface — but shipping US1 without it means shipping links whose isolation has
been proved only at the filter and not at the routes. Do not stop between Phase 3 and Phase 4.

### What to watch

- **T013's explicit cascade.** Left at EF Core's default, deleting an Ersteller hands its content to
  the operator. The test that catches it is T057, and nothing else will.
- **T027's mount point.** Under `/api/v1/admin`, the creator group inherits that group's exemption
  from maintenance mode. T022 is the guard.
- **T036's single route.** The asymmetry with feature 007 is the decision, not an oversight.
- **T016's refactor.** Behaviour must not change; run `WishClaimTests` and `WishConcurrencyTests`
  before and after and compare.
- **T047.** If the existing participant end-to-end suite needs any change to pass, that is a defect
  in this feature.
