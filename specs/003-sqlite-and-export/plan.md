# Implementation Plan: SQLite Storage and JSON Export

**Branch**: `dev` (feature directory `003-sqlite-and-export`) | **Date**: 2026-09-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-sqlite-and-export/spec.md`

## Summary

Replace the PostgreSQL server with a single SQLite file on a mounted volume, reducing the running
system from two containers to one; add a JSON export and a consistent backup the creator can
download; and retire feature 001's walking skeleton, which has done its job.

The work divides unevenly. Swapping the storage provider is mechanical. What is not mechanical,
and where the whole risk sits, is that **the storage layer carries four guarantees that the tests
assert and the provider does not automatically preserve**: the exact response cap under
concurrency, durability of a confirmed answer, a backup that actually restores, and retention
filtering by deadline. Phase 0 measured all four rather than assuming them — one measurement
changed the data model and one proved the specification had been too optimistic about backups.

## Technical Context

**Language/Version**: C# on .NET 10, TypeScript on Node 24 — unchanged
**Primary Dependencies**: ASP.NET Core 10, EF Core 10 with `Microsoft.EntityFrameworkCore.Sqlite`
10.0.11 (replacing `Npgsql.EntityFrameworkCore.PostgreSQL`); Vue 3.5, Vuetify 4, Pinia 4,
vue-i18n 11, vue-router 5, Vite 8 — unchanged
**Storage**: SQLite, one file in a mounted directory. WAL journal mode, `synchronous=FULL`
**Testing**: xUnit + Mvc.Testing with a temporary file per test class (`Testcontainers.PostgreSql`
removed); Vitest; Playwright
**Logging**: Serilog — unchanged
**Target Platform**: one Linux container, modern browsers
**Project Type**: Web application, single origin, `/api/v1`
**Performance Goals**: export at the feature 002 limits within 10 s (SC-011); no regression on the
5 s page budgets inherited from 001
**Constraints**: exact response cap under concurrency (FR-010); a confirmed answer survives a
power cut (FR-012a); a downloaded backup restores completely (FR-003); no token in an export
(FR-015)
**Scale/Scope**: single application instance, one file, the feature 002 limits unchanged

## Constitution Check

Checked against `.specify/memory/constitution.md` (v2.0.0).

- [x] **I. Zero-Signup Participation (NON-NEGOTIABLE)**: nothing about the participant path
      changes. No account, no login, no identification is added or implied. The export and the
      backup are creator-only functions behind the existing session requirement.
- [x] **II. Test-First Development (NON-NEGOTIABLE)**: every behaviour below is introduced by a
      failing test first. FR-029 sharpens this: the concurrency guarantees must be re-proven
      against the new storage, not inherited from a suite that passed against the old one.
- [x] **III. Simplicity & YAGNI**: this feature **removes** more than it adds — a container, a
      dependency (`Testcontainers.PostgreSql`), two endpoints, a page, a store and a probe. The
      two additions, export and backup, are each one endpoint answering a stated requirement.
      Encryption at rest was considered and rejected rather than deferred (FR-007c).
- [x] **IV. Data Minimization & Operator-Controlled Storage**: the data moves from a server the
      operator runs to a file the operator holds — further towards operator control, not away.
      Exports carry no tokens (FR-015). The at-rest exposure is unchanged in kind but easier to
      reach, and FR-007a–c address that explicitly rather than leaving it unrecorded.
- [x] **Technology Constraints**: SQLite on a mounted volume, as amended in v2.0.0. One
      dependency swapped, one removed, none added.

**Post-design re-check (after Phase 1)**: no deviation. The design introduces no architectural
layer; `RetentionService`, `PollService` and `ResultsProjection` keep their shapes, and the two new
endpoints call one new class each. Notably this feature ends with **fewer** Complexity Tracking
entries than feature 002 began with, because the Testcontainers justification disappears with the
dependency.

## Project Structure

### Documentation (this feature)

```text
specs/003-sqlite-and-export/
|-- plan.md              # This file
|-- spec.md              # 43 requirements, 19 success criteria
|-- research.md          # Phase 0 - 10 decisions, four of them measured
|-- data-model.md        # Phase 1
|-- quickstart.md        # Phase 1
|-- contracts/
|   `-- openapi.yaml     # Phase 1 - two endpoints added, two removed
`-- checklists/
    `-- requirements.md
```

### Source Code (repository root)

```text
backend/src/Rundfrage.Api/
|-- Data/
|   |-- RundfrageDbContext.cs        # DateTimeOffset -> DateTime (research R-1)
|   |-- Entities/                    # same four entities, UTC instants
|   |-- Migrations/                  # DELETED and regenerated once for SQLite (R-10)
|   `-- StorageSetup.cs              # NEW - WAL, synchronous=FULL, busy timeout, permissions
|-- Diagnostics/                     # DELETED - DatabaseProbe, ConnectivityStatus (R-5)
|-- Endpoints/
|   |-- MessageEndpoint.cs           # DELETED (R-5)
|   |-- StatusEndpoint.cs            # DELETED (R-5)
|   |-- Admin/
|   |   |-- PollAdminEndpoints.cs    # gains GET .../export
|   |   |-- BackupEndpoint.cs        # NEW - consistent snapshot download (FR-003)
|   |   `-- SignInEndpoints.cs       # unchanged
|   `-- Public/                      # unchanged
|-- Polls/
|   |-- PollExport.cs                # NEW - the export document (R-9)
|   |-- ResponseService.cs           # FOR UPDATE -> write transaction (R-2)
|   |-- PollService.cs               # ordering by UTC instant (R-1)
|   `-- ResultsProjection.cs         # ordering by UTC instant (R-1)
|-- Retention/RetentionService.cs    # deadline filter by UTC instant (R-1)
`-- Time/BerlinClock.cs              # gains a UTC instant accessor; zone logic unchanged

frontend/src/
|-- components/
|   |-- SystemStatus.vue             # DELETED (R-5)
|   `-- admin/PollList.vue           # gains export and backup actions
|-- stores/status.ts                 # DELETED (R-5)
`-- router.ts                        # /status route removed

e2e/tests/
|-- walking-skeleton.spec.ts         # DELETED - assertions re-pointed, not dropped (FR-024b)
|-- database-status.spec.ts          # DELETED - likewise
|-- storage-resilience.spec.ts       # NEW - inherits what those two were really proving
`-- export-and-backup.spec.ts        # NEW

compose.yaml                          # one service; db, its healthcheck and volume removed
```

**Structure Decision**: the shape of the application does not change. Storage is swapped behind
`RundfrageDbContext`, which is what an ORM boundary is for, and the two new capabilities are
endpoints beside the existing admin ones.

Two groupings are worth naming. **`StorageSetup` is a new single-purpose class** because three
settings — journal mode, synchronous level, busy timeout — must be applied consistently to every
connection and each of them carries a guarantee from the specification. Scattering them across
`Program.cs` is how one of them ends up missing from the test configuration and a guarantee
quietly holds only in production. **`PollExport` is separate from `ResultsProjection`** although
both read a poll: the projection serves a live grid and may change with the interface, while the
export carries a version and a promise about its shape (FR-020a–c). Merging them would couple a
versioned document to a view.

## Requirement Coverage

All 43 requirements and 19 success criteria are accounted for. Most are cited inline above or in
the design artifacts; the rest are grouped here so the tasks phase has an explicit home for each.

**Carried by the storage setup**: FR-002, FR-004, FR-005, FR-006, FR-007, FR-007a, FR-012a,
FR-012b — journal mode, synchronous level, file location, permissions, automatic creation on an
empty directory, and a schema that is safe to apply again over existing storage. SC-004 (a stop
and start preserves everything) is the same guarantee seen from outside, and is the cheapest of
them to verify.

**Carried unchanged from feature 002**: FR-008, FR-011, FR-012. These are not re-specified; they
are re-verified. The task list re-runs the existing suites, which is the point of FR-028.

**Verification obligations rather than designs**: FR-027 (test-first) constrains how every other
task is executed; FR-028 and SC-008 require the existing suites to pass with no assertion
weakened; FR-029, SC-006, SC-006a, SC-006b, SC-007 require the concurrency and durability
guarantees re-proven against SQLite; FR-030 and SC-013 require no orphaned code after the removal;
FR-024b and SC-014 require the removed suites' assertions to be re-pointed at the product.

**Measured criteria**: SC-001, SC-002 (container count, image size) are checked once at the end;
SC-003 (fresh clone) repeats feature 001's measurement; SC-005, SC-005a (backup and restore),
SC-009, SC-009a (export shape), SC-010 (no tokens), SC-011 (export at the limits), SC-011a (file
permissions), SC-012 (unreachable storage) each become a named verification task.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *(none)* | — | — |

This feature introduces no deviation from the constitution. It is worth recording *why* that is
unusual: the change removes a dependency (`Testcontainers.PostgreSql`, whose justification
disappears with the database server — research R-6), a container, two endpoints, a page and a
store, while adding two endpoints that each answer a requirement directly. The i18n deviation
inherited from feature 002 remains recorded there and is untouched here.

**One decision that could have become a deviation and did not**: encryption at rest. It was
considered during clarification and rejected on the merits — a key stored beside the data in the
same configuration protects against nobody who can reach the data, while adding a way to lose
everything by losing a key (FR-007c). Recording it as rejected rather than deferred keeps it from
resurfacing as an unexamined "should we?".
