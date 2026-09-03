# Phase 0 Research: SQLite Storage and JSON Export

**Feature**: 003-sqlite-and-export | **Date**: 2026-09-03

Ten questions had to be settled. Four were answered by running the thing rather than reasoning
about it, and two of those changed the design — one of them substantially. The measurements are
reproduced below because their conclusions are not obvious and would otherwise read as opinion.

---

## R-1: `DateTimeOffset` does not work on SQLite

**Decision**: every stored instant becomes a UTC `DateTime`. `DateTimeOffset` disappears from the
persisted model.

**This is not a preference. Measured 2026-09-03 with EF Core 10.0.11:**

```text
Where(p => p.Deadline > cutoff)        DateTimeOffset  -> InvalidOperationException
                                                          "could not be translated"
Where(p => p.Deadline <= cutoff)       DateTimeOffset  -> InvalidOperationException
OrderBy(p => p.Deadline)               DateTimeOffset  -> NotSupportedException
                                                          "SQLite does not support expressions of
                                                           type 'DateTimeOffset' in ORDER BY"
Where(p => p.DeadlineUtc > cutoff)     DateTime (UTC)  -> works
OrderBy(p => p.DeadlineUtc)            DateTime (UTC)  -> works
```

**Four places in the existing code break on this**, all silently at compile time and loudly at the
first request:

| Site | Expression |
|---|---|
| `RetentionService.LivePolls` | `Where(p => p.RetentionDeadline > clock.Now)` |
| `RetentionService.EraseExpiredAsync` | `Where(p => p.RetentionDeadline <= clock.Now)` |
| `PollService.ListAsync` | `OrderByDescending(p => p.CreatedAt)` |
| `ResultsProjection.BuildAsync` | `OrderBy(r => r.SubmittedAt)` |

**Rationale**: nothing is lost. Every instant in this system is already UTC — `BerlinClock` is the
only thing that knows about a zone, and it converts on the way out, not on the way in. Storing an
offset that is always zero was carrying information the domain does not have.

**Alternatives considered**:
- *A value converter mapping `DateTimeOffset` to a sortable string* — rejected. It restores
  ordering but leaves the comparison translation problem, and it hides a limitation behind
  machinery instead of removing the cause.
- *Client-side evaluation* — rejected outright. `LivePolls` is the access filter on every request;
  loading every poll to filter in memory would defeat the retention design and scale with the
  table.

---

## R-2: The response cap holds exactly under concurrency

**Decision**: keep the transaction-with-count-check shape from feature 002 R-9, replacing
PostgreSQL's `FOR UPDATE` row lock with SQLite's write-transaction semantics.

**Measured**: 200 concurrent submissions against a cap of 50, on one file:

```text
accepted = 50    rejected = 150    errors = 0    stored = 50    (1,966 ms)
```

Exactly the cap. Nothing lost, nothing double-counted, nothing failed.

**Rationale**: SQLite serialises writers, which is precisely the property `FOR UPDATE` was
providing. The guarantee FR-010 requires survives; the mechanism named in feature 002's research
does not, and that is recorded in this specification's Superseded Requirements table.

**Consequence to carry into the tasks**: this is the single most likely thing to be broken
silently by the migration, which is why FR-029 requires it to be proven against the new storage
rather than inherited. A busy timeout must be configured, or a contending writer surfaces as an
error rather than a wait.

---

## R-3: A hand copy is not a backup — while the system is running

**Decision**: the system produces a consistent copy through SQLite's online backup mechanism, and
the operator downloads it (FR-003). Hand-copying from the mounted directory while the system runs
is documented as unsupported (FR-003b).

**Measured — and the scope of the danger is narrower and sharper than first recorded here.** The
first version of this decision said a hand copy is unusable, full stop. That is not true, and the
integration test that was written to prove it failed instead. What the measurements actually show:

```text
Situation at the moment of the copy                       Hand copy of the main file
---------------------------------------------------------------------------------------
No connection open (system stopped)                       complete — 20 of 20
A connection open, companion file holding commits         20 of 40
A read in progress, pinning an older view                 40 of 60
Fresh storage, first connection still open                UNUSABLE — "no such table"
```

So: **stop the system and `cp` is fine; leave it running and the copy is silently short** — by a
few answers, or by every one of them including the schema, depending on how much has been folded
back into the main file at that instant. The operator cannot tell which they got. That is the
whole hazard, and it is worse than a copy that plainly fails, because this one looks right.

The last row is the case the specification originally predicted as "missing the most recent
answers". It is harsher than that: on storage that has never been folded back, the schema itself
is still in the companion file, so the copy has no tables at all.

**Rationale**: this is the same class of silent loss that ruled out plain JSON files as the system
of record, and it would have been introduced by the fix for it. The endpoint exists so the safe
route is also the convenient one — nobody stops a running system to take a backup.

**Alternatives considered**:
- *Document "copy the whole directory"* — better, and still wrong: a copy taken mid-write can be
  inconsistent, and it relies on discipline at exactly the moment someone is improvising.
- *Require the system to be stopped* — honest, correct, and it makes backups something one avoids
  doing. Availability that costs data is a bad trade in both directions.

---

## R-4: A confirmed answer survives a power cut

**Decision**: WAL journal mode with `synchronous=FULL`.

**Rationale**: FR-012a requires that a participant who has seen the confirmation keeps their
answer. WAL alone does not give that — its default `synchronous=NORMAL` acknowledges a commit
before it is durably on disk, so a power cut can swallow recent transactions without corrupting
anything. `FULL` closes that window.

At this write volume the cost is nil: answers arrive at human pace, not at machine pace. The
setting would matter for a system taking thousands of writes per second; this one takes a handful
per poll.

**Alternatives considered**:
- *`synchronous=NORMAL`* — the common recommendation, and it trades exactly the property this
  feature's specification argues from. Rejected on the specification's own reasoning.
- *Rollback journal instead of WAL* — durable, and it serialises readers against writers, which
  would make the results grid contend with submissions.

---

## R-5: What comes down with the diagnostic

**Decision**: remove `SystemStatus.vue`, the `status` store, the `/status` route, both endpoints
that fed them (`/api/v1/status/database` and `/api/v1/message`), `DatabaseProbe`,
`ConnectivityStatus`, and their tests.

**Rationale**: FR-022 and FR-023. The text endpoint existed only to prove data reached the page
during scaffolding; the status endpoint only to feed the page's three states. With the page gone
both are code that still has to be maintained, secured and reasoned about while serving nothing.

**What must not come down with it**: feature 001's suite used that page as a convenient surface
for assertions about the chain working at all. FR-024b requires those to be re-pointed at the
product — which now exercises the same chain on every end-to-end run — rather than deleted. The
number of product assertions may not fall (SC-014).

**Alternatives considered**:
- *Keep the endpoints, drop only the page* — rejected: an endpoint nothing reads is the definition
  of dead code, and it would still need to be covered by the admin-authorisation assertions.

---

## R-6: Testcontainers is no longer needed

**Decision**: remove `PostgresFixture` and the `Testcontainers.PostgreSql` dependency. Integration
tests get a temporary SQLite file per test class, deleted afterwards.

**Rationale**: the dependency was justified in feature 002's Complexity Tracking by one need — an
*empty* database per run, which a shared Compose instance could not provide. A temporary file
provides that need trivially. Keeping a container-orchestration dependency to get an empty file
would be the kind of accumulation Principle III asks to justify, and here it cannot be.

**Consequence**: the integration suite loses its dependency on a running Docker daemon, which also
makes it faster and usable where Docker is not available.

---

## R-7: One container

**Decision**: `compose.yaml` keeps a single `app` service. The `db` service, its healthcheck, its
volume and its environment go. A named volume holds the storage directory.

**Rationale**: FR-001 and FR-026. Leaving a dormant database service would keep 415 MB in the pull
and invite the two to drift apart.

**Consequence**: feature 001's `depends_on` reasoning (research R-6 of that feature — deliberately
*not* gating startup on database health so the failure path stayed testable) becomes moot along
with the diagnostic it protected.

---

## R-8: File permissions

**Decision**: the storage directory and its files are created with access for the application's
account only. The container already runs as a non-root user (`APP_UID`, from feature 001).

**Rationale**: FR-007a. Modest, and the honest limit of what the application can do — anything
beyond it is the host's business (FR-007c), which is why encryption at rest was rejected rather
than deferred.

---

## R-9: The shape of an export

**Decision**: one JSON document per poll, carrying `formatVersion`, `exportedAt`, the poll, its
days in order, and its responses with their per-day answers. Answer values use the same tokens as
the API — `yes`, `maybe`, `no` — and an unanswered day is absent, exactly as it is in storage.

**Rationale**: FR-014 to FR-020c. Reusing the API's vocabulary means the export and the interface
describe the same thing in the same words; inventing a second vocabulary would create two things
to keep in step. Absence continuing to mean "no answer" keeps the export honest about what was
actually recorded.

**The one thing the export must not contain** is any token (FR-015). The participant token grants
the ability to answer; a response's edit token grants the ability to change that answer. A file
handed to someone that carries either would quietly undo feature 002's FR-029, and it would be
undone by an omission rather than a decision — which is why SC-010 verifies it by scanning a
produced file against the tokens actually held in storage.

---

## R-10: Migrating the schema, not the data

**Decision**: delete the two existing EF Core migrations and generate one fresh migration for
SQLite. No data is carried across (FR-025).

**Rationale**: the existing migrations contain PostgreSQL-specific SQL and cannot be replayed on
SQLite. Since no deployment exists and nothing needs preserving, a single clean migration is
simpler and more honest than a chain that pretends to describe a history it never had.

**Alternatives considered**:
- *Keep the migration history and add a SQLite one* — rejected. The history would describe a
  database that no instance ever runs.
- *Write a data-migration tool* — rejected: FR-025 says there is nothing to migrate, and the
  development database holds test polls nobody needs.

---

## Measurements taken during implementation

Recorded here rather than in a report, because these are the numbers the requirements are
checked against and the next person will want them without re-running anything.

| | Measured |
|---|---|
| SC-001, containers running | **1** (was 2) |
| SC-002, images to pull | application image 254 MB; the 415 MB `postgres:17-alpine` pull is gone |
| SC-003, fresh checkout to answering | **~2 min** with a cold application layer, 2 s with a warm cache — the budget is 10 min |
| SC-005, backup restored into a second instance | 11 polls, 9 responses — all present |
| SC-011, export at the feature 002 limits | **2 s** for 1000 responses across 100 days — the budget is 10 s |
| Backend suite | 6 s, no Docker daemon required |

**Three things the engine already did, and one it did not.** Before writing `StorageSetup`, the
settings were read off a bare connection:

```text
journal_mode = wal    (set by the migration, not by us)
synchronous  = 2      (FULL - the engine's default)
foreign_keys = 1      (the driver's default)
busy_timeout = 0      <- nothing waits
```

Three of the four therefore held by coincidence. They are stated explicitly anyway: an inherited
default is not a guarantee, and nothing announces it when it changes. The fourth was the one that
mattered — without it a contending writer is refused instead of queued.

**Mutation checks.** Each of the two decisive guarantees was verified by breaking it:

| Mutation | Result |
|---|---|
| `deferred: true` on the write transaction | both concurrency tests fail |
| `synchronous = NORMAL` | `StorageSettingsTests` fails; `DurabilityTests` does **not** |
| `SecureFile` removed from startup | permission test fails (`0644` instead of `0600`) |

The middle row is worth stating plainly: no test can observe the difference between a commit that
reached the disk and one that reached the operating system's cache, because that needs the power
to actually fail. The durability guarantee is therefore carried by the setting, and the setting is
what is asserted. Claiming otherwise would be the more comfortable and less true report.
