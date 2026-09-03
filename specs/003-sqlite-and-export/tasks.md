---
description: "Task list for SQLite Storage and JSON Export"
---

# Tasks: SQLite Storage and JSON Export

**Input**: Design documents from `/specs/003-sqlite-and-export/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: Test tasks are MANDATORY (Constitution Principle II, FR-027). Every behaviour task is
preceded by the test task that defines it, and that test MUST be observed failing first.

**Organization**: grouped by user story. US1 (one container) is the MVP; US2 (JSON export) is
independent of it by design and could ship on its own.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1 or US2; Setup, Foundational and Polish carry no story label

## A note on the shape of this list

This feature is mostly **subtraction**, and subtraction has a failure mode that addition does not:
a deleted test takes its assertion with it silently. Three rules follow, and they are why several
tasks below look like bookkeeping.

1. **A removal task never stands alone.** Every deletion of a test is paired with the task that
   re-points what it was proving onto the product (FR-024b).
2. **Four guarantees are re-proven, not inherited** (FR-029). They passed against PostgreSQL; the
   suite would keep passing against SQLite for the wrong reason if the mechanism underneath were
   quietly gone.
3. **The build is red on purpose between T001 and T020.** Swapping the provider breaks compilation
   at every site that stored a `DateTimeOffset` — that list of compiler errors *is* the Phase 2
   work list, and it is better to see it than to guess at it.

---

## Phase 1: Setup

**Purpose**: swap the dependency and state the new configuration contract.

- [X] T001 Replace `Npgsql.EntityFrameworkCore.PostgreSQL` with `Microsoft.EntityFrameworkCore.Sqlite` in `backend/src/Rundfrage.Api/Rundfrage.Api.csproj`
- [X] T002 [P] Remove the `Testcontainers.PostgreSql` package reference from `backend/tests/Rundfrage.Api.IntegrationTests/Rundfrage.Api.IntegrationTests.csproj` (research R-6)
- [X] T003 [P] Rewrite the storage section of `.env.example`: drop `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`; add `DATA_DIR` with the working default and a comment naming FR-007b — whoever reads the file reads every answer
- [X] T004 [P] Add the local data directory to `.gitignore` so a developer's storage file cannot be committed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the storage provider, the instant type it forces, and the test harness. Nothing in
US1 or US2 can be verified until the suite can run at all.

**Blocks**: US1 entirely; US2's backend tests.

### The test harness

- [X] T005 Create `backend/tests/Rundfrage.Api.IntegrationTests/SqliteFixture.cs` giving each test class its own temporary storage file in a temporary directory, deleted on dispose — the replacement for a disposable container, and still genuinely empty per class (research R-6)
- [X] T006 Delete `backend/tests/Rundfrage.Api.IntegrationTests/PostgresFixture.cs`
- [X] T007 Update `backend/tests/Rundfrage.Api.IntegrationTests/ApiFactory.cs`: the constructor takes a storage path, and `UnreachableConnection` becomes an **unwritable path** rather than a non-routable host — the honest SQLite equivalent of "the database is down"
- [X] T008 Re-point the fixture in all ten integration test classes (`AdminAuthorizationTests`, `AdminLoggingTests`, `ParticipationTests`, `PollListingTests`, `ResponseLimitTests`, `ResultsTests`, `RetentionTests`, `RevisionTests`, `ScaleTests`, `SchemaCreationTests`) in `backend/tests/Rundfrage.Api.IntegrationTests/`

### Instants become UTC `DateTime` (research R-1)

- [X] T009 Run `dotnet test backend/Rundfrage.slnx` and record the failures — `InvalidOperationException` on the retention filter and `NotSupportedException` on the two orderings. These are the failing tests for T010–T015; they must be observed before anything below is changed
- [X] T010 [P] Update `backend/tests/Rundfrage.Api.UnitTests/BerlinClockTests.cs` to assert UTC `DateTime` deadlines, including the two summer-time cases at lines 58–75 that must keep their exact expected instants
- [X] T011 Change `CreatedAt` and `RetentionDeadline` to UTC `DateTime` in `backend/src/Rundfrage.Api/Data/Entities/Poll.cs`, and `SubmittedAt` in `backend/src/Rundfrage.Api/Data/Entities/PollResponse.cs`
- [X] T012 Update `backend/src/Rundfrage.Api/Time/BerlinClock.cs`: `RetentionDeadlineFor` returns a UTC `DateTime`, `HasPassed` takes one, and `Now` gains a UTC `DateTime` companion. The zone logic and the `ResolveZone` failure message are untouched
- [X] T013 [P] Fix the deadline filter in `LivePolls()` and `EraseExpiredAsync()` in `backend/src/Rundfrage.Api/Retention/RetentionService.cs`
- [X] T014 [P] Fix the `ORDER BY` and the `RetentionDeadline` field of the summary record in `backend/src/Rundfrage.Api/Polls/PollService.cs`
- [X] T015 [P] Fix the row `ORDER BY` in `backend/src/Rundfrage.Api/Polls/ResultsProjection.cs` and the response record in `backend/src/Rundfrage.Api/Endpoints/Admin/PollAdminEndpoints.cs`
- [X] T016 Update the eight `DateTimeOffset.UtcNow` sites in the integration tests (`RetentionTests` ×3, `ParticipationTests` ×2, `PollListingTests` ×2, `RevisionTests`, `ResponseLimitTests`, `ScaleTests`) — these set deadlines and submission instants directly and must match the stored type

### The write path without `FOR UPDATE` (research R-2)

- [X] T017 Confirm `ResponseLimitTests` and `ScaleTests` now fail: the raw `SELECT 1 ... FOR UPDATE` in `ResponseService` is PostgreSQL syntax and SQLite rejects it. This is the failing test for T018
- [X] T018 Replace the row lock with the write transaction described in research R-2 in `backend/src/Rundfrage.Api/Polls/ResponseService.cs`, keeping the count-check-then-insert shape from feature 002 R-9 unchanged

### Storage settings that carry requirements

- [X] T019 Write `backend/tests/Rundfrage.Api.IntegrationTests/StorageSettingsTests.cs` asserting, by querying the connection itself, that journal mode is WAL (FR-012b), `synchronous` is FULL (FR-012a) and a busy timeout is set (FR-009, FR-010) — on a connection the application handed out, not one the test opened with its own settings
- [X] T020 Create `backend/src/Rundfrage.Api/Data/StorageSetup.cs` applying those three settings to every connection, and wire `UseSqlite` plus the `DATA_DIR` resolution into `backend/src/Rundfrage.Api/Program.cs` (FR-002, FR-007). The build goes green here

### The schema

- [X] T021 Delete `20260902081756_InitialEmpty*`, `20260902200534_DatePoll*` and `RundfrageDbContextModelSnapshot.cs` from `backend/src/Rundfrage.Api/Data/Migrations/`, then generate one fresh SQLite migration (research R-10)
- [X] T022 Extend `backend/tests/Rundfrage.Api.IntegrationTests/SchemaCreationTests.cs`: storage is created against an empty directory with no manual step (FR-004), and applying the schema a second time over existing storage is safe (FR-005)
- [X] T023 Update `backend/src/Rundfrage.Api/Data/DatabaseStartup.cs` — the retry budget existed because a database server is commonly still starting; a local file is not. Keep the "never prevent the host from serving" guarantee (FR-024), shorten the reasoning to what is still true
- [X] T024 Run `dotnet test backend/Rundfrage.slnx`. Every test that passed before this feature passes now, with no assertion weakened (FR-028, SC-008)

**Checkpoint**: storage is swapped and the existing suite is green. US1 and US2 can proceed.

---

## Phase 3: User Story 1 — The system runs as one container (P1) 🎯 MVP

**Goal**: one container, one file, a backup that actually restores, and the walking skeleton
retired without losing what it proved.

**Independent test**: start on a machine with no previous state, create a poll, stop everything —
exactly one application container ran and the whole state is in one mounted directory.

### The four guarantees, re-proven against SQLite (FR-029)

- [X] T025 [US1] Write `backend/tests/Rundfrage.Api.IntegrationTests/ConcurrentWriteTests.cs`: many participants submit at the same instant against a poll one short of the cap — exactly one is accepted, the rest refused, none errors, and the stored count is exact (FR-010, SC-006, SC-006a)
- [X] T026 [US1] Extend `backend/tests/Rundfrage.Api.IntegrationTests/ConcurrentWriteTests.cs` with the second concurrency case: two participants submitting simultaneously to a poll with room both succeed and neither overwrites the other (SC-006b)
- [X] T027 [US1] Write `backend/tests/Rundfrage.Api.IntegrationTests/DurabilityTests.cs`: an answer confirmed to its participant is present in storage read by a *separate* connection immediately afterwards, with no clean shutdown in between (FR-012a, SC-007)
- [X] T028 [US1] Verify T027 has teeth: set `synchronous=NORMAL` in `backend/src/Rundfrage.Api/Data/StorageSetup.cs`, observe what changes, restore `FULL`. Record the result in the task list. A durability test that passes under both settings is testing nothing

### Backup (FR-003, FR-003a)

- [X] T029 [US1] Write `backend/tests/Rundfrage.Api.IntegrationTests/BackupTests.cs`: a backup taken **while writes are in flight** opens as a standalone database, contains every table, and is internally consistent — never half a response (SC-005)
- [X] T030 [US1] Add the counter-case to `backend/tests/Rundfrage.Api.IntegrationTests/BackupTests.cs`: a hand copy of only the main storage file, taken under the same conditions, is **not** usable. This is the measurement that justifies FR-003b existing at all
- [X] T031 [US1] Add the authorization case: `GET /api/v1/admin/backup` without a session is refused exactly as every other admin function is
- [X] T032 [US1] Create `backend/src/Rundfrage.Api/Data/BackupService.cs` producing a consistent, self-contained copy through SQLite's online backup mechanism (research R-3)
- [X] T033 [US1] Create `backend/src/Rundfrage.Api/Endpoints/Admin/BackupEndpoint.cs` per `contracts/openapi.yaml`: streams the artefact, sets a timestamped `Content-Disposition`, answers 503 when storage is unreachable, and is mapped inside the existing `/admin` group in `backend/src/Rundfrage.Api/Program.cs`
- [X] T034 [US1] Add the temporary-file cleanup assertion to `BackupTests.cs`: nothing produced for a download outlives the request (FR-021)

### Storage that cannot be reached (FR-024, FR-024a)

- [X] T035 [US1] Rewrite `backend/tests/Rundfrage.Api.IntegrationTests/StartupResilienceTests.cs` against an unwritable storage path: the application starts and serves, rather than failing to boot
- [X] T036 [US1] Add to `backend/tests/Rundfrage.Api.IntegrationTests/StartupResilienceTests.cs`: with storage unreachable, an attempt to record an answer is refused — the system does not pretend to accept it (spec Edge Cases)
- [X] T037 [US1] Write `frontend/tests/unit/PollList.storage.spec.ts` asserting the admin area shows that something is wrong with storage, rather than an empty list that looks like "no polls yet" (FR-024a)
- [X] T038 [US1] Implement that state in `frontend/src/components/admin/PollList.vue` and add its keys to `frontend/src/locales/de.json`

### Backup in the interface

- [X] T039 [US1] Write `frontend/tests/unit/PollList.backup.spec.ts` for the backup action — present, and triggering the download
- [X] T040 [US1] Add the backup action to `frontend/src/components/admin/PollList.vue` and its client call to `frontend/src/api/client.ts`
- [X] T041 [US1] Add `backup.*` keys to `frontend/src/locales/de.json` (`frontend/tests/unit/noLiteralStrings.spec.ts` enforces that no German literal reaches a component)

### One container (FR-001, FR-026)

- [X] T042 [US1] Rewrite `compose.yaml`: a single `app` service, a named volume mounted at `DATA_DIR`, and the `db` service, its healthcheck, its environment and its volume removed. The `depends_on` comment about deliberately not gating on database health goes with them (research R-7)
- [X] T043 [US1] Update the header comment of `compose.yaml` — it currently states "exactly two services (FR-003, SC-010)", citing feature 001's requirements, which this feature supersedes
- [X] T044 [US1] Create the storage directory with access for the application's account only in `docker/Dockerfile`, keeping the existing `USER $APP_UID` and the `tzdata` line and its explanation (FR-007a, research R-8)
- [X] T045 [US1] Write `backend/tests/Rundfrage.Api.IntegrationTests/StoragePermissionTests.cs` asserting the storage file's mode excludes group and other (FR-007a, SC-011a)

### Retiring the walking skeleton (FR-022, FR-023) — and keeping what it proved (FR-024b)

- [X] T046 [US1] Create `e2e/tests/storage-resilience.spec.ts` carrying forward the three assertions worth keeping, now pointed at the product: the application is served from a single origin with no cross-origin request (constitution Principle IV), the admin area still renders when storage is unreachable, and it recovers on reload with no restart
- [X] T047 [US1] Delete `e2e/tests/walking-skeleton.spec.ts` and `e2e/tests/database-status.spec.ts` — only after T046 is green
- [X] T048 [US1] Fix the CI readiness gate in `.github/workflows/ci.yml`: it polls `/api/v1/message`, which this task removes. Point it at a path that survives, and delete the "wait for the database to become healthy" step that references `docker compose ps -q db`
- [X] T049 [US1] Delete `backend/src/Rundfrage.Api/Endpoints/MessageEndpoint.cs` and `StatusEndpoint.cs`, the whole `backend/src/Rundfrage.Api/Diagnostics/` directory, and their registrations in `Program.cs`
- [X] T050 [US1] Delete `backend/tests/Rundfrage.Api.IntegrationTests/MessageEndpointTests.cs`, `StatusEndpointTests.cs` and `backend/tests/Rundfrage.Api.UnitTests/DatabaseProbeTests.cs`
- [X] T051 [US1] Delete `frontend/src/components/SystemStatus.vue`, `frontend/src/stores/status.ts`, and the `/status` route in `frontend/src/router.ts`
- [X] T052 [US1] Delete `frontend/tests/unit/statusStore.spec.ts`, `SystemStatus.message.spec.ts` and `SystemStatus.states.spec.ts`
- [X] T053 [US1] Remove the now-unused `message.*` and `status.*` key groups from `frontend/src/locales/de.json`
- [X] T054 [US1] Scan `backend/src/`, `frontend/src/`, `e2e/` and `docker/` for orphans: no reference to the removed endpoints, components, store or probe survives anywhere, including comments that explain vanished behaviour (FR-030, SC-013)

### End-to-end

- [X] T055 [US1] Extend `e2e/tests/storage-resilience.spec.ts` with the restart case: create a poll, restart the container set, every poll and answer is still there (SC-004)

**Checkpoint**: US1 is independently testable and shippable.

---

## Phase 4: User Story 2 — The creator takes a poll out as JSON (P2)

**Goal**: one JSON file per poll, containing the poll and every answer, carrying no capability.

**Independent test**: create a poll, record two answers, download it, confirm the file contains
the title, the days, both participants and their answers.

- [X] T056 [US2] Write `backend/tests/Rundfrage.Api.IntegrationTests/ExportTests.cs` for the shape in `contracts/openapi.yaml`: `formatVersion`, `exportedAt`, title, message, days in chronological order, and every response with its display name and per-day answers (FR-014, SC-009)
- [X] T057 [US2] Add the token test to `backend/tests/Rundfrage.Api.IntegrationTests/ExportTests.cs` — scan the produced document against the participant token and every edit token **actually held in storage**, not against a regex for what a token looks like (FR-015, SC-010)
- [X] T058 [US2] Add the absence test to `backend/tests/Rundfrage.Api.IntegrationTests/ExportTests.cs`: a day a participant did not answer is missing from their answers rather than carrying a fourth value (SC-009a)
- [X] T059 [US2] Add the empty case to `backend/tests/Rundfrage.Api.IntegrationTests/ExportTests.cs`: a poll with no responses exports successfully with an empty list, not an error (FR-018)
- [X] T060 [US2] Add the authorization case to `backend/tests/Rundfrage.Api.IntegrationTests/ExportTests.cs`: an export requested without a creator session is refused exactly as every other admin function is (FR-017)
- [X] T061 [US2] Add the consistency case to `backend/tests/Rundfrage.Api.IntegrationTests/ExportTests.cs`: an export produced while a response is being written contains that response completely or not at all (FR-019)
- [X] T062 [US2] Create `backend/src/Rundfrage.Api/Polls/PollExport.cs` — the document and its projection, read inside one transaction, separate from `ResultsProjection` because it carries a version and a promise about its shape (plan Structure Decision, research R-9)
- [X] T063 [US2] Add `GET /polls/{pollId}/export` to `backend/src/Rundfrage.Api/Endpoints/Admin/PollAdminEndpoints.cs`, answering the neutral not-found for an unknown poll and setting a `Content-Disposition` that names the poll and the moment (FR-021a)
- [X] T064 [US2] Write `frontend/tests/unit/PollList.export.spec.ts` for the per-poll export action
- [X] T065 [US2] Add the export action to `frontend/src/components/admin/PollList.vue`, its call to `frontend/src/api/client.ts`, and `export.*` keys to `frontend/src/locales/de.json`
- [X] T066 [US2] Create `e2e/tests/export-and-backup.spec.ts`: create a poll, answer it twice, download the export in a real browser, parse it without repair (FR-016), and assert both participants appear

**Checkpoint**: US2 is independently testable and shippable.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T067 Write `backend/tests/Rundfrage.Api.IntegrationTests/ExportScaleTests.cs`: a poll at the feature 002 limits — 1000 responses across 100 days — exports as one download within 10 s (SC-011)
- [X] T068 [P] Rewrite `README.md`: one container, the mounted directory, `DATA_DIR`, backup and export, and the removal of the `/status` section and the `POSTGRES_*` table. State FR-007b plainly — whoever can read the file can read every answer
- [X] T069 [P] Add the backup instruction to `README.md` in the form the measurement supports: use the download, not `cp`, with the three-line result that shows why
- [X] T070 Measure SC-001 (`docker compose ps` shows exactly one container) and SC-002 (image size) against the system built from `docker/Dockerfile`, and record both in `specs/003-sqlite-and-export/research.md`
- [X] T071 Measure SC-003: a fresh clone of the repository starts with `docker compose up --build` and no manual step, repeating feature 001's measurement, and record it in `specs/003-sqlite-and-export/research.md`
- [X] T072 Verify `specs/003-sqlite-and-export/quickstart.md` end to end — every command in it, run as written
- [X] T073 Run `dotnet test backend/Rundfrage.slnx`, `npm run test:unit` in `frontend/`, and `npx playwright test` in `e2e/` against the real container set; report the counts
- [X] T074 Confirm no plaintext password or real hash reaches the repository, and that `.env` is untouched, before any commit

---

## Dependencies

```text
Setup (T001-T004)
   |
Foundational (T005-T024)   <-- blocks everything; build is red from T001 until T020
   |
   +--> US1 (T025-T055)    MVP
   |
   +--> US2 (T056-T066)    independent of US1 by design (spec: "could ship separately")
             |
Polish (T067-T074)
```

**Within Foundational**: T009 must be *observed failing* before T010–T016. T017 before T018.
T019 before T020. T021 after T011 (the migration is generated from the changed entities).

**Within US1**: T025–T028 are independent of the backup work and can run alongside it. T046 must
be green before T047 deletes the specs it replaces. T048 must land with T049, not after — the CI
readiness probe calls the endpoint T049 deletes.

**Within US2**: T056–T061 all touch `ExportTests.cs`, so they are sequential; T062 follows all of
them; T064 and T066 need T063.

## Parallel execution examples

**Setup**: T002, T003, T004 together (three unrelated files).

**Foundational, after T009 is observed failing**: T010, T013, T014, T015 together — unit tests,
retention, listing and results are four separate files.

**US1**: the concurrency and durability group (T025–T028) alongside the backup group
(T029–T034); the frontend group (T037–T041) alongside both.

**US2**: T064 (frontend test) alongside T062 (backend document), since neither reads the other.

## Implementation strategy

**MVP = Phase 1 + Phase 2 + US1.** That delivers the feature's reason for existing: one container,
one file, and a backup that restores. US2 adds the capability the file-based idea was *for* —
having the data in hand — and is deliberately separable.

**The riskiest tasks are T025–T030**, and they are early on purpose. If the concurrency cap or the
backup cannot be made to hold under SQLite, that is the moment to know it — before the container
set, the interface and the documentation have all been rewritten around the assumption that it
can. Phase 0 measured all three and they held, but a measurement in a spike is not the same as a
guarantee in the product.

---

## What actually happened

The list survived contact with the work, with four departures worth recording.

**The order in Phase 2 was wrong, and had to be.** T009 asked for the translation failures to be
observed before anything was changed. They could not be: swapping the provider left the solution
uncompilable (the PostgreSQL migrations reference the old driver), and once it compiled,
`MigrateAsync` failed on PostgreSQL-specific DDL before any query ran. So T020's wiring and
T021's migration deletion came first, and the runtime evidence for R-1 remains the Phase 0 spike.
What *was* observed directly, exactly as written, is T017: `SQLite Error 1: 'near "FOR": syntax
error'`.

**Three test files were converted before being deleted.** `DatabaseProbeTests`,
`LogRedactionTests` and `SchemaCreationTests` called `UseNpgsql` directly, so Phase 2 could not
compile until they were touched — twenty tasks before T050 was due to delete two of them. They
were converted minimally rather than deleted early, so the phase boundary stayed real.

**T028 came out the other way, and the result is kept.** Setting `synchronous = NORMAL` fails
`StorageSettingsTests` and leaves `DurabilityTests` green. That is not a gap in the durability
tests; it is the honest limit of what a test can see without the power actually failing. Recorded
in research.md rather than papered over.

**Two defects surfaced that no task predicted**, both from doing the thing rather than reasoning
about it:

- The backup endpoint failed intermittently with 503 under load. `BackupService` opened its
  connections directly, bypassing the interceptor, so it ran with a busy timeout of zero and was
  refused the moment it met a writer — a backup failing exactly when it is most worth taking.
  Fixed by sharing one settings path (`StorageSetup.Apply`).
- `.gitignore` gained `data/`, which on a case-insensitive filesystem also matches
  `backend/src/Rundfrage.Api/Data/`. Three new source files were invisible to git and nothing said
  so, because files already tracked stay tracked. Caught by T071 — building from a fresh checkout,
  which failed to compile. Anchored to `/data/`.

---

## Review pass

A second reading of the whole change, after everything was green. Nine things came out of it;
five were defects rather than polish.

**The admin area called a rejected form entry a storage failure.** `PollList` derived
"storage unavailable" from the poll store's `problem`, and `PollForm` used the same field for its
validation message. A poll submitted without a title therefore produced the correct validation
text *and* "your data cannot be reached" above it — two accounts of one event, one of them false
and alarming. The store now keeps `loadProblem` and `problem` apart, and a test holds them apart.

**The busy timeout was set last, and it protects the statements after it.** Setting the journal
mode needs a lock; until the timeout is in effect, a connection that meets a writer is refused
rather than made to wait. Reordered so the timeout comes first.

**A failed backup left its half-written file behind.** The endpoint reported 503 and the
temporary file stayed — a file that looks like a backup. FR-021 now holds on the failing path too.

**An unadopted write transaction leaked.** If `UseTransactionAsync` threw, the raw transaction
stayed open, holding the only write lock there is; every later submission would have waited out
the busy timeout and failed.

**A cancelled download was logged as a storage failure.** The endpoint's catch-all swallowed
`OperationCanceledException`, so a reader who closed the tab produced an error entry about a
failure that never happened.

**"Build ohne Warnungen" was false again**, and for the same reason as in feature 002: an
incremental build skips the test project. A clean build showed one nullability warning (CS8631)
in `ExportTests`. Fixed, and the check is now `dotnet test`, which builds everything.

**The constitution carried a claim the measurements had refuted** — "the file is the backup unit:
copying it copies the system's state". That is the sentence an operator would act on, and it is
false while the system runs. Amended to v2.0.1.

**Dead code and duplication**: `ApiFactory.StoragePath` was never read; `credentials.ts` was
introduced for the two new end-to-end specs while three older ones kept their own copy of the same
helper, each failing differently. One helper now, used by all five.

**Untested defensive branches**: `FileNameFor` handled a 300-character title and a title made
entirely of punctuation, and nothing exercised either. `DownloadNameTests` covers them, and
`ExportTests` now asserts the document carries *exactly* the fields the contract names — the half
a shape test usually misses, and the way a token would actually reach an export.
