# Phase 1 Data Model: SQLite Storage and JSON Export

**Feature**: 003-sqlite-and-export | **Date**: 2026-09-03

No entity is added, removed or reshaped. The four tables of feature 002 carry over with the same
columns, the same relationships and the same rules. Two things change: **where the tables live**,
and **how instants are typed**.

---

## The one change to the model: instants

Every `DateTimeOffset` becomes a UTC `DateTime`.

| Table | Column | Was | Becomes |
|---|---|---|---|
| `Poll` | `CreatedAt` | `timestamptz` / `DateTimeOffset` | UTC `DateTime` |
| `Poll` | `RetentionDeadline` | `timestamptz` / `DateTimeOffset` | UTC `DateTime` |
| `PollResponse` | `SubmittedAt` | `timestamptz` / `DateTimeOffset` | UTC `DateTime` |

**Not a preference — a limitation, measured** (research.md R-1). The SQLite provider translates
neither comparisons nor `ORDER BY` on `DateTimeOffset`:

```text
Where(p => p.Deadline > cutoff)   -> InvalidOperationException, "could not be translated"
OrderBy(p => p.Deadline)          -> NotSupportedException
```

Four existing queries depend on exactly those two operations: the retention access filter, the
retention sweep, the poll listing's ordering, and the results grid's row ordering.

**Nothing is lost.** Every instant in this system was already UTC — `BerlinClock` is the only
component that knows about a time zone, and it converts on the way *out*. The stored offset was
always zero, so it carried information the domain does not have. `CandidateDay.Date` stays a
`date`: it never was an instant and is unaffected.

---

## Where the tables live

| | Was | Becomes |
|---|---|---|
| Engine | PostgreSQL server in its own container | SQLite, in-process |
| Location | a Docker volume the server owned | one file in a mounted directory |
| Reaching it | host, port, user, password | a path |
| Backup | `pg_dump` or a volume snapshot | a consistent copy the system produces (FR-003) |

The schema itself is unchanged: four tables, the same two unique token indexes, the same unique
`(PollId, Date)`, the same cascade deletes, the same absence-means-no-answer design for
`DayAnswer`.

---

## Storage settings that carry requirements

These are not tuning. Each one is a requirement made real, which is why they live in one place
(`StorageSetup`) rather than scattered through startup.

| Setting | Requirement | What it prevents |
|---|---|---|
| WAL journal mode | FR-012b | An unclean stop leaving the storage unreadable |
| `synchronous=FULL` | FR-012a | A confirmed answer vanishing in a power cut |
| Busy timeout | FR-009, FR-010 | A contending writer failing instead of waiting |
| File permissions | FR-007a | Every account on the host reading every answer |

> **On `synchronous`.** The usual advice with WAL is `NORMAL`, which acknowledges a commit before
> it is durably on disk. That trades exactly the property this feature's specification argues
> from — it is the reason an in-memory store was rejected. At a handful of writes per poll the
> stricter setting costs nothing measurable.

---

## What the schema does *not* gain

- **No table for exports.** An export is produced on demand and exists for the duration of the
  download (FR-021). Persisting it would create a second copy to keep in step with the first.
- **No table for backups.** Same reasoning.
- **No account table.** Unchanged from feature 002: the single operator is configuration, not a row.
- **No column recording who submitted from where.** Unchanged, and still forbidden — FR-042 of
  feature 002 and this feature's SC-021 equivalent both hold.

---

## The export document

Produced, never stored. Structure fixed by FR-014 to FR-020c and by `contracts/openapi.yaml`.

| Field | Notes |
|---|---|
| `formatVersion` | Integer. Additive changes stay within a version; removals or renames raise it (FR-020b). |
| `exportedAt` | UTC instant the document was produced (FR-020). |
| `poll.title`, `poll.message` | As stored. |
| `poll.days[]` | Chronological, each with its date. |
| `responses[]` | Display name and the answers actually given. |

**Two absences are deliberate and load-bearing:**

- **No token, of any kind** (FR-015). The poll's participant token grants the right to answer; a
  response's edit token grants the right to change that answer. An export carrying either would
  hand that capability to whoever receives the file — undoing feature 002's FR-029 through an
  omission rather than a decision. SC-010 verifies this by scanning a produced file against the
  tokens actually held in storage, rather than by reading the serialiser.
- **No entry for an unanswered day.** Absence means "no answer" in storage (feature 002, research
  R-8), and the export keeps that meaning rather than inventing a fourth value. An export that
  filled in a placeholder would claim something was recorded that never was.

---

## The backup artefact

A byte-for-byte usable copy of the storage, produced while the system runs and downloaded once
(FR-003, FR-003a).

**It must be self-contained.** Measured (research.md R-3): while the system holds the storage
open, a plain copy of the main file is silently short — by a few answers, or by all of them
including the schema, depending on how much has been folded back into that file at the instant of
the copy. The backup mechanism must therefore produce one complete artefact, not a file that needs
its neighbours.

```text
Situation at the moment of the copy                   Hand copy of the main file
-----------------------------------------------------------------------------------
System stopped                                        complete
A connection open, commits still in the companion     20 of 40
Fresh storage, first connection still open            UNUSABLE — "no such table"
```

Stopping the system first would make `cp` safe. It would also make backups something one avoids
doing, which is why the mechanism exists instead.

Restoring means putting the file in place and starting the system. Nothing in the application
reads a backup back in (Out of Scope).
