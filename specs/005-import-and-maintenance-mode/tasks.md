---
description: "Task list for feature 005: importing exported data, and a maintenance mode"
---

# Tasks: Importing Exported Data, and a Maintenance Mode

**Input**: Design documents from `/specs/005-import-and-maintenance-mode/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: MANDATORY (Constitution Principle II). Every behaviour task is preceded by the test that
defines it, and that test MUST be observed failing for the intended reason before implementation.

**Organization**: grouped by user story. US1 and US2 are independent of each other. **US3 depends on
US2** — FR-024 refuses a restore unless maintenance mode is on, so there is nothing to test without
it. This is the one cross-story dependency and it is deliberate.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable — different file, no dependency on an incomplete task
- **[Story]**: US1, US2, US3

## Path Conventions

Web application per plan.md: `backend/src/Rundfrage.Api/`, `backend/tests/`, `frontend/src/`, `e2e/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: a known-good starting point and the fixtures every story's tests need.

- [X] T001 Run the full suite and record the baseline (expected: 95 unit + 112 integration green) so any later red is attributable to this feature, not inherited
- [X] T002 [P] Add `ExportDocumentBuilder` test helper producing a valid formatVersion 1 document with adjustable defects, in `backend/tests/Rundfrage.Api.IntegrationTests/ExportDocumentBuilder.cs`
- [X] T003 [P] Add `BackupFileFixture` test helper producing a real backup via the existing `BackupService`, in `backend/tests/Rundfrage.Api.IntegrationTests/BackupFileFixture.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: receiving an upload safely. Blocks **US1 and US3**; US2 needs none of it and may start
in parallel with this phase.

**⚠️ CRITICAL**: FR-005a is the requirement most easily satisfied on the happy path and missed on
the failing one — the path where leftovers accumulate unnoticed. The test comes first for that reason.

- [X] T004 [P] Write failing unit test: a received upload is deleted when the operation succeeds, when it is refused, and when it throws — in `backend/tests/Rundfrage.Api.UnitTests/UploadedFileTests.cs` (FR-005a, SC-007a)
- [X] T005 Implement `UploadedFile` — streams a request body to a temp file, deletes on dispose including the exception path — in `backend/src/Rundfrage.Api/Http/UploadedFile.cs` (FR-005a)
- [X] T006 [P] Write failing integration test: a body larger than Kestrel's 28.6 MB default is accepted on the import routes, in `backend/tests/Rundfrage.Api.IntegrationTests/UploadSizeTests.cs` (research R-5)
- [X] T007 Lift `MaxRequestBodySize` and `MultipartBodyLengthLimit` on the import routes only, with a comment naming the spec's recorded decision so it is not read as an oversight, in `backend/src/Rundfrage.Api/Program.cs` (research R-5)
- [X] T008 [P] Add the shared error codes (`not_a_valid_export`, `format_version_too_new`, `poll_too_large`, `not_a_backup`, `maintenance_required`, `storage_locked`, `confirmation_required`) as constants in `backend/src/Rundfrage.Api/Http/ErrorCodes.cs` (FR-006)

**Checkpoint**: uploads can be received and are guaranteed not to outlive their request.

---

## Phase 3: User Story 1 — Import an exported poll (Priority: P1) 🎯 MVP

**Goal**: a JSON export becomes a new poll with a new participant link, and a summary names
everything that was not taken and why.

**Independent Test**: export a poll, delete it, import the file, compare title, message, days,
names and per-day answers field by field. Needs neither maintenance mode nor restore.

### Tests for User Story 1 ⚠️ Write first, observe failing

- [X] T009 [P] [US1] Contract test: `POST /api/v1/admin/polls/import` returns 200 with the `ImportSummary` shape, and 401 without a session, in `backend/tests/Rundfrage.Api.IntegrationTests/PollImportTests.cs` (FR-002)
- [X] T010 [P] [US1] Round-trip test: export → delete → import reproduces title, message, days, display names and per-day answers field by field, in `PollImportTests.cs` (FR-009, SC-002)
- [X] T011 [P] [US1] Test: the imported poll's participant link differs from the original's, and each response gets a fresh personal link, in `PollImportTests.cs` (FR-011)
- [X] T012 [P] [US1] Test: a participant can answer an imported poll through its new link and sees the imported answers alongside their own, in `PollImportTests.cs` (US1 scenario 3)
- [X] T013 [P] [US1] Test: a day a response did not answer has no entry after import — no placeholder is invented, in `PollImportTests.cs` (FR-010)
- [X] T014 [P] [US1] Test: importing the same file twice yields two independent polls, neither damaged, in `PollImportTests.cs` (US1 scenario 6)
- [X] T015 [P] [US1] Test: the imported poll receives a retention deadline by the ordinary rule and it appears in the poll list, in `PollImportTests.cs` (FR-015)
- [X] T016 [P] [US1] Refusal tests: not JSON, not this document, `formatVersion` 2, title/message/day-count over limit, response count over limit — each creates nothing and names its code, in `PollImportRefusalTests.cs` (FR-006, FR-007, FR-013, FR-014, SC-004)
- [X] T017 [P] [US1] Skip tests: expired poll, answer naming an unknown day, unrecognised availability, over-long display name, response answering one day twice — each is skipped and reported, the rest imports, in `PollImportSkipTests.cs` (FR-012, SC-003)
- [X] T018 [P] [US1] Test: a file whose only poll is expired returns 200 with `imported: false` and a populated `skipped` — not an error, in `PollImportSkipTests.cs` (FR-004, SC-003)
- [X] T019 [P] [US1] Test: no field of an imported file reaches the log, while the fact of the import does, in `backend/tests/Rundfrage.Api.IntegrationTests/ImportLoggingTests.cs` (FR-005)
- [X] T020 [P] [US1] Scale test: a document at the documented maximum — 100 candidate days and 1000 responses — imports as one request while the operator waits, in `backend/tests/Rundfrage.Api.IntegrationTests/PollImportScaleTests.cs` (SC-001a, FR-003a). Follows the precedent of 003's `ExportScaleTests.cs`
- [X] T021 [P] [US1] Frontend test: the summary renders skip reasons from codes, hides the skipped list when empty, and shows „Nichts übernommen" in the neutral register, in `frontend/tests/unit/ImportSummary.spec.ts` (ui-contract §4)

### Implementation for User Story 1

- [X] T022 [US1] Implement the import document shape and its reader in `backend/src/Rundfrage.Api/Polls/PollImport.cs` — parse, version-check, classify each defect as refuse or skip per data-model.md §2
- [X] T023 [US1] Implement `ImportSummary` and `SkippedItem` result shapes in `PollImport.cs`, matching `contracts/openapi.yaml`. The summary is the request's return value and is neither stored nor retrievable afterwards — no history, no job record (FR-003, FR-003a)
- [X] T024 [US1] Implement poll creation from an accepted document — fresh tokens, `CreatedAt`/`SubmittedAt` at the import instant, retention by the ordinary rule — in `PollImport.cs` (FR-008, FR-011, FR-015)
- [X] T025 [US1] Wrap creation in one transaction so a poll is created whole or not at all, in `PollImport.cs` (FR-013, SC-004)
- [X] T026 [US1] Add `POST /polls/import` to `backend/src/Rundfrage.Api/Endpoints/Admin/ImportEndpoints.cs` and register it on the admin group in `Program.cs` (FR-001, FR-002)
- [X] T027 [US1] Correct the `FormatVersion` XML comment in `backend/src/Rundfrage.Api/Polls/PollExport.cs` — it currently ends "and there is no import", which this task makes false (research R-7)
- [X] T028 [P] [US1] Add the import control and file picker to the admin area in `frontend/src/components/admin/ImportPanel.vue` (ui-contract §3a)
- [X] T029 [P] [US1] Implement the summary component — outcome line, copyable new link with the note that old links do not reach it, skip list as a `<ul>`, focus on appearance — in `frontend/src/components/admin/ImportSummary.vue`. It must not offer to reopen a past summary, because none is kept (FR-007, FR-003a, SC-009, ui-contract §4)
- [X] T030 [US1] Add German strings for every error and skip code to `frontend/src/locales/de.json`. `frontend/tests/unit/noLiteralStrings.spec.ts` already fails any `.vue` template carrying a literal user-facing string, so every new component must resolve its text through i18n (FR-006)
- [X] T031 [US1] Verify T009–T021 now pass and the baseline from T001 is still green

**Checkpoint**: US1 complete and shippable on its own. This is the MVP.

---

## Phase 4: User Story 2 — Maintenance mode (Priority: P2)

**Goal**: participants see a notice instead of any poll; the operator keeps working and can switch
it back off.

**Independent Test**: switch on, open a participant link with no session, confirm the notice
replaces the poll, switch off, confirm the poll returns unchanged.

**Note**: independent of US1. May be built in parallel with Phases 2–3.

### Tests for User Story 2 ⚠️ Write first, observe failing

- [X] T032 [P] [US2] Unit tests for `MaintenanceState`: absent marker means off, present means on, switching on twice and off twice both succeed, in `backend/tests/Rundfrage.Api.UnitTests/MaintenanceStateTests.cs` (FR-025, edge cases)
- [X] T033 [P] [US2] Unit test: an empty or unreadable marker file still means **on** — the safe direction — rather than throwing or defaulting to off, in `MaintenanceStateTests.cs` (data-model §1)
- [X] T034 [P] [US2] Test: maintenance state survives a restart of the host, in `backend/tests/Rundfrage.Api.IntegrationTests/MaintenanceModeTests.cs` (FR-029, SC-010)
- [X] T035 [P] [US2] Test: with maintenance on, a poll link, a personal link and the results route each return the notice and disclose no title, message, day, name or result, in `MaintenanceModeTests.cs` (FR-026)
- [X] T036 [P] [US2] Test: the response is byte-identical for a real poll token and an unknown one — maintenance must not become an existence oracle, in `MaintenanceModeTests.cs` (FR-032, 002 SC-012)
- [X] T037 [P] [US2] Test: with maintenance on, a submission and a revision are both refused and nothing is recorded, in `MaintenanceModeTests.cs` (FR-027)
- [X] T038 [P] [US2] Test: with maintenance on, sign-in and every admin route still work, including switching it back off, in `MaintenanceModeTests.cs` (FR-028)
- [X] T039 [P] [US2] Test: `GET /api/v1/health` answers 200 while maintenance is on, in `MaintenanceModeTests.cs` (FR-031)
- [X] T040 [P] [US2] Test: switching maintenance on and off alters no poll or response, in `MaintenanceModeTests.cs` (FR-034)
- [X] T041 [P] [US2] Contract test for `GET`/`PUT /api/v1/admin/maintenance`, including 401 without a session, in `MaintenanceEndpointTests.cs` (contracts/openapi.yaml)
- [X] T042 [P] [US2] Frontend test: the switch shows current state, confirms when switching **on** and not when switching off, and the banner is persistent rather than a toast, in `frontend/tests/unit/MaintenanceSwitch.spec.ts` (ui-contract §2)

### Implementation for User Story 2

- [X] T043 [US2] Implement `MaintenanceState` over a marker file beside the storage, with the file's presence as the state and its content as the switch-on instant, in `backend/src/Rundfrage.Api/Maintenance/MaintenanceState.cs` (FR-030, research R-6)
- [X] T044 [US2] Add the marker file's path to `backend/src/Rundfrage.Api/Data/StorageLocation.cs` so the directory layout stays defined in one place
- [X] T045 [US2] Implement `MaintenanceMiddleware` with the three-way split from data-model.md §5 — admin through, health through, everything else the notice — in `backend/src/Rundfrage.Api/Maintenance/MaintenanceMiddleware.cs` (FR-026, FR-027, FR-028, FR-031)
- [X] T046 [US2] Register the middleware in `Program.cs` after the forwarded-headers step and before routing, so one gate covers routes added later (FR-033)
- [X] T047 [US2] Add `GET`/`PUT /admin/maintenance` in `backend/src/Rundfrage.Api/Endpoints/Admin/MaintenanceEndpoints.cs` and register on the admin group (FR-025)
- [X] T048 [P] [US2] Implement the participant notice — `<h1>Wartung</h1>`, `role="status"`, `<main>` landmark, no retry control, no poll content — in `frontend/src/components/poll/MaintenanceNotice.vue` (FR-032, ui-contract §1)
- [X] T049 [P] [US2] Add the admin switch, the „seit <Zeitpunkt>" line and the persistent banner in `frontend/src/components/admin/MaintenanceSwitch.vue` and `frontend/src/stores/maintenance.ts` (ui-contract §2)
- [X] T050 [US2] Verify T032–T042 pass and the baseline is still green

**Checkpoint**: US1 and US2 both work independently.

---

## Phase 5: User Story 3 — Restore from a backup (Priority: P3)

**Goal**: every poll, response and **link** returns as it was when the backup was taken.

**Independent Test**: take a backup, create another poll, restore, confirm the system matches the
backup — including that a link issued before it still works and the newer poll is gone.

**⚠️ Depends on US2** (FR-024). **⚠️ Read research.md R-2 before starting.**

### Tests for User Story 3 ⚠️ Write first, observe failing

- [X] T051 [US3] **R-2 regression test** — open a transaction on the storage, then attempt a restore, and assert it refuses with `storage_locked` rather than throwing or hanging. Makes an intermittent failure deterministic, in `backend/tests/Rundfrage.Api.IntegrationTests/RestoreLockTests.cs` (research R-2)
- [X] T052 [US3] Test: the retention sweep is suspended for the duration of a restore and resumes afterwards — a sweep waking mid-restore must not be able to fail it, in `RestoreLockTests.cs` (research R-2)
- [X] T053 [P] [US3] Test: a restore is refused with `maintenance_required` while maintenance is off, and nothing is replaced, in `backend/tests/Rundfrage.Api.IntegrationTests/RestoreTests.cs` (FR-024)
- [X] T054 [P] [US3] Test: after a restore, a participant link captured before the backup was taken reaches its poll again, and a personal link still revises its own response, in `RestoreTests.cs` (FR-017, SC-006)
- [X] T055 [P] [US3] Test: polls created after the backup was taken are gone, and the count matches what the preview announced, in `RestoreTests.cs` (FR-016, FR-018)
- [X] T056 [P] [US3] Test: a file that is not a backup is refused with `not_a_backup` and the existing data is untouched, in `RestoreTests.cs` (FR-019, research R-4)
- [X] T057 [P] [US3] Test: a backup carrying an older schema is brought forward by migrations after the swap, and the application can read it, in `RestoreTests.cs` (FR-022, research R-3)
- [X] T058 [P] [US3] Test: a restore that fails after the swap leaves the previous data recoverable — the safety copy is what makes this true, in `RestoreTests.cs` (FR-020, FR-021, SC-007)
- [X] T059 [P] [US3] Test: a restore does not change maintenance state, in either direction, in `RestoreTests.cs` (FR-023, FR-030, SC-010)
- [X] T060 [P] [US3] Test: polls in the backup already past retention are restored, named in the summary, and removed by the next sweep, in `RestoreTests.cs` (FR-016a)
- [X] T061 [P] [US3] Contract tests for `POST /admin/restore/preview` and `POST /admin/restore`, including 400 without `confirm` and 409 for both refusal reasons, in `RestoreEndpointTests.cs` (contracts/openapi.yaml)
- [X] T062 [P] [US3] Test: the uploaded backup is deleted after preview and after restore, on every path, in `RestoreTests.cs` (FR-005a, SC-007a)
- [X] T063 [P] [US3] Frontend test: the restore control is disabled while maintenance is off and says why; confirmation names the loss rather than being a bare OK, in `frontend/tests/unit/RestorePanel.spec.ts` (SC-005, ui-contract §3b)

### Implementation for User Story 3

- [X] T064 [US3] Add a suspend gate to `RetentionSweep` so a restore can hold it off and release it, in `backend/src/Rundfrage.Api/Retention/RetentionService.cs` (research R-2)
- [X] T065 [US3] Implement `RestoreService.VerifyAsync` — open the upload as its own database and run `PRAGMA integrity_check`, touching nothing live — in `backend/src/Rundfrage.Api/Data/RestoreService.cs` (FR-019, research R-4)
- [X] T066 [US3] Implement `RestoreService.PreviewAsync` returning the counts and losses the operator will be asked to confirm, in `RestoreService.cs` (FR-018)
- [X] T067 [US3] Implement `RestoreService.RestoreAsync` following data-model.md §5 exactly: refuse → verify → suspend sweep → safety copy → `BackupDatabase(upload → live)` → migrations → resume → delete upload (FR-020, FR-021, FR-022, research R-1)
- [X] T068 [US3] Map a `SqliteException` for a locked database onto the `storage_locked` refusal rather than a 500, in `RestoreService.cs` (research R-2)
- [X] T069 [US3] Add `POST /admin/restore/preview` and `POST /admin/restore` to `Endpoints/Admin/ImportEndpoints.cs`, both refusing unless maintenance is on (FR-024)
- [X] T070 [P] [US3] Implement the two-step restore panel — upload, preview with the loss named, explicit confirmation — in `frontend/src/components/admin/RestorePanel.vue`, visually separated from the JSON import (FR-001, SC-005, ui-contract §3b)
- [X] T071 [US3] Verify T051–T063 pass and the baseline is still green

**Checkpoint**: all three stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T072 [P] End-to-end test: operator imports a JSON export and shares the new link; a participant answers through it, in `e2e/tests/import.spec.ts` (SC-001)
- [X] T073 [P] End-to-end test: maintenance on → participant sees the notice, not the poll; off → the poll returns unchanged, in `e2e/tests/maintenance.spec.ts` (SC-008, constitution gate 3: the participant path is verified end to end)
- [X] T074 **Correct `README.md`**: the sentence "Ein Import existiert nicht." appears twice — under *Exportieren* and under *Sichern und Wiederherstellen* — and both become false with this feature
- [X] T075 [P] Add sections for importing and for maintenance mode to `README.md`, stating that a JSON import always mints new links and that a restore requires maintenance mode
- [X] T076 [P] Update `specs/005-import-and-maintenance-mode/quickstart.md` if anything measured during implementation contradicts research.md
- [X] T077 Verify every error and skip code in `contracts/openapi.yaml` has a German string and is reachable by a test
- [X] T078 Run the full suite — unit, integration, end-to-end — and confirm green (constitution gate 2)
- [X] T079 Confirm the healthcheck reports healthy throughout a maintenance window and a restore (FR-031, SC-011)
- [ ] T080 Walk `quickstart.md` end to end as written, on a clean checkout — **not done: needs a running Docker daemon, which was unavailable in this environment**

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: no dependencies
- **Phase 2 Foundational**: after Setup. Blocks **US1 and US3**. Does **not** block US2
- **Phase 3 US1**: after Phase 2
- **Phase 4 US2**: after Phase 1 — may run in parallel with Phases 2 and 3
- **Phase 5 US3**: after Phase 2 **and** Phase 4
- **Phase 6 Polish**: after the stories being shipped are complete

### User Story Dependencies

- **US1 (P1)**: independent. Ships alone as the MVP
- **US2 (P2)**: independent of US1. Ships alone
- **US3 (P3)**: **requires US2**. FR-024 refuses a restore without maintenance mode, so there is no
  version of US3 that is testable without it

### Within Each Story

Tests written and failing → services → endpoints → frontend → verification. No implementation task
starts before the test that defines it has been seen to fail.

### Parallel Opportunities

- T002, T003 together
- T004 and T006 together; T008 alongside either
- Almost all of Phase 3's tests (T009–T021) — separate files or separate cases
- Almost all of Phase 4's tests (T032–T042)
- Phase 5's tests except T051 and T052, which share `RestoreLockTests.cs` and must be written in sequence
- Two people could take US1 and US2 simultaneously after Phase 1

---

## Parallel Example: User Story 1

```bash
# Tests first, all at once:
Task: "Round-trip test in PollImportTests.cs"
Task: "Refusal tests in PollImportRefusalTests.cs"
Task: "Skip tests in PollImportSkipTests.cs"
Task: "Logging test in ImportLoggingTests.cs"
Task: "Summary rendering test in frontend/tests/unit/ImportSummary.spec.ts"

# Then implementation, where the frontend pair is parallel:
Task: "Import control in frontend/src/components/admin/ImportPanel.vue"
Task: "Summary component in frontend/src/components/admin/ImportSummary.vue"
```

---

## Implementation Strategy

### MVP (User Story 1 only)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **stop and validate** → ship.
   A working JSON import that cannot damage anything existing.

### Incremental delivery

1. Setup + Foundational
2. **US1** → validate → ship (MVP)
3. **US2** → validate → ship — useful on its own, for any occasion the data must hold still
4. **US3** → validate → ship — the heaviest and riskiest slice, last, on top of US2

Stopping after US2 is a coherent release. The manual restore route in the README stays valid until
US3 lands, so nothing is left broken by stopping there.

---

## Notes

- **T051 is the task most likely to be skipped and least safe to skip.** Its failure is
  intermittent and depends on what time of day the operator acts; a suite that merely runs a
  restore passes and proves nothing (research R-2)
- **T074 is not cosmetic.** A README asserting "Ein Import existiert nicht." next to a working
  import is the most authoritative wrong statement the project would contain
- FR-005a's failing path (T004) is the one that accumulates leftovers unnoticed — the same trap
  `BackupEndpoint` already solved in the other direction with `FileOptions.DeleteOnClose`
- Commit after each task or logical group; stop at any checkpoint to validate a story alone
