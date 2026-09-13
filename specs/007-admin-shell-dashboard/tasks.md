---
description: "Task list for feature 007 — admin shell, dashboard and settings"
---

# Tasks: Admin Shell, Dashboard and Settings

**Input**: Design documents from `/specs/007-admin-shell-dashboard/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Test tasks are MANDATORY (Constitution Principle II: Test-First Development). Every
behaviour task is preceded by the test that defines it, and that test MUST be observed failing for
the intended reason before implementation begins.

**Organization**: Grouped by user story so each can be implemented, tested and demonstrated on its
own.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an incomplete task)
- **[Story]**: US1, US2, US3 — maps to the user stories in spec.md
- Exact file paths are given in every task

## Path Conventions

Web app per plan.md: `backend/src/Rundfrage.Api/`, `frontend/src/`, `e2e/tests/`.

## The one thing to read before starting

`research.md` R-4. SC-011 promises six dashboard figures in two seconds at FR-028c's scale, whose
worst case is 50 million `DayAnswer` rows. That is very likely unachievable, and **Phase 1 measures
it before anything is built on top of it**. Do not resolve a slow result with a cached total —
Principle IV forbids the extra record, and five deletion paths would each have to decrement it.

---

## Phase 1: Risk Resolution (Blocking Gate for US3)

**Purpose**: Answer the open performance question before designing around a guess. Frontend work
(Phases 2–5) does not depend on this; **US3 does**.

- [X] T001 Add a seeded scale fixture producing FR-028c's worst case — 500 polls × 1000 responses × 100 candidate days — in `backend/tests/Rundfrage.Api.IntegrationTests/DashboardScaleFixture.cs`, following the pattern of the existing `ExportScaleTests.cs` and `PollImportScaleTests.cs` fixtures
- [X] T002 Add a timed assertion for the figure-6 aggregate (`GROUP BY Availability` over `DayAnswer` joined to live polls) in `backend/tests/Rundfrage.Api.IntegrationTests/DashboardScaleTests.cs`, marked `Category=DashboardScale`, asserting SC-011's two-second budget
- [X] T003 Run T002 and record the measured figure in `specs/007-admin-shell-dashboard/research.md` under R-4, replacing the "under measurement" wording with the number

**GATE — T004 branches on T002's result. Choose exactly one:**

- [ ] ~~T004a~~ (not taken — measurement exceeded two seconds at the original scale) If under two seconds: mark R-4 resolved in `specs/007-admin-shell-dashboard/research.md`, keep T002 as a permanent regression test, and update the PERFORMANCE note in `specs/007-admin-shell-dashboard/contracts/openapi.yaml` with the measured figure
- [X] T004b If over two seconds but acceptable at realistic fill: amend **SC-011** and **FR-028c** in `specs/007-admin-shell-dashboard/spec.md` to a scale the product actually reaches, add a Clarifications bullet dated to the amendment recording the measured numbers and that the criterion was wrong rather than the design, and retune T002's fixture and budget to the amended scale
- [ ] ~~T004c~~ (not taken — realistic fill measures 0.80 s) If over two seconds even at realistic fill: **stop and ask the user.** Every way to make figure 6 fast is something the spec forbids — a cached total (Principle IV, and stale on five deletion paths) or a capped "over N" reading (rejected as Option D during clarification). Dropping figure 6 and keeping the other five is the fallback and is the user's decision, not ours

**Checkpoint**: The performance question has a number attached to it and the spec matches reality.

---

## Phase 2: Foundational (Blocking Prerequisites for All Stories)

**Purpose**: The shell and its routes. No user story is reachable without them.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Add the shell and navigation catalogue entries — `nav.dashboard`, `nav.settings`, `nav.toggle`, `shell.signOut` — to `frontend/src/locales/de.json`, reusing the existing `poll.listTitle` for the Terminfindungen entry rather than adding a second word for it (FR-002a, FR-035)
- [X] T006 Write the failing router test asserting the nested admin route table of research.md R-5 — `/admin` → dashboard, `/admin/terminfindungen`, `/admin/terminfindungen/:pollId`, `/admin/einstellungen`, `/admin/anmelden` outside the shell, and any unknown `/admin/*` redirecting to `/admin` — in `frontend/tests/unit/adminRoutes.spec.ts` (FR-005, FR-006, FR-007)
- [X] T007 Write the failing component test for the shell in `frontend/tests/unit/AdminShell.spec.ts`: `admin-shell` present, nested `RouterView` renders the child, `sign-out` present, wordmark links to `/admin`, and the shell absent on `/admin/anmelden` (FR-001, FR-008, FR-010, FR-036)
- [X] T008 Implement `frontend/src/components/admin/AdminShell.vue` — app bar, drawer container, nested `<RouterView>`, sign-out, wordmark to the dashboard — making T007 pass
- [X] T009 Rewrite the admin route table as nested children of the shell in `frontend/src/router.ts`, with `/admin` as the index child and a catch-all redirect, making T006 pass
- [X] T010 Remove the admin chrome conditional from `frontend/src/App.vue`, leaving it with the participant app bar only, and update `frontend/tests/unit/AppBrand.spec.ts` for the moved wordmark behaviour (FR-009)

**Checkpoint**: The shell exists and every address resolves. Nothing has moved into it yet.

---

## Phase 3: User Story 1 — One area at a time, reached from the left (Priority: P1) 🎯 MVP

**Goal**: A left navigation bar with settings last; date polls in their own clean area with answers
on their own address; settings and maintenance out of the poll area entirely.

**Independent Test**: Sign in, confirm the left navigation bar with settings last, open the
date-poll area and confirm every poll task is there and no maintenance control is, open one poll's
answers and confirm that address reloads to the same answers, then open settings and confirm the
maintenance and backup controls are there instead.

### Tests for User Story 1 ⚠️ Write first, observe failing

- [X] T011 [P] [US1] Write the failing navigation test in `frontend/tests/unit/AdminNav.spec.ts`: exactly three entries in order, settings last with nothing after it, labels from the catalogue, `aria-current="page"` on exactly one entry, and entry 2 still current on `/admin/terminfindungen/:pollId` (FR-002, FR-002a, FR-003, FR-004, FR-014e)
- [X] T012 [P] [US1] Extend `frontend/tests/unit/PollList.spec.ts` with failing assertions: summaries only and no grid rendered inline, `poll-create-toggle` and `poll-import-toggle` present with their forms hidden on arrival, at most one form open at a time, a rejected form staying open with its entry intact, the empty state offering creation, and `export-poll` and `delete-poll` still on the list (FR-014b, FR-014d, FR-014h, FR-014i, FR-014k, FR-014l)
- [X] T013 [P] [US1] Write the failing test for the answers page in `frontend/tests/unit/PollAnswersView.spec.ts`: title and message shown, `ResultGrid` rendered with `deletable`, `back-to-polls` present, no `export-poll` or `delete-poll` duplicated, deleting the last answer leaving the page on its empty state, and a 404 redirecting to the list (FR-014c, FR-014d, FR-014f, FR-014g)
- [X] T014 [P] [US1] Write the failing test for the relocated controls in `frontend/tests/unit/SettingsView.spec.ts`: `settings-maintenance`, `settings-backup` and `settings-restore` present, and no poll control present (FR-019, SC-002)
- [X] T015 [P] [US1] Extend `frontend/tests/unit/MaintenanceSwitch.spec.ts` with the failing assertion that the switch renders the toggle and **no** `maintenance-banner`, and add to `frontend/tests/unit/AdminShell.spec.ts` that the shell renders the banner from the store whenever maintenance is on, regardless of the area shown and regardless of the switch being mounted (FR-026, FR-026a, FR-026b, FR-026c)
- [X] T016 [P] [US1] Migrate `e2e/tests/admin-access.spec.ts` and `e2e/tests/admin-journey.spec.ts` to the new addresses, keeping every existing `data-testid` and every behavioural assertion unchanged, and asserting that signing in lands on `/admin` in every case (FR-011a, R-9)
- [X] T017 [P] [US1] Migrate `e2e/tests/import.spec.ts` and `e2e/tests/export-and-backup.spec.ts` — import behind its reveal in the poll area, backup download in settings — keeping their ids and assertions (R-9)
- [X] T018 [P] [US1] Migrate `e2e/tests/results-summary.spec.ts` to open a poll's answers at `/admin/terminfindungen/:pollId` instead of expanding them inline, keeping its summary and best-day assertions unchanged (FR-016, R-9)
- [X] T019 [P] [US1] Migrate `e2e/tests/maintenance.spec.ts` — the toggle in settings — and add the assertion the old arrangement could not make: switch maintenance on, navigate to the poll list, and find `maintenance-banner` there too (FR-026, SC-012)
- [X] T020 [P] [US1] Migrate `e2e/tests/storage-resilience.spec.ts` and `e2e/tests/date-poll-journey.spec.ts` for the new admin navigation, changing **only** their admin setup steps. Their participant assertions must not need a change — if one does, the implementation is wrong (Principle I, FR-009, R-9)
- [X] T021 [P] [US1] Add a failing e2e assertion to `e2e/tests/zero-signup.spec.ts` that a participant on `/u/:token` sees no `admin-shell`, no `admin-nav` and no link into `/admin`, and that the steps from link to answer form remain zero (FR-009, SC-010, constitution gate 3)

### Implementation for User Story 1

- [X] T022 [US1] Implement `frontend/src/components/admin/AdminNav.vue` — `v-navigation-drawer` with `v-list nav`, three `v-list-item` entries with `:to`, permanent from `md` up and temporary below, no hand-written `aria-current` — and mount it in `frontend/src/components/admin/AdminShell.vue`, making T011 pass (FR-002, FR-003, FR-012, FR-013; research R-2)
- [X] T023 [US1] Create `frontend/src/components/admin/SettingsView.vue` holding `MaintenanceSwitch`, the backup download and `RestorePanel`, and remove all three from `frontend/src/components/admin/PollList.vue`, making T014 pass (FR-015, FR-019, SC-002)
- [X] T024 [US1] Move the maintenance banner out of `frontend/src/components/admin/MaintenanceSwitch.vue` and into `frontend/src/components/admin/AdminShell.vue`, reading the `maintenance` store, making T015 pass. **One task on purpose** — splitting the lift from the relocation leaves an intermediate commit with the warning nowhere, passing its own tests while violating FR-026 (research R-6)
- [X] T025 [US1] Move sign-out out of `frontend/src/components/admin/PollList.vue`; the shell owns it after T008 (FR-010)
- [X] T026 [US1] Trim `frontend/src/components/admin/PollList.vue` to summaries only and put `PollForm` and `ImportPanel` behind `poll-create-toggle` and `poll-import-toggle`, with at most one open, entry discarded on close or on leaving, and the empty state offering creation, making T012 pass (FR-014b, FR-014h, FR-014i, FR-014j, FR-014l)
- [X] T027 [US1] Change the poll list's `show-results` control from an inline expander to a link to `/admin/terminfindungen/:pollId`, removing the `openPollId`/`openResults` state from `frontend/src/components/admin/PollList.vue` (FR-014a, FR-014b)
- [X] T028 [US1] Implement `frontend/src/components/admin/PollAnswersView.vue` — title, message, `ResultGrid` with `deletable`, `back-to-polls`, per-response deletion re-reading the poll, and a 404 redirect to the list — making T013 pass (FR-014a, FR-014c, FR-014f, FR-014g; research R-8)
- [X] T029 [US1] Add the catalogue entries for the new controls — the two reveal actions, `back-to-polls`, the settings section names, and the poll-not-there message — to `frontend/src/locales/de.json` (FR-035)
- [X] T030 [US1] Run the full suite and confirm T016–T021 pass with no change to any participant assertion (`npm run test:unit`, `dotnet test`, `npx playwright test`)

**Checkpoint**: User Story 1 is complete and demonstrable. The navigation works, the poll area is
clean, answers have their own address, and settings holds what moved.

---

## Phase 4: User Story 2 — Settings and maintenance, on a page of their own (Priority: P2)

**Goal**: The settings page as a deliberate page — three titled sections ordered by consequence,
restore last and set apart, and no half-finished step surviving a departure.

**Independent Test**: From the settings page, switch maintenance mode on and off, download a
backup, and run a restore through its preview and confirmation — all without leaving the page, and
with none of those three controls reachable from the date-poll area.

### Tests for User Story 2 ⚠️ Write first, observe failing

- [X] T031 [P] [US2] Extend `frontend/tests/unit/SettingsView.spec.ts` with failing assertions: each section has its own title and description, the sections appear in the order maintenance → backup → restore, and restore is last and visibly separated (FR-020, FR-020a)
- [X] T032 [P] [US2] Add the failing abandonment test to `frontend/tests/unit/SettingsView.spec.ts`: begin a restore as far as a chosen file and a preview, leave the settings area, return, and find no armed confirmation and no retained file (FR-022)
- [X] T033 [P] [US2] Extend `frontend/tests/unit/RestorePanel.spec.ts` with the failing assertion that the maintenance guard still refuses and still names the missing step after the move (FR-024)
- [X] T034 [P] [US2] Add a failing e2e assertion to `e2e/tests/maintenance.spec.ts` that maintenance keeps its asymmetry after the move — switching on asks first, switching off does not (FR-023)

### Implementation for User Story 2

- [X] T035 [US2] Give each section of `frontend/src/components/admin/SettingsView.vue` its own title and description and order them by consequence with restore last and visibly separated, making T031 pass (FR-020, FR-020a)
- [X] T036 [US2] Reset the restore and import step state when `frontend/src/components/admin/SettingsView.vue` unmounts, so no chosen file or preview survives leaving the area, making T032 pass (FR-022)
- [X] T037 [US2] Add the settings section titles and descriptions to `frontend/src/locales/de.json` (FR-020, FR-035)

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 — A dashboard that answers "what is in here?" (Priority: P3)

**Goal**: The landing page reports the installation's state in six figures.

**Independent Test**: Sign in with a known set of polls and answers and confirm the landing page
reports figures that match them — including the yes/maybe/no totals checked against the sum of the
per-poll summaries — then delete a poll and confirm every affected figure follows.

**⚠️ Depends on Phase 1's gate being resolved (T004a, T004b or T004c).**

### Tests for User Story 3 ⚠️ Write first, observe failing

- [X] T038 [P] [US3] Write the failing projection test in `backend/tests/Rundfrage.Api.UnitTests/DashboardProjectionTests.cs`: each of the five server figures against a known fixture, the seven-day window boundary, `nextDeletion` null only when no poll exists, and expired polls excluded from every figure (data-model.md)
- [X] T039 [P] [US3] Write the failing agreement test in `backend/tests/Rundfrage.Api.IntegrationTests/DashboardTests.cs` asserting that the yes/maybe/no aggregate equals the sum of every poll's per-day summaries from `ResultsProjection`, and that a poll with no answers contributes nothing rather than zeros (FR-028a, FR-029)
- [X] T040 [P] [US3] Add the failing endpoint tests to `backend/tests/Rundfrage.Api.IntegrationTests/DashboardTests.cs`: the payload shape of `contracts/openapi.yaml`, no poll title or participant name anywhere in it, and a 401 without a session (FR-034; matches `AdminAuthorizationTests.cs` conventions)
- [X] T041 [P] [US3] Write the failing store test in `frontend/tests/unit/dashboardStore.spec.ts` for the four states of research R-7 — loading, unreachable, unauthorized redirect, ready — mirroring `frontend/tests/unit/apiClient.spec.ts` conventions
- [X] T042 [P] [US3] Write the failing view test in `frontend/tests/unit/DashboardView.spec.ts`: six labelled figures, **no number rendered while loading**, no figure at all when unreachable, words rather than zeros when nothing is stored, the distribution's own empty state when polls exist but nothing is answered, and the three distinct readings of the deletions figure (FR-028, FR-031, FR-032; ui-contract §3)
- [X] T043 [P] [US3] Add the failing e2e dashboard test to `e2e/tests/admin-journey.spec.ts`: signing in lands on the dashboard with it marked current, and the figures follow a poll deletion (FR-027, FR-030)
- [X] T044 [P] [US3] Extend `e2e/tests/storage-resilience.spec.ts` with the failing assertion that an unreachable store leaves the dashboard showing no figure at all (FR-032, SC-009)

### Implementation for User Story 3

- [X] T045 [US3] Implement `backend/src/Rundfrage.Api/Polls/DashboardProjection.cs` with the five aggregate queries of data-model.md, all scoped by `RetentionService.LivePolls()`, in a fixed number of queries that does not grow with the poll count, making T038 and T039 pass (FR-028b)
- [X] T046 [US3] Implement `backend/src/Rundfrage.Api/Endpoints/Admin/DashboardEndpoint.cs` per `contracts/openapi.yaml` and register it and the projection in `backend/src/Rundfrage.Api/Program.cs`, making T040 pass
- [X] T047 [P] [US3] Add `fetchDashboard` and the `DashboardView` type to `frontend/src/api/client.ts` per `contracts/openapi.yaml`
- [X] T048 [US3] Implement `frontend/src/stores/dashboard.ts` mirroring the `polls` store's `figures`/`loading`/`loadProblem` shape so the `unauthorized` redirect and `useProblemText` carry over, making T041 pass (research R-7)
- [X] T049 [US3] Implement `frontend/src/components/admin/DashboardView.vue` with the six figures — maintenance read from the `maintenance` store and **not** from the payload — making T042 pass (FR-028, FR-033; data-model.md "Figure 5")
- [X] T050 [US3] Add the dashboard figure labels and empty-state wording to `frontend/src/locales/de.json` (FR-028, FR-031, FR-035)
- [X] T051 [US3] Run `npm run test:unit` in `frontend/`, `dotnet test` in `backend/` and `npx playwright test` in `e2e/`, confirming the dashboard tests T043 in `e2e/tests/admin-journey.spec.ts` and T044 in `e2e/tests/storage-resilience.spec.ts` pass

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T052 [P] Verify SC-007 on a 375-pixel-wide viewport: every area usable, the drawer reachable through `admin-nav-toggle`, and no content permanently covered — add the assertion to `e2e/tests/admin-access.spec.ts` (FR-012)
- [X] T053 [P] Verify SC-008 by keyboard alone across every area, asserting each navigation entry exposes its name and its current state, in `e2e/tests/admin-access.spec.ts` (FR-013)
- [X] T054 [P] Confirm `frontend/tests/unit/noLiteralStrings.spec.ts` covers every component added by this feature, and add any missed file to its scan (FR-035)
- [X] T055 Re-check every gate of the Constitution Check in `specs/007-admin-shell-dashboard/plan.md` against the delivered code and record the post-implementation result
- [X] T056 Walk `specs/007-admin-shell-dashboard/quickstart.md` end to end on a clean checkout and correct anything that has drifted
- [X] T057 Run the full suite green — `npm run test:unit`, `npm run typecheck`, `npm run build`, `dotnet test`, `npx playwright test` — noting the build fails on warnings and type errors (constitution gate 2)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Risk Resolution)**: No dependencies. Gates **US3 only**; Phases 2–4 do not wait on it
- **Phase 2 (Foundational)**: No dependencies. **Blocks all user stories**
- **Phase 3 (US1)**: Depends on Phase 2
- **Phase 4 (US2)**: Depends on Phase 3 — T023 creates the settings page that US2 refines
- **Phase 5 (US3)**: Depends on Phase 2 and Phase 1's gate. Independent of US1 and US2
- **Phase 6 (Polish)**: Depends on all desired stories

### Critical couplings — do not split these

- **T024** lifts the banner and strips it from the switch in one task. Split it and there is an
  intermediate commit with the warning nowhere, passing its own tests while violating FR-026
  (research R-6)
- **T004a/b/c** is a branch, not a list. Exactly one applies, chosen by T002's measurement
- **T020 and T021** must not require changes to participant assertions. If they do, the
  implementation has touched the participant path and FR-009 is broken

### Parallel Opportunities

- **T001 and T005** can start together on day one: the scale fixture is backend-only and the
  catalogue entries are frontend-only
- **Phase 1 runs in parallel with Phases 2–4 entirely.** The spike is backend; the shell, poll area
  and settings are frontend. Only US3 waits
- **Phase 5's backend (T038–T040, T045–T046) is independent of all frontend work** and can proceed
  alongside Phase 3 once Phase 1's gate is resolved
- Within Phase 3, all eleven test tasks T011–T021 are `[P]` — different files, no shared state
- Within Phase 5, all seven test tasks T038–T044 are `[P]`
- T022–T028 are **not** parallel with each other: T023, T025, T026 and T027 all edit
  `PollList.vue`, and T024 and T022 both edit `AdminShell.vue`

### Within Each User Story

Tests are written and observed failing before implementation. Backend projection before endpoint;
store before view; catalogue entries alongside the component that needs them, never afterwards.

---

## Parallel Example: User Story 1 tests

```bash
# All eleven test tasks touch different files and can be written together:
Task: "T011 AdminNav.spec.ts — three entries, settings last, aria-current"
Task: "T012 PollList.spec.ts — summaries only, forms behind reveals"
Task: "T013 PollAnswersView.spec.ts — own address, back link, 404 redirect"
Task: "T014 SettingsView.spec.ts — the three relocated controls"
Task: "T015 MaintenanceSwitch.spec.ts + AdminShell.spec.ts — banner in the shell"
Task: "T016 e2e admin-access + admin-journey migration"
Task: "T017 e2e import + export-and-backup migration"
Task: "T018 e2e results-summary migration"
Task: "T019 e2e maintenance migration + cross-area banner"
Task: "T020 e2e storage-resilience + date-poll-journey migration"
Task: "T021 e2e zero-signup — no admin surface for participants"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 2 (Foundational) — the shell and its routes
2. Phase 3 (US1) — navigation, clean poll area, answers on their own address
3. **STOP and VALIDATE** — the restructure is the point of this feature and stands on its own
4. Phase 1 can run alongside, in the background, without blocking any of this

### Incremental Delivery

1. Foundational → the shell exists, nothing has moved
2. US1 → the restructure, demonstrable (**MVP**)
3. US2 → settings becomes a deliberate page
4. US3 → the dashboard, once Phase 1's gate is answered

### Recommended sequencing for one developer

Start T001 (the scale fixture) and let its measurement run while working T005–T010, because T002's
result may amend the spec and it is better to know early. Then US1 straight through. US3 last, by
which time the gate is long resolved.

---

## Notes

- Existing `data-testid` values are **kept** wherever a control only moves (R-9). A diff should be
  a move or a rename, never both — a real regression can hide in renaming noise
- Do not write `aria-current`. Vue Router sets it on the active link; assert that, not a CSS class
- The dashboard's loading state renders no numbers. Two seconds of zeros is a dashboard making
  false claims
- Figure 6 needs no "exclude the unanswered" filter. `DayAnswer` stores no row for an unanswered
  day, so a grouped count cannot count one. Do not add a filter
- `components/poll/` is untouched by every task here. That is the participant surface and
  Principle I is why
- Commit after each task or logical group; stop at any checkpoint to validate a story on its own
