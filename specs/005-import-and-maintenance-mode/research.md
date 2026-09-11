# Phase 0 Research: Importing Exported Data, and a Maintenance Mode

**Feature**: 005-import-and-maintenance-mode | **Date**: 2026-09-04

Seven questions had to be settled. Five were answered by running the thing rather than reasoning
about it, and **two of the measurements changed the design** — one of them removed a requirement
the specification had implied, and one added a constraint nobody had written down.

The crux was never the JSON import. It was this: *how do you replace a SQLite file that the
application is holding open?* Everything about User Story 3 follows from the answer.

---

## R-1: A live database can be replaced in place, and the tokens come with it

**Decision**: a restore uses SQLite's own online backup mechanism **in reverse** — the uploaded
file is the source, the live database is the destination. No file is moved, renamed or deleted.

**Measured 2026-09-04, Microsoft.Data.Sqlite 10.0.11, same connection settings as the application
(`busy_timeout=5000`, WAL, `synchronous=FULL`, `Pooling=False`):**

```text
held connection sees before : 3 rows, first='live-A'
BackupDatabase(into live)   : SUCCEEDED
held connection sees after  : 5 rows, first='backup-A'
tokens preserved            : tok-bak-1
a NEW connection sees       : 5 rows
```

The connection that was already open when the restore ran sees the restored data immediately,
without being reopened. This is what makes FR-017 achievable at all: the tokens are ordinary rows,
so a mechanism that replaces every row replaces them too, and every link in the backup works again
without anything being reissued.

**Rationale**: it is the exact mirror of `BackupService`, which feature 003 already built and
tested for the download direction. The same locking, the same busy timeout, the same guarantee that
a row is wholly present or wholly absent.

**Alternatives considered**:

- *Stop the application, swap the file, start it again.* This is the README's manual procedure. It
  works, and it stays as the fallback, but it cannot be driven from the admin area — which is what
  FR-001 asks for.
- *Delete the file and its `-wal`/`-shm` companions, then copy the upload into place.* Requires
  every connection to be closed first, and leaves a window in which the storage does not exist.
  Nothing recovers from a crash in that window.

---

## R-2: A restore fails if any connection holds an open transaction — this is the load-bearing finding

**Decision**: the retention sweep must be held off for the duration of a restore, and the restore
must report a lock conflict as a plain refusal rather than a fault.

**Measured, immediately after R-1 and under the same settings:**

```text
reader in transaction sees  : 5 rows
restore during open read tx : FAILED SqliteException: SQLite Error 5: 'database is locked'
```

The five-second busy timeout does not rescue this. A reader inside an open transaction holds its
lock for as long as the transaction lives, and the backup API needs the destination exclusively.

**Why this matters more than it looks.** The specification's answer to concurrency was
maintenance mode: FR-024 refuses a restore unless participants are already locked out. That removes
*participants* — but this application has a writer that maintenance mode does not touch, because it
is not a request at all: `RetentionSweep` wakes every hour and opens a transaction to erase expired
polls. If it wakes during a restore, the restore fails with `database is locked`, and the operator
sees a failure with no cause they can act on.

So maintenance mode is necessary and **not sufficient**. The sweep must be suspended too. That is a
design constraint discovered by measurement, not derived from the specification, and it is recorded
here because a plan that only implemented FR-024 would look complete and fail intermittently — the
worst failure mode there is, because it depends on what time the operator happened to press the
button.

**Alternatives considered**:

- *Raise the busy timeout for the restore.* Only converts a fast failure into a slow one; the
  sweep's transaction is not guaranteed to end within any particular window.
- *Retry the restore a few times.* Same objection, plus it makes an operation the operator is
  watching take an unpredictable time for reasons never explained to them.
- *Stop the sweep permanently and erase on access only.* Changes retention behaviour to solve a
  problem that lasts a few seconds a year. Rejected as disproportionate.

---

## R-3: A restore replaces the schema, so migrations must run after it

**Decision**: after the storage is replaced, migrations run against it before the application
serves again. FR-022 is satisfied by re-running the same migration path startup already uses.

**Measured** — an incoming file whose `Polls` table lacks a column the live one has:

```text
restore of older schema     : SUCCEEDED, live columns are now [Id, Title]
=> the live schema is REPLACED, not merged.
```

The restore is total. It does not merge, and it does not refuse a shape it does not recognise — it
simply adopts it, including the migration-history table the backup carried. An older backup
therefore leaves the application running against a schema it was not built for, and nothing
announces that.

**Rationale**: `DatabaseStartup.ApplyMigrationsAsync` already brings any shape forward to the
current one, reports failure instead of throwing, and is idempotent on an up-to-date database
(feature 003 proved the last of these). Reusing it costs nothing and adds no new path.

**Alternatives considered**: *refuse a backup whose migration history is not current.* This is the
"or refused with that stated as the reason" half of FR-022, and it is strictly worse: the operator's
backup is valid and the system can read it after one mechanical step it is already able to perform.

---

## R-4: The uploaded file can be verified before it touches anything

**Decision**: verification is `PRAGMA integrity_check` on the upload, opened as its own database.
It runs before the restore and touches nothing live, satisfying FR-019.

**Measured:**

```text
opening junk as a database  : REFUSED SqliteException: SQLite Error 26: 'file is not a database'
integrity_check on a backup : ok
```

A file that is not a database is refused at the moment it is opened — no parsing, no heuristics.
A file that is one is checked structurally before being used as a source.

**Rationale**: both checks read the *uploaded* file only. At the point either can fail, the live
storage has not been touched, so FR-019's "MUST leave the existing data untouched" holds by
construction rather than by careful ordering someone must maintain.

**Alternatives considered**: *checking a magic header ourselves.* Reimplements what opening the
file already does, and would accept a file that is superficially a database but internally broken —
which `integrity_check` catches and a header check cannot.

---

## R-5: The framework imposes a size limit that the specification says must not exist

**Decision**: both limits are lifted explicitly on the two import endpoints, and the decision is
commented at the site so it is not read as an oversight.

**Measured:**

```text
KestrelServerLimits.MaxRequestBodySize   : 30.000.000 bytes (28,6 MB)
FormOptions.MultipartBodyLengthLimit     : 134.217.728 bytes (128,0 MB)
```

The specification records a deliberate decision that neither upload is size-limited, with the
accepted consequence written into Assumptions. Left alone, ASP.NET Core would impose 28.6 MB
anyway — a limit nobody chose, that contradicts a decision that *was* chosen, and that would
surface as an opaque `413` on the day someone's backup grew past it.

**Rationale**: a default that quietly contradicts a recorded decision is worse than either
position taken deliberately. Lifting it makes the specification true.

**Noted for whoever revisits this**: this is also exactly where a limit would go back if the
accepted consequence ever stops being acceptable. It is one line, at one place, and the
specification's Assumptions section says what would have to change first.

---

## R-6: Where the maintenance flag lives

**Decision**: a marker file in the data directory, beside the storage file but not inside it.
Present means on; absent means off.

**Rationale**: FR-030 requires the state to survive a restore, and FR-029 requires it to survive a
restart. Those two together eliminate every other candidate:

| Candidate | Survives restart | Survives restore | Verdict |
|---|---|---|---|
| A row in the database | yes | **no** — replaced with everything else | Fails FR-030 |
| In-memory flag | **no** | yes | Fails FR-029 |
| Environment variable | yes | yes | Fails FR-033 — needs a redeploy to change |
| **File beside the storage** | yes | yes | Meets all three |

R-1 confirms the last row rather than assuming it: the restore replaces the database file and its
companions and touches nothing else in the directory, so a sibling file is outside its reach.

The file's content carries the moment maintenance was switched on, which is what lets the notice
and the admin area say more than "on". It carries nothing else — no operator identity, nothing that
would make it worth reading for any other reason.

**Alternatives considered**: *a second SQLite database for settings.* A whole storage mechanism for
one boolean, and a second thing to back up. Rejected under Principle III.

---

## R-7: The JSON import needs no new format and no new version

**Decision**: read `formatVersion` 1 as `PollExport` writes it. Nothing about the export changes.

**Rationale**: this was settled in clarification rather than research — the JSON export exists for
onward processing, so link continuity is not its job, and restoration is the backup's job instead.
That answer is what allows 003 FR-015 to stand untouched: no token is added to any file, and the
sensitive artefact remains the one that was always sensitive.

The one asymmetry worth stating: `PollExport.FormatVersion` is currently described in its own
XML comment as *"a signal, not a promise… and there is no import"*. That sentence stops being true
with this feature, and the comment must be corrected in the same change — a stale comment asserting
the opposite of the behaviour is how the next person gets it wrong.
