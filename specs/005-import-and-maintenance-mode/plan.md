# Implementation Plan: Importing Exported Data, and a Maintenance Mode

**Branch**: `005-import-and-maintenance-mode` | **Date**: 2026-09-04 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/005-import-and-maintenance-mode/spec.md`

## Summary

Three capabilities, of very different weight: read a JSON export back in as a new poll, switch the
participant side into a maintenance notice, and replace the whole storage from a downloaded backup.

The JSON import is ordinary work — validate a document, write rows, report what was skipped. The
maintenance mode is small. **The restore is where the whole risk sits**, and Phase 0 measured it
rather than reasoning about it. Two findings shaped the design:

- A live SQLite database *can* be replaced in place, and connections already open see the new data
  immediately — so tokens come back and links work again, without any file being moved (R-1).
- A restore **fails with `database is locked` if any connection holds an open transaction** (R-2).
  Maintenance mode locks participants out, but the hourly `RetentionSweep` is not a request and is
  untouched by it. The sweep must be suspended for the duration, or the restore fails
  intermittently — depending on nothing more than what time the operator pressed the button.

The second finding is the reason this plan is not simply "call `BackupDatabase` the other way
round". It is a constraint no requirement states, and a plan that implemented only FR-024 would
look complete and be wrong a fraction of the time.

## Technical Context

**Language/Version**: C# on .NET 10, TypeScript on Node 24 — unchanged
**Primary Dependencies**: ASP.NET Core 10, EF Core 10 with `Microsoft.EntityFrameworkCore.Sqlite`
10.0.11; Vue 3.5, Vuetify 4, Pinia 4, vue-i18n 11, vue-router 5, Vite 8 — **no new dependency**
**Storage**: SQLite, one file in a mounted directory; WAL, `synchronous=FULL`, `Pooling=False`.
Maintenance state is a marker file beside it, deliberately outside the database (R-6)
**Testing**: xUnit + Mvc.Testing with a temporary directory per test class; Vitest; Playwright
**Logging**: Serilog — unchanged. FR-005 forbids imported content in the log
**Target Platform**: one Linux container, modern browsers
**Project Type**: Web application, single origin, `/api/v1`
**Performance Goals**: an import at the feature 002 limits (100 days × 1000 responses) completes
while the operator waits, as one request (SC-001a, FR-003a)
**Constraints**: a restore is atomic and preserves every token (FR-017, FR-020); the previous data
survives a failed restore (FR-021); no uploaded file outlives its import (FR-005a); maintenance
mode never reports the application unhealthy (FR-031)
**Scale/Scope**: single application instance, one operator, one file

## Constitution Check

Checked against `.specify/memory/constitution.md` (v2.0.1).

- [x] **I. Zero-Signup Participation (NON-NEGOTIABLE)**: nothing is added between a link and the
      answer form. Maintenance mode *removes* the form, but it is a deliberate operator state, not
      a step a participant must complete — there is nothing to sign up for, and nothing to pass.
      Imported polls keep the same link-as-capability model; FR-011 mints fresh link-scoped secrets
      rather than identifying anyone.
- [x] **II. Test-First Development (NON-NEGOTIABLE)**: every behaviour below is introduced by a
      failing test first. R-2 in particular gets a regression test that opens a transaction and
      asserts the restore refuses cleanly — the failure it describes is intermittent, so it must be
      pinned by a test that makes it deterministic.
- [x] **III. Simplicity & YAGNI**: no new project, layer or dependency. The restore reuses the
      mechanism `BackupService` already uses, in the other direction. FR-003a's answer — one
      request, no job store — was chosen in clarification specifically to keep a second body of
      persistent state out of the design. Maintenance state is one file, not a settings table.
- [x] **IV. Data Minimization & Operator-Controlled Storage**: no new data about anyone is
      collected. FR-005a is a *reduction*: an uploaded backup is every link in the system, and it is
      removed the moment the import ends. Retention is untouched — FR-016a hands expired polls to
      the existing sweep rather than granting anything a longer life.
- [x] **Technology Constraints**: SQLite, ASP.NET Core, Vue 3 + Vuetify + Pinia, Serilog,
      xUnit/Vitest/Playwright. Nothing added.

**Post-design re-check (after Phase 1)**: no deviation, and one simplification found on the way.
The restore needs no new storage abstraction: `StorageLocation` already centralises the path and
`StorageSetup.Apply` already puts the right settings on a directly-opened connection, which is
exactly what R-1's source and destination connections need. The only genuinely new runtime concept
is *suspending the retention sweep*, and it is one gate around an existing loop rather than a
scheduler.

Complexity Tracking is empty. No principle is deviated from.

## Project Structure

### Documentation (this feature)

```text
specs/005-import-and-maintenance-mode/
├── plan.md              # This file
├── research.md          # Phase 0 output — the measurements
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── openapi.yaml     # The four new endpoints
│   └── ui-contract.md   # Admin controls and the maintenance notice
├── checklists/
│   └── requirements.md
├── spec.md
└── tasks.md             # Created by /speckit-tasks, not here
```

### Source Code (repository root)

```text
backend/src/Rundfrage.Api/
├── Data/
│   ├── BackupService.cs           # exists — download direction
│   ├── RestoreService.cs          # NEW — upload direction (R-1, R-3, R-4)
│   ├── StorageLocation.cs         # exists — gains the marker file's path
│   └── DatabaseStartup.cs         # exists — reused after a restore (R-3)
├── Maintenance/
│   ├── MaintenanceState.cs        # NEW — the marker file, read and written (R-6)
│   └── MaintenanceMiddleware.cs   # NEW — replaces participant routes with the notice
├── Polls/
│   ├── PollExport.cs              # exists — its "there is no import" comment is corrected
│   └── PollImport.cs              # NEW — reads a v1 document, reports what it skipped
├── Retention/
│   └── RetentionService.cs        # exists — RetentionSweep gains a suspend gate (R-2)
└── Endpoints/Admin/
    ├── ImportEndpoints.cs         # NEW — JSON import, backup restore
    └── MaintenanceEndpoints.cs    # NEW — read and set maintenance state

backend/tests/Rundfrage.Api.UnitTests/
├── UploadedFileTests.cs           # NEW — FR-005a on the success, refusal and throwing paths
└── MaintenanceStateTests.cs       # NEW

backend/tests/Rundfrage.Api.IntegrationTests/
├── PollImportTests.cs             # NEW  (+ …RefusalTests, …SkipTests, …ScaleTests)
├── RestoreTests.cs                # NEW
├── RestoreLockTests.cs            # NEW — the R-2 lock regression, made deterministic
└── MaintenanceModeTests.cs        # NEW

frontend/src/
├── components/admin/ImportPanel.vue        # NEW — the project routes to components/, not pages/
├── components/admin/ImportSummary.vue      # NEW
├── components/admin/MaintenanceSwitch.vue  # NEW
├── components/admin/RestorePanel.vue       # NEW
├── components/poll/MaintenanceNotice.vue   # NEW — what a participant sees
├── locales/de.json                         # exists — gains the new strings
└── stores/maintenance.ts                   # NEW

e2e/tests/
├── import.spec.ts                 # NEW
└── maintenance.spec.ts            # NEW — participant sees the notice, not the poll
```

**Structure Decision**: the existing layout is unchanged, and the frontend follows the convention
already in `router.ts` — components under `components/admin/` and `components/poll/`, with no
`pages/` directory, because the project has never had one. Backend files slot into folders that
already exist, plus one new folder `Maintenance/` because the state, the middleware and their
tests are one concern and belong together. No new project, no new layer.

Note `frontend/tests/unit/noLiteralStrings.spec.ts`: it fails any `.vue` template carrying a
literal user-facing string, so every component listed above resolves its text through
`locales/de.json`. That guard already exists and will catch a new component that forgets.

## Phase ordering, and why it is not the story order

The user stories are prioritised P1 (JSON import), P2 (maintenance), P3 (restore). Implementation
order differs on one point: **maintenance mode must be built before the restore**, because FR-024
makes it a precondition the restore refuses without, and because R-2's sweep suspension is tested
through it. US1 stays first and stays independently shippable.

| Order | Slice | Delivers | Depends on |
|---|---|---|---|
| 1 | JSON import (US1) | FR-007 to FR-015, FR-003, FR-003a, FR-005a | nothing |
| 2 | Maintenance mode (US2) | FR-025 to FR-034 | nothing |
| 3 | Restore (US3) | FR-016 to FR-024 | slice 2 (FR-024), R-2 |

## Risks carried into implementation

- **R-2's lock is intermittent by nature.** Its test must create the condition deliberately; a suite
  that merely runs a restore will pass and prove nothing.
- **A restore replaces the migration history too** (R-3). If migrations are not re-run afterwards,
  the application runs against a schema it was not built for, and the first failing request is far
  from the cause.
- **The `PollExport` comment asserts the opposite of this feature** ("there is no import"). Left
  alone it becomes the most authoritative wrong statement in the codebase.
- **FR-005a covers the failing path**, which is the one that accumulates leftovers unnoticed —
  the same trap `BackupEndpoint` solved with `FileOptions.DeleteOnClose` in the other direction.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

The feature itself adds no project, layer or service, and every new class answers one stated
requirement. One dependency was added during review, and Principle III requires it to be argued
here rather than in a commit message.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| `vue-tsc` + `@types/node` (dev dependencies) | The frontend had **no type checking at all**. `vite build` hands sources to esbuild, which *removes* types instead of checking them, and plain `tsc` cannot read `.vue` files - where 1,586 of the frontend's 2,338 lines live. Measured during review: a template reading `summary.restored`, a field removed from its interface, built cleanly and shipped. | *Plain `tsc`*: covers only the 752 lines that are already the safest, and reports nothing about templates, props or emits between components. *Relying on the IDE*: checks whatever a developer happens to have open, and never runs in CI. *Relying on unit tests*: they caught the seeded error only because a test happened to render that branch; the same error behind an unrendered `v-if` passes 129 green tests. |
| `typescript` pinned from `^7` to `^5` | `vue-tsc` 3.3.11 patches `typescript/lib/tsc`, and TypeScript 7 removed that subpath from its `exports` map, so the two cannot be installed together today. | *Keeping TypeScript 7 and skipping the check*: leaves the gap this entry exists to close. The pin costs nothing measurable: nothing in the project imports `typescript`, neither Vite nor Vitest depends on it at runtime, and it is an optional peer of vue/pinia/vuetify. TypeScript 7 was doing nothing here except being a checker that never ran. **Revisit when Vue's language tooling supports TypeScript 7** - at that point the pin can simply be lifted. |
