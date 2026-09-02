---
description: "Task list for Platform Scaffold (Walking Skeleton)"
---

# Tasks: Platform Scaffold (Walking Skeleton)

**Input**: Design documents from `/specs/001-platform-scaffold/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: Test tasks are MANDATORY here (Constitution Principle II, FR-022). Every behaviour
task is preceded by the test task that defines it, and that test MUST be observed failing
before implementation begins.

**Organization**: Grouped by user story so each can be implemented, tested, and demonstrated
independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete work)
- **[Story]**: US1, US2, US3 — maps to the user stories in spec.md
- All paths are relative to the repository root `/Users/hendrik/repos/Rundfrage`

## Environment note

Spec Kit branch validation expects an `NNN-slug` branch, but development happens on `dev`
(FR-015). Run every Spec Kit command for this feature with:

```bash
export SPECIFY_FEATURE=001-platform-scaffold
```

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Repository layout and project skeletons. No behaviour yet, so no tests here.

- [X] T001 Flatten the repository so git root == project root, moving `.specify/`, `.claude/`, `CLAUDE.md`, `specs/` into `/Users/hendrik/repos/Rundfrage` with `git mv` (research.md R-8) — **executed 2026-09-02**
- [X] T002 Create `.gitignore` at repository root covering macOS, .NET, Node, Playwright and `.env` — **executed 2026-09-02**
- [ ] T003 Create the .NET solution at `backend/Rundfrage.sln` targeting .NET 10
- [ ] T004 Create the ASP.NET Core project `backend/src/Rundfrage.Api/Rundfrage.Api.csproj` with package references for `Npgsql.EntityFrameworkCore.PostgreSQL`, `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Formatting.Compact`
- [ ] T005 [P] Create the xUnit unit test project `backend/tests/Rundfrage.Api.UnitTests/Rundfrage.Api.UnitTests.csproj` referencing the API project
- [ ] T006 [P] Create the xUnit integration test project `backend/tests/Rundfrage.Api.IntegrationTests/Rundfrage.Api.IntegrationTests.csproj` with `Microsoft.AspNetCore.Mvc.Testing` and `Testcontainers.PostgreSql`
- [ ] T007 [P] Scaffold the Vue 3 + Vite project in `frontend/` with `vue`, `vuetify`, `pinia`, `vue-i18n`, and dev dependencies `vite`, `vitest`, `@vue/test-utils`, `jsdom`
- [ ] T008 [P] Create `frontend/vite.config.ts` with the dev proxy `/api` → `http://localhost:8080` (FR-003b, research.md R-10)
- [ ] T009 [P] Create `frontend/vitest.config.ts` using the jsdom environment
- [ ] T010 [P] Scaffold the Playwright project at `e2e/playwright.config.ts` with `baseURL: http://localhost:8080`
- [ ] T011 [P] Create `.dockerignore` at repository root excluding `.git/`, `node_modules/`, `bin/`, `obj/`, `dist/`, `.env*`
- [ ] T012 [P] Create `.env.example` at repository root documenting `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `LOG_LEVEL` (FR-005, data-model.md §4)

**Checkpoint**: Projects compile and empty test suites run.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting infrastructure every user story depends on.

**⚠️ CRITICAL**: No user story work may begin until this phase is complete.

- [ ] T013 Write a failing test in `backend/tests/Rundfrage.Api.UnitTests/LoggingConfigurationTests.cs` asserting Serilog emits structured entries to stdout and honours `LOG_LEVEL` (FR-024, FR-025)
- [ ] T014 Configure Serilog in `backend/src/Rundfrage.Api/Program.cs` with the compact JSON formatter writing to stdout and the minimum level bound to `LOG_LEVEL`; set the `Npgsql` source to `Warning` (research.md R-5 measure 3) — makes T013 pass
- [ ] T015 Create `backend/src/Rundfrage.Api/Data/RundfrageDbContext.cs` with **no `DbSet<>`** and **without** `EnableRetryOnFailure` (data-model.md §1, research.md R-3)
- [ ] T016 Register the DbContext in `backend/src/Rundfrage.Api/Program.cs` using `ConnectionStrings__Default` including `Timeout=2` (research.md R-3 layer 1)
- [ ] T017 Generate the deliberately empty initial EF Core migration into `backend/src/Rundfrage.Api/Data/Migrations/` (research.md R-1)
- [ ] T018 [P] Create `frontend/src/locales/de.json` with the keys `app.title`, `status.loading`, `status.database.reachable`, `status.database.unreachable`, `status.backend.unreachable` (FR-028, data-model.md §3)
- [ ] T019 [P] Register vue-i18n in `frontend/src/main.ts` with `de` as the only locale and fallback (FR-029)
- [ ] T020 Configure `/api/v1` routing plus SPA fallback in `backend/src/Rundfrage.Api/Program.cs`, so unmatched paths serve `wwwroot/index.html` and API paths never collide with client-side routes (FR-006a)

**Checkpoint**: Logging, data access, i18n and routing conventions exist. User stories can start.

---

## Phase 3: User Story 1 — Fresh clone starts with one command (Priority: P1) 🎯 MVP

**Goal**: `docker compose up` on a fresh clone yields a reachable page displaying text that
came from the backend.

**Independent Test**: On a clean machine, clone, run the single command, open the address, and
see backend-provided text. Requires neither US2 nor US3.

### Tests (write first, observe failing)

- [ ] T021 [P] [US1] Write a failing contract test in `backend/tests/Rundfrage.Api.IntegrationTests/MessageEndpointTests.cs` asserting `GET /api/v1/message` returns 200 with a non-empty `message` field, matching `contracts/openapi.yaml`
- [ ] T022 [P] [US1] Write a failing Vitest test in `frontend/tests/unit/SystemStatus.message.spec.ts` asserting the component renders the message fetched from the API, asserted via `data-testid` (FR-030)
- [ ] T023 [P] [US1] Write a failing Playwright test in `e2e/tests/walking-skeleton.spec.ts` asserting the page served by Compose displays the backend text

### Implementation

- [ ] T024 [US1] Implement `GET /api/v1/message` in `backend/src/Rundfrage.Api/Endpoints/MessageEndpoint.cs` returning `{ "message": ... }` — makes T021 pass
- [ ] T025 [P] [US1] Implement the API client in `frontend/src/api/client.ts` targeting the same-origin `/api/v1` prefix (FR-003a)
- [ ] T026 [US1] Implement `frontend/src/components/SystemStatus.vue` rendering the message with a `data-testid`, all text via i18n keys — makes T022 pass
- [ ] T027 [US1] Wire the component into `frontend/src/App.vue`
- [ ] T028 [US1] Create the multi-stage build at `docker/Dockerfile`: Node stage runs `npm ci && npm run build`, .NET stage publishes and receives `dist/` as `wwwroot/` (plan.md Structure Decision)
- [ ] T029 [US1] Create `compose.yaml` at repository root with exactly two services, `app` and `db`, and inline defaults via `${VAR:-default}` so no `.env` is needed (FR-003, FR-001, research.md R-11) — makes T023 pass
- [ ] T030 [US1] Add a named volume for the database in `compose.yaml` so contents survive restart (FR-004)

**Checkpoint**: MVP delivered — one command, one origin, page shows backend text.

---

## Phase 4: User Story 2 — The page reports database connectivity (Priority: P2)

**Goal**: The page shows, at a glance, whether the database is reachable — and stays usable
when it is not.

**Independent Test**: Confirm the success state; stop the database, reload, confirm the
failure state; restore it and confirm recovery without restart.

### Tests (write first, observe failing)

- [ ] T031 [P] [US2] Write a failing unit test in `backend/tests/Rundfrage.Api.UnitTests/DatabaseProbeTests.cs` asserting the probe reports `Unreachable` and returns within 2 s when the database does not respond (FR-012, SC-004a)
- [ ] T032 [P] [US2] Write a failing unit test in `backend/tests/Rundfrage.Api.UnitTests/LogRedactionTests.cs` asserting a probe failure with a wrong password logs neither the password nor the connection string (FR-026, research.md R-5 measure 4, SC-011)
- [ ] T033 [P] [US2] Write a failing unit test in `backend/tests/Rundfrage.Api.UnitTests/DatabaseProbeTests.cs` asserting each check emits exactly one log entry carrying outcome and duration (FR-027)
- [ ] T034 [P] [US2] Write a failing integration test in `backend/tests/Rundfrage.Api.IntegrationTests/StatusEndpointTests.cs` asserting `GET /api/v1/status/database` returns **200 with `state: "reachable"`** against a Testcontainers database
- [ ] T035 [P] [US2] Write a failing integration test in `backend/tests/Rundfrage.Api.IntegrationTests/StatusEndpointTests.cs` asserting the endpoint returns **200 — not 503 — with `state: "unreachable"`** when the database is down (contracts/openapi.yaml, research.md R-4)
- [ ] T036 [P] [US2] Write a failing integration test in `backend/tests/Rundfrage.Api.IntegrationTests/StatusEndpointTests.cs` asserting the response carries no connection string, credential, host name, or stack trace (FR-014)
- [ ] T037 [P] [US2] Write a failing integration test in `backend/tests/Rundfrage.Api.IntegrationTests/SchemaCreationTests.cs` asserting that starting against an empty Testcontainers database creates `__EFMigrationsHistory`, and that a second start is a safe no-op (FR-013, FR-013a)
- [ ] T038 [P] [US2] Write a failing integration test in `backend/tests/Rundfrage.Api.IntegrationTests/StartupResilienceTests.cs` asserting the host starts and serves when the database is unreachable at startup (FR-011, research.md R-2)
- [ ] T039 [P] [US2] Write a failing Vitest test in `frontend/tests/unit/statusStore.spec.ts` asserting the store maps 2xx+reachable, 2xx+unreachable, and request failure to the three UI states (data-model.md §3)
- [ ] T040 [P] [US2] Write a failing Vitest test in `frontend/tests/unit/SystemStatus.states.spec.ts` asserting all three states render distinguishably, asserted via `data-testid` and translation keys (FR-010, FR-030)
- [ ] T041 [P] [US2] Write failing Playwright tests in `e2e/tests/database-status.spec.ts` covering: database up, `docker compose stop db`, database restored (SC-005), and `docker compose stop app` for the third state

### Implementation

- [ ] T042 [US2] Implement `backend/src/Rundfrage.Api/Diagnostics/DatabaseProbe.cs` executing `SELECT 1` via `ExecuteSqlRawAsync` under a 2-second `CancellationTokenSource`, returning `ConnectivityStatus` (FR-008, research.md R-3) — makes T031 pass
- [ ] T043 [US2] Add the single structured log entry with outcome and duration to `DatabaseProbe`, logging exception type and a sanitised message only — makes T032 and T033 pass
- [ ] T044 [US2] Implement `GET /api/v1/status/database` in `backend/src/Rundfrage.Api/Endpoints/StatusEndpoint.cs`, always returning 200 with the state token, timestamp and duration, never internals — makes T034, T035, T036 pass
- [ ] T045 [US2] Implement the startup migration in `backend/src/Rundfrage.Api/Program.cs` with a bounded retry (5 attempts, exponential backoff, ~30 s) that logs at `Error` and lets the host start on final failure — makes T037 and T038 pass
- [ ] T046 [P] [US2] Implement `frontend/src/stores/status.ts` deriving `backendUnreachable` from any failed or non-2xx response — makes T039 pass
- [ ] T047 [US2] Extend `frontend/src/components/SystemStatus.vue` to render the tri-state plus the loading state via i18n keys and `data-testid` — makes T040 pass
- [ ] T048 [US2] Add the `db` healthcheck and plain `depends_on: [db]` (no `condition: service_healthy`) to `compose.yaml` (research.md R-6) — makes T041 pass

**Checkpoint**: The full chain browser → backend → database is proven, including its failure paths.

---

## Phase 5: User Story 3 — Every push is built and tested automatically (Priority: P3)

**Goal**: Pushes and pull requests on `dev` and `main` build the system and run every suite.

**Independent Test**: Push to `dev` and observe a run; push a deliberately broken test and
observe failure.

- [ ] T049 [US3] Create the `dev` and `main` branches and confirm `dev` is the working branch (FR-015) — `dev` created 2026-09-02, verify `main` still represents released state
- [ ] T050 [US3] Create `.github/workflows/ci.yml` at the **git repository root** triggered on push to `dev`/`main` and on pull requests targeting them (FR-016)
- [ ] T051 [US3] Add the `backend` job to `.github/workflows/ci.yml` running `dotnet test backend/` including the Testcontainers integration tests
- [ ] T052 [P] [US3] Add the `frontend` job to `.github/workflows/ci.yml` running `npm ci && npm run test:unit` in `frontend/`
- [ ] T053 [P] [US3] Add the `e2e` job to `.github/workflows/ci.yml` running `docker compose up -d --build`, waiting for the `db` healthcheck, then `npx playwright test`
- [ ] T054 [US3] Confirm the workflow builds and tests only — no image build, tag, push, or release artifact, and no deployment step (FR-018, FR-019)
- [ ] T055 [US3] Verify a deliberately broken test fails the workflow and names the failing test, then revert the break (SC-007)

**Checkpoint**: Regressions in US1 and US2 are caught automatically.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [ ] T056 [P] Write `README.md` at repository root documenting the start command, the application address, and the meaning of each of the three database states (FR-023), derived from `quickstart.md`
- [ ] T057 [P] Add the `test:unit` script and any missing scripts to `frontend/package.json`
- [ ] T058 Verify SC-001 and SC-002 by hand: fresh clone, one command, working system in under 10 minutes with zero additional manual steps
- [ ] T059 Verify SC-003 and SC-004 by measurement: page renders text and state within 5 s in both the healthy and the database-down case
- [ ] T060 Verify SC-010 with `docker compose ps`: exactly two containers, and confirm no CORS configuration exists anywhere in the codebase
- [ ] T061 Verify SC-012 by scanning `frontend/src/` for literal user-facing strings; the expected count is zero (FR-029)
- [ ] T062 Confirm the Complexity Tracking table in `plan.md` still matches what was built, in particular the i18n deviation from Principle III

---

## Dependencies

**Phase order**: Setup (T001–T012) → Foundational (T013–T020) → US1 (T021–T030) → US2 (T031–T048) → US3 (T049–T055) → Polish (T056–T062)

**Story dependencies**:

- **US1** depends only on Setup and Foundational. It is the MVP and ships alone.
- **US2** depends on US1 for the page and the container set it extends. Its backend tasks
  (T031–T038, T042–T045) depend only on Foundational and could be built earlier if desired.
- **US3** is independent of both. It can be built at any time after Setup, but delivers no
  value until there is something to build — hence P3.

**Test-first ordering (Principle II)**: within every story, all listed test tasks precede
their implementation tasks. T013 precedes T014; T021–T023 precede T024–T030; T031–T041
precede T042–T048.

---

## Parallel Execution Opportunities

- **Setup**: T005–T012 are all `[P]` — different files, no interdependencies.
- **US1 tests**: T021, T022, T023 target three different suites and can be written together.
- **US2 tests**: T031–T041 are all `[P]` — eleven independent test files.
- **US2 implementation**: T046 `[P]` (frontend store) is independent of the backend tasks
  T042–T045 once the contract in `contracts/openapi.yaml` is fixed.
- **CI jobs**: T052 and T053 are `[P]` relative to T051.
- **Polish**: T056 and T057 are `[P]`.

---

## Implementation Strategy

**MVP = Phase 1 + Phase 2 + Phase 3 (US1)** — 30 tasks, of which 2 are already done. That
alone satisfies the walking-skeleton promise: one command, one origin, backend text on the
page.

**Increment 2 = Phase 4 (US2)** turns the two-tier demo into the real Durchstich through the
database, including the failure paths that make it trustworthy.

**Increment 3 = Phase 5 (US3)** protects both against silent decay.

Each increment is independently demonstrable, and the checkpoints mark the points at which the
system can be shown to someone.
