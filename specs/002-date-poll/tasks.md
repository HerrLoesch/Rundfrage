---
description: "Task list for Date Poll (Terminfindung)"
---

# Tasks: Date Poll (Terminfindung)

**Input**: Design documents from `/specs/002-date-poll/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: Test tasks are MANDATORY (Constitution Principle II, FR-046). Every behaviour task is
preceded by the test task that defines it, and that test MUST be observed failing first.

**Organization**: Grouped by user story so each can be implemented, tested and demonstrated
independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete work)
- **[Story]**: US1–US5, mapping to the five user stories in spec.md
- Paths are relative to the repository root `/Users/hendrik/repos/Rundfrage`

## Environment note

```bash
export SPECIFY_FEATURE=002-date-poll
```

Required for every Spec Kit command: branch validation expects `NNN-slug`, development happens on
`dev` (constitution v1.1.1).

---

## Phase 1: Setup

**Purpose**: Infrastructure this feature needs before any behaviour can be written.

- [ ] T001 Add `RUN apk add --no-cache tzdata` to the runtime stage of `docker/Dockerfile` — **verified defect**, without it `TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin")` throws in the runtime image (research.md R-6)
- [ ] T002 Add a test in `backend/tests/Rundfrage.Api.IntegrationTests/TimeZoneAvailabilityTests.cs` asserting Europe/Berlin resolves, so a future base-image change cannot silently reintroduce T001's defect
- [ ] T003 Add `ADMIN_USER` and `ADMIN_PASSWORD_HASH` to `compose.yaml` and `.env.example` **without defaults** — unlike feature 001 this must not start on built-in values (quickstart.md)
- [ ] T004 [P] Install `vue-router` in `frontend/` and create `frontend/src/router.ts` with the four routes: sign-in, admin poll list, poll page, response revision
- [ ] T005 [P] Create the component directories `frontend/src/components/admin/` and `frontend/src/components/poll/`

**Checkpoint**: The container can resolve Europe/Berlin and the SPA can route.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The three shared authorities and the schema. Every user story depends on these.

**⚠️ CRITICAL**: No user story work may begin until this phase is complete.

### The day-boundary authority

- [ ] T006 [P] Write failing tests in `backend/tests/Rundfrage.Api.UnitTests/BerlinClockTests.cs`: today's date at 00:30 Berlin in summer and in winter, and a retention deadline computed from a last candidate day (FR-011a, FR-011b, SC-027, SC-028)
- [ ] T007 Implement `backend/src/Rundfrage.Api/Time/BerlinClock.cs` as the single day-boundary authority — makes T006 pass

### The token mint

- [ ] T008 [P] Write failing tests in `backend/tests/Rundfrage.Api.UnitTests/CapabilityTokenTests.cs`: 22 base64url characters, at least 128 bits of entropy, no two equal across 10,000 mints, and no correlation with a counter (FR-017, SC-006)
- [ ] T009 Implement `backend/src/Rundfrage.Api/Security/CapabilityToken.cs` using `RandomNumberGenerator` — makes T008 pass

### The one neutral not-found

- [ ] T010 [P] Write failing tests in `backend/tests/Rundfrage.Api.UnitTests/NeutralNotFoundTests.cs` asserting one identical status, body and header set, with no variant per cause (FR-027, FR-040, SC-012)
- [ ] T011 Implement `backend/src/Rundfrage.Api/Http/NeutralNotFound.cs` — makes T010 pass

### Schema

- [ ] T012 [P] Create the entities in `backend/src/Rundfrage.Api/Data/Entities/`: `Poll.cs`, `CandidateDay.cs`, `PollResponse.cs`, `DayAnswer.cs` per data-model.md — `Availability` has **three** values, not four (research.md R-8)
- [ ] T013 Register the four sets and their configuration in `backend/src/Rundfrage.Api/Data/RundfrageDbContext.cs`: unique indexes on both token columns and on (PollId, Date), cascade deletes, and the FR-015 column lengths
- [ ] T014 Write a failing test in `backend/tests/Rundfrage.Api.IntegrationTests/SchemaCreationTests.cs` asserting the four tables, both unique token indexes and the cascade behaviour exist after migration — extends the test feature 001 wrote against an empty schema
- [ ] T015 Generate the second EF Core migration into `backend/src/Rundfrage.Api/Data/Migrations/` — makes T014 pass
- [ ] T016 Write a failing test asserting no table has a column for an IP address or user agent (FR-042, SC-021), in `backend/tests/Rundfrage.Api.IntegrationTests/SchemaCreationTests.cs`

**Checkpoint**: Shared authorities and schema exist. User stories can start.

---

## Phase 3: User Story 1 — A creator signs in and creates a date poll (Priority: P1) 🎯 MVP

**Goal**: The operator authenticates and creates a poll, receiving a participant link.

**Independent Test**: Sign in, create a poll with a title, a message and three days, and confirm a
participant link and a retention deadline are shown. Requires no other story.

### Tests (write first, observe failing)

- [ ] T017 [P] [US1] Write failing tests in `backend/tests/Rundfrage.Api.UnitTests/PasswordHashTests.cs`: a generated hash verifies, a wrong password does not, the hash string is self-describing, and the plaintext is unrecoverable from it (FR-003, FR-045a)
- [ ] T018 [P] [US1] Write failing tests in `backend/tests/Rundfrage.Api.UnitTests/SignInThrottleTests.cs`: 5 failures lock for 15 minutes, a **correct** password is also refused during the lockout, the lockout expires unaided, and success resets the count (FR-005, FR-005a, SC-019)
- [ ] T019 [P] [US1] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/SignInEndpointTests.cs`: 204 with a session cookie on success, 401 identical for wrong user and wrong password, 429 while locked, and `HttpOnly`/`Secure`/`SameSite=Strict` on the cookie (FR-004, FR-006, research.md R-1)
- [ ] T020 [P] [US1] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/AdminAuthorizationTests.cs` asserting **every** route under `/api/v1/admin` returns 401 without a session and discloses nothing about what exists (FR-002, FR-048, SC-004)
- [ ] T021 [P] [US1] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/PollCreationTests.cs`: title required, at least one day required, a duplicate day stored once, past days accepted, days returned chronologically, a participant token minted, and the retention deadline computed (FR-008, FR-010, FR-012, FR-013, FR-014, FR-016)
- [ ] T022 [P] [US1] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/PollCreationTests.cs` asserting each of the five FR-015 limits is refused server-side with the limit named, bypassing the form (SC-017)
- [ ] T023 [P] [US1] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/StartupConfigurationTests.cs` asserting the application refuses to start without `ADMIN_USER` or `ADMIN_PASSWORD_HASH`, and that no plaintext password is usable as the hash (SC-015)
- [ ] T024 [P] [US1] Write failing tests in `backend/tests/Rundfrage.Api.UnitTests/AdminLoggingTests.cs` asserting one entry each for sign-in success, sign-in failure, lockout and poll creation, carrying no password, name, answer or token (FR-043a, FR-043b, SC-023, SC-024)
- [ ] T025 [P] [US1] Write failing Vitest tests in `frontend/tests/unit/SignInForm.spec.ts` and `frontend/tests/unit/PollForm.spec.ts`, asserting via `data-testid` and translation keys only (FR-030)
- [ ] T026 [P] [US1] Write a failing Playwright test in `e2e/tests/admin-access.spec.ts` asserting the admin area is unreachable without signing in (FR-048)

### Implementation

- [ ] T027 [US1] Implement `backend/src/Rundfrage.Api/Security/PasswordHash.cs` with PBKDF2-HMAC-SHA256 at 600,000 iterations and a self-describing encoding (research.md R-2) — makes T017 pass
- [ ] T028 [US1] Add the `--hash-password` switch to `backend/src/Rundfrage.Api/Program.cs` so the operator can produce a hash without a separate tool (quickstart.md)
- [ ] T029 [US1] Implement `backend/src/Rundfrage.Api/Security/SignInThrottle.cs` as in-memory state for the single account (research.md R-12) — makes T018 pass
- [ ] T030 [US1] Configure cookie authentication in `backend/src/Rundfrage.Api/Program.cs`: `HttpOnly`, `Secure`, `SameSite=Strict`, 8-hour sliding expiry — makes T019 pass
- [ ] T031 [US1] Add startup configuration validation to `backend/src/Rundfrage.Api/Program.cs` that refuses to start without both admin variables — makes T023 pass
- [ ] T032 [US1] Implement `backend/src/Rundfrage.Api/Endpoints/Admin/SignInEndpoints.cs` for `POST` and `DELETE /api/v1/admin/session` (FR-007)
- [ ] T033 [US1] Apply an authorization requirement to the whole `/api/v1/admin` route group in `backend/src/Rundfrage.Api/Program.cs`, so FR-048 holds by prefix rather than per handler — makes T020 pass
- [ ] T034 [US1] Implement `backend/src/Rundfrage.Api/Polls/PollService.cs`: creation, FR-015 limit enforcement, day de-duplication, and the retention deadline via `BerlinClock` — makes T021 and T022 pass
- [ ] T035 [US1] Implement `backend/src/Rundfrage.Api/Endpoints/Admin/PollAdminEndpoints.cs` for `POST` and `GET /api/v1/admin/polls`, matching `contracts/openapi.yaml` (FR-018)
- [ ] T036 [US1] Add the four FR-043a log entries for sign-in and poll creation — makes T024 pass
- [ ] T037 [P] [US1] Add the German strings for sign-in and poll creation to `frontend/src/locales/de.json`, including every `Problem.code` from the contract (FR-028, FR-029)
- [ ] T038 [P] [US1] Implement `frontend/src/stores/session.ts` and `frontend/src/stores/polls.ts`
- [ ] T039 [US1] Implement `frontend/src/components/admin/SignInForm.vue`, `PollForm.vue` and `PollList.vue`, showing each poll's link and retention deadline (FR-039a) — makes T025 pass
- [ ] T040 [US1] Wire the admin routes into `frontend/src/router.ts` with a guard that redirects to sign-in — makes T026 pass

**Checkpoint**: MVP — the operator can sign in and produce a shareable poll.

---

## Phase 4: User Story 2 — Anyone answers via the link, without an account (Priority: P2)

**Goal**: A participant opens the link and submits a complete response with no account.

**Independent Test**: Open a poll link in a browser with no session and no stored credentials,
submit a response, and confirm it was recorded.

### Tests (write first, observe failing)

- [ ] T041 [P] [US2] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/PublicPollTests.cs` asserting `GET /api/v1/polls/{token}` succeeds with **no** session, cookie or header of any kind (FR-019, FR-020, Principle I)
- [ ] T042 [P] [US2] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/NeutralNotFoundTests.cs` asserting unknown, malformed, expired and deleted tokens produce **byte-identical** responses, and that a malformed token is not short-circuited (SC-012, research.md R-4)
- [ ] T043 [P] [US2] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/ResponseSubmissionTests.cs`: a complete submission is stored, a display name is required, an unknown day is refused, and an omitted day stores **nothing** (FR-022, FR-023, FR-024, research.md R-8)
- [ ] T044 [P] [US2] Write a failing test asserting the submission response carries an edit token that resolves to that response and to no other (FR-026, FR-029)
- [ ] T045 [P] [US2] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/RateLimitTests.cs`: the 11th submission within an hour returns 429 with a retry hint, and nothing about the source is stored (FR-027a, FR-027c, SC-020, SC-021)
- [ ] T046 [P] [US2] Write a failing test in `backend/tests/Rundfrage.Api.IntegrationTests/ResponseCapTests.cs` asserting the 1001st submission is refused with 409, including under concurrent submission (FR-015a, research.md R-9)
- [ ] T047 [P] [US2] Write failing Vitest tests in `frontend/tests/unit/AnswerForm.spec.ts`: three native radios per day, an unselected day submits nothing, and keyboard-only operation works (FR-023, FR-050, FR-052, SC-025, research.md R-11)
- [ ] T048 [P] [US2] Write a failing Vitest test in `frontend/tests/unit/PollView.spec.ts` asserting the visibility notice appears **before** the name field, and that the grid renders without submitting (FR-036a, FR-036b, SC-013)
- [ ] T049 [P] [US2] Write the failing Playwright test `e2e/tests/zero-signup.spec.ts`: a complete response from a bare link with no account, no session and no stored credentials (FR-047, SC-002, SC-003)

### Implementation

- [ ] T050 [US2] Implement `backend/src/Rundfrage.Api/Endpoints/Public/PollEndpoints.cs` for `GET /api/v1/polls/{pollToken}` — makes T041 and T042 pass
- [ ] T051 [US2] Implement `backend/src/Rundfrage.Api/Polls/ResponseService.cs`: submission, the row-locked 1000-cap, and storing only answered days — makes T043, T044 and T046 pass
- [ ] T052 [US2] Implement `backend/src/Rundfrage.Api/Endpoints/Public/ResponseEndpoints.cs` for `POST /api/v1/polls/{pollToken}/responses`
- [ ] T053 [US2] Configure the built-in rate limiter in `backend/src/Rundfrage.Api/Program.cs`, partitioned in memory by request source, 10 per hour, on the submit endpoint only (research.md R-5) — makes T045 pass
- [ ] T054 [US2] Add the FR-043a log entry for a rate-limited submission, carrying no request source (FR-043b)
- [ ] T055 [P] [US2] Add the participant-facing German strings to `frontend/src/locales/de.json`, including the visibility notice
- [ ] T056 [P] [US2] Implement `frontend/src/stores/answering.ts`
- [ ] T057 [US2] Implement `frontend/src/components/poll/AnswerForm.vue` using a `fieldset` of three native radios per day — makes T047 pass
- [ ] T058 [US2] Implement `frontend/src/components/poll/PollView.vue` and `ShareLink.vue` — makes T048 and T049 pass

**Checkpoint**: Principle I is satisfied end to end — the product's reason to exist works.

---

## Phase 5: User Story 3 — The creator sees who can attend when (Priority: P3)

**Goal**: The grid shows every response and the per-day totals.

**Independent Test**: With two responses recorded, open the poll and confirm the grid shows both
and totals each day correctly.

### Tests (write first, observe failing)

- [ ] T059 [P] [US3] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/ResultsTests.cs` asserting the per-day totals count only the three answered states and need not sum to the response count (FR-033)
- [ ] T060 [P] [US3] Write a failing test asserting `responseCount` reports every response across all pages, so the uncounted *no answer* state stays legible (FR-033a)
- [ ] T061 [P] [US3] Write a failing test asserting response rows are paged at 50 and that no row carries anyone's edit token (FR-029, research.md R-7)
- [ ] T062 [P] [US3] Write a failing test asserting a poll with no responses returns an explicit empty state rather than an error (FR-034)
- [ ] T063 [P] [US3] Write a failing performance test asserting a poll at 1000 responses across 100 days returns its first page and totals within 5 seconds (FR-036c, SC-016)
- [ ] T064 [P] [US3] Write failing Vitest tests in `frontend/tests/unit/ResultGrid.spec.ts`: the three answered states and the empty cell are distinguishable, and remain so in greyscale (FR-024a, FR-053, SC-026, SC-029, SC-030)
- [ ] T065 [P] [US3] Write a failing Playwright assertion in `e2e/tests/date-poll-journey.spec.ts` that a newly submitted response appears on reload (FR-035, SC-009)

### Implementation

- [ ] T066 [US3] Implement `backend/src/Rundfrage.Api/Polls/ResultsProjection.cs` computing totals with a grouped query and paging rows — makes T059 to T063 pass
- [ ] T067 [US3] Add `GET /api/v1/admin/polls/{pollId}` to `backend/src/Rundfrage.Api/Endpoints/Admin/PollAdminEndpoints.cs`
- [ ] T068 [US3] Include the grid in the public poll payload from `PollEndpoints.cs` (FR-036)
- [ ] T069 [US3] Implement `frontend/src/components/poll/ResultGrid.vue` with a character or word per state, never colour alone — makes T064 pass

**Checkpoint**: The collected answers are readable and the best day is visible at a glance.

---

## Phase 6: User Story 4 — A participant corrects their answer (Priority: P4)

**Goal**: A participant revises their own response using the personal link, with no account.

**Independent Test**: Submit a response, follow the personal link, change one day, and confirm the
change is stored without a second response appearing.

### Tests (write first, observe failing)

- [ ] T070 [P] [US4] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/ResponseRevisionTests.cs`: the edit token returns that response prefilled, and grants access to no other response and to no admin function (FR-028, FR-029)
- [ ] T071 [P] [US4] Write a failing test asserting a revision updates in place and leaves the response count unchanged (FR-030, SC-008)
- [ ] T072 [P] [US4] Write a failing test asserting no participant route can change a response without its edit token (FR-031)
- [ ] T073 [P] [US4] Write a failing test asserting an edit token for a deleted or expired poll produces the same neutral not-found (FR-040)
- [ ] T074 [P] [US4] Write a failing Vitest test in `frontend/tests/unit/AnswerForm.revision.spec.ts` asserting previous answers are prefilled and editable
- [ ] T075 [P] [US4] Write a failing Playwright assertion in `e2e/tests/date-poll-journey.spec.ts` covering submit → revise → verify

### Implementation

- [ ] T076 [US4] Add `GET` and `PUT /api/v1/responses/{editToken}` to `backend/src/Rundfrage.Api/Endpoints/Public/ResponseEndpoints.cs` — makes T070 to T073 pass
- [ ] T077 [US4] Extend `ResponseService.cs` with in-place revision that clears answers for omitted days
- [ ] T078 [US4] Add the revision route to `frontend/src/router.ts` and reuse `AnswerForm.vue` in revision mode — makes T074 and T075 pass

**Checkpoint**: A wrong answer can be corrected without anyone being identified.

---

## Phase 7: User Story 5 — Polls do not accumulate forever (Priority: P5)

**Goal**: The creator can delete, and expired polls disappear on their own.

**Independent Test**: Create a poll, add a response, delete the poll, and confirm the response is
gone and both link kinds stop working.

### Tests (write first, observe failing)

- [ ] T079 [P] [US5] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/PollDeletionTests.cs` asserting deleting a poll removes every response, verified by direct storage inspection rather than by attempting access (FR-037, FR-049, SC-007)
- [ ] T080 [P] [US5] Write a failing test asserting a single response can be deleted without touching the poll or other responses, and that the per-day totals update (FR-037a, FR-037b, SC-022)
- [ ] T081 [P] [US5] Write a failing test asserting a poll one second past its deadline is already unreachable through all three route kinds, before any sweep runs (FR-039b, SC-031)
- [ ] T082 [P] [US5] Write failing tests in `backend/tests/Rundfrage.Api.IntegrationTests/RetentionSweepTests.cs`: the sweep erases expired polls, is safe to run repeatedly, and logs how many it removed (FR-039c, FR-039d, SC-014, SC-032)
- [ ] T083 [P] [US5] Write failing tests asserting one log entry each for poll deletion, response deletion and each retention sweep, none carrying a name, answer or token (FR-043a, FR-043b, SC-023, SC-024)
- [ ] T084 [P] [US5] Write a failing Vitest test in `frontend/tests/unit/DeleteConfirm.spec.ts` asserting the confirmation names how many responses will be destroyed (FR-038)
- [ ] T085 [P] [US5] Write a failing Playwright assertion in `e2e/tests/date-poll-journey.spec.ts` that after deletion both the participant link and the personal link show the neutral not-found (FR-040)

### Implementation

- [ ] T086 [US5] Implement the access filter in `backend/src/Rundfrage.Api/Retention/RetentionService.cs` that every poll, results and response lookup passes through — makes T081 pass
- [ ] T087 [US5] Implement the hosted sweep in `RetentionService.cs` with a periodic timer running at least daily, idempotent, logging its count — makes T082 pass
- [ ] T088 [US5] Add `DELETE /api/v1/admin/polls/{pollId}` and `DELETE .../responses/{responseId}` to `PollAdminEndpoints.cs` — makes T079 and T080 pass
- [ ] T089 [US5] Add the three FR-043a log entries for deletion and retention — makes T083 pass
- [ ] T090 [US5] Implement `frontend/src/components/admin/DeleteConfirm.vue` — makes T084 and T085 pass

**Checkpoint**: Principle IV's retention requirement is satisfied by an actual erasure.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T091 [P] Update `README.md` with the operator setup, the admin address, the participant flow and the three answer states, derived from `quickstart.md` (FR-023 of 001)
- [ ] T092 [P] Add the `Problem.code` translations for every code in `contracts/openapi.yaml` to `frontend/src/locales/de.json`, so no error reaches a user untranslated
- [ ] T093 Verify SC-001 and SC-004 by hand: a participant answers a five-day poll in under 60 seconds; the operator creates one in under two minutes
- [ ] T094 Verify SC-005 by hand: a revised answer is visible on reload without any restart
- [ ] T095 Verify SC-011 by scanning a full run's logs for names, answers, tokens and request sources; the expected count is zero
- [ ] T096 Verify SC-012 by comparing the four not-found responses byte for byte
- [ ] T097 Verify SC-025 by completing a whole response using only the keyboard, with no pointing device
- [ ] T098 Verify SC-018 by confirming an 8-hour idle session no longer grants admin access
- [ ] T099 Confirm the Complexity Tracking table in `plan.md` still matches what was built, in particular that no repository, service-layer indirection or mediator appeared
- [ ] T100 Re-run the full suite — xUnit, Vitest and Playwright — and confirm the build stays warning-free

---

## Dependencies

**Phase order**: Setup (T001–T005) → Foundational (T006–T016) → US1 (T017–T040) → US2 (T041–T058)
→ US3 (T059–T069) → US4 (T070–T078) → US5 (T079–T090) → Polish (T091–T100)

**Story dependencies**:

- **US1** depends only on Setup and Foundational. It is the MVP and ships alone.
- **US2** depends on US1 for a poll to answer. Its backend work depends only on Foundational.
- **US3** depends on US2 for responses to display, though the empty-state case (T062) is testable
  as soon as US1 exists.
- **US4** depends on US2 — there must be a response before one can be revised.
- **US5** depends on US1 for a poll to delete. The retention sweep (T082, T087) depends on nothing
  but Foundational and could be built at any point after it.

**T001 blocks almost everything.** Without tzdata in the runtime image, `BerlinClock` throws, and
`BerlinClock` computes the retention deadline that every poll creation writes. This ordering is
not cosmetic — it is why the verified defect is task one.

**Test-first ordering (Principle II)**: within every phase, all listed test tasks precede their
implementation tasks. T006 before T007; T017–T026 before T027–T040; T041–T049 before T050–T058;
T059–T065 before T066–T069; T070–T075 before T076–T078; T079–T085 before T086–T090.

---

## Parallel Execution Opportunities

- **Foundational**: T006, T008, T010 and T012 are `[P]` — three independent authorities and the
  entity files, no shared file among them.
- **US1 tests**: T017–T026 are all `[P]`, ten independent test files.
- **US2 tests**: T041–T049 are all `[P]`, nine independent test files.
- **US3 tests**: T059–T065 are all `[P]`.
- **US5 tests**: T079–T085 are all `[P]`.
- **Locales and stores**: T037/T038 and T055/T056 are `[P]` against their story's backend work
  once the contract is fixed.
- **Polish**: T091 and T092 are `[P]`.

---

## Implementation Strategy

**MVP = Phase 1 + Phase 2 + Phase 3 (US1)** — 40 tasks. The operator can sign in and produce a
shareable link. Nothing can be answered yet, but the protected creation path is real.

**Increment 2 = Phase 4 (US2)** is the one that matters most: it is where Principle I stops being
a promise the constitution makes and becomes behaviour a test proves.

**Increment 3 = Phase 5 (US3)** makes the collected answers readable, which is what the poll was
for.

**Increments 4 and 5 (US4, US5)** add correction and retention. US5 is the smallest and could be
pulled forward if the 30-day deadline needs to be demonstrable earlier.

Each increment ends at a checkpoint where the system can be shown to someone.
