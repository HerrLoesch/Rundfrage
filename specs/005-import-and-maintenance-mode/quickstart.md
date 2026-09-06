# Quickstart: Importing Exported Data, and a Maintenance Mode

**Feature**: 005-import-and-maintenance-mode | **Date**: 2026-09-04

Read `research.md` before touching the restore. Two of its measurements are the reason the design
looks the way it does, and both describe failures that do not reproduce reliably.

## Running it

Unchanged from feature 003:

```bash
docker compose up --build          # http://localhost:8080
./start-services.sh                # backend in the container, frontend on Vite
```

Backend tests need no Docker daemon — each test class gets its own temporary directory.

```bash
dotnet test backend/Rundfrage.slnx
cd frontend && npx vitest run
cd e2e && npx playwright test        # needs the system running
```

## The three slices, in build order

Note that this is **not** the user-story order. Maintenance mode comes before the restore because
FR-024 makes it a precondition the restore refuses without.

1. **JSON import** — independent, additive, cannot damage existing data. Start here.
2. **Maintenance mode** — independent. Marker file, middleware, admin switch.
3. **Restore** — depends on 2. This is where the risk is.

## The two things that will bite you

### A restore fails if anything holds a transaction

Measured, not theorised (research R-2):

```text
restore during open read tx : FAILED SqliteException: SQLite Error 5: 'database is locked'
```

The five-second busy timeout does not save it. Maintenance mode locks participants out, but
**`RetentionSweep` is not a request** — it wakes hourly and opens a transaction regardless of
maintenance mode. Suspend it around the restore.

This is the failure that will pass every test you write casually. A test that just calls restore
succeeds; the bug appears in production, once, at a time nobody can connect to a cause. Write the
test that opens a transaction deliberately.

### A restore replaces the schema, including the migration history

Also measured (research R-3): restoring an older backup leaves the live database with the older
shape. Run `DatabaseStartup.ApplyMigrationsAsync` after the swap — it is idempotent on an
up-to-date database, so there is no branch to write.

### An upload has three files, not one

Found during implementation, not during Phase 0, and it is the kind of thing that passes review.

`UploadedFile` deleted the file it received. Verifying a backup *opens* that file as a database,
which leaves `-wal` and `-shm` beside it — and those carry the same data as the file itself. So
FR-005a was satisfied on paper and broken in fact: every participant link and every personal link
in the system stayed on disk while the code reported that nothing was kept.

An integration test counting temporary files caught it (`RestoreTests`), and the unit-level
regression lives in `UploadedFileTests.Deletes_the_journal_companions_as_well`. Feature 003 had
already met the same asymmetry from the other side — `StorageSetup.SecureFile` tightens permissions
on all three files for exactly this reason. Worth remembering the next time anything in this system
treats a SQLite file as one file.

## Where things go

| Concern | File |
|---|---|
| Restore mechanism | `backend/src/Rundfrage.Api/Data/RestoreService.cs` (new) |
| Its mirror, already built | `Data/BackupService.cs` — read it first; the restore is it, reversed |
| JSON reading | `Polls/PollImport.cs` (new), beside `Polls/PollExport.cs` |
| Maintenance state | `Maintenance/MaintenanceState.cs` (new) — the marker file |
| Maintenance enforcement | `Maintenance/MaintenanceMiddleware.cs` (new) |
| Sweep suspension | `Retention/RetentionService.cs` — gate the existing loop |
| Endpoints | `Endpoints/Admin/ImportEndpoints.cs`, `Endpoints/Admin/MaintenanceEndpoints.cs` |

## Ordering that carries the guarantees

From `data-model.md` §5. The order is what makes FR-019 and FR-021 true by construction rather than
by care — steps 1 to 4 cannot lose anything, because the live storage is not written until step 6:

```text
1. refuse unless maintenance is on        4. suspend the retention sweep
2. receive the upload                     5. safety copy of current storage
3. open it; PRAGMA integrity_check        6. BackupDatabase(upload -> live)
                                          7. migrations   8. resume sweep; delete upload
```

## Things that are decisions, not oversights

Each of these looks like something to fix. Each was chosen, and the reasoning is in the spec:

- **No upload size limit**, on either path. Kestrel's 28.6 MB default is lifted deliberately
  (research R-5). The accepted consequence is in the spec's Assumptions. Do not add a limit back
  without changing that section first.
- **The summary is not stored.** One request, no job store, no history (FR-003a).
- **`imported: false` is a success response.** A file holding one expired poll returns 200 with an
  empty result and a populated `skipped` (FR-004).
- **A JSON import always mints new links.** The export carries no tokens by design (003 FR-015), and
  link continuity is the backup's job, not the JSON's.

## One correction to make on the way

`PollExport.FormatVersion`'s XML comment currently ends *"and there is no import"*. That stops
being true with this feature. Fix it in the same change — left alone it is the most authoritative
wrong statement in the codebase.
