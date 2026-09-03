# Feature Specification: SQLite Storage and JSON Export

**Feature Branch**: `dev` (feature directory `003-sqlite-and-export`; no feature branch per constitution v2.0.0)
**Created**: 2026-09-03
**Status**: Draft
**Input**: User description: "Die Postgres Datenbank macht die Lösung sehr schwergewichtig. Was könnten wir stattdessen nutzen? Ein verzeichnis, in das der aktuelle Status jeder abfrage als json abgelegt wird und die man als admin dann herunterladen kann? Das Verzeichnis könnte ich ja als volumen mounten."

## Overview

Rundfrage currently runs a PostgreSQL server to hold a few kilobytes of polls. Measured on
2026-09-03: the database image weighs **415 MB** and its process holds **23 MB** of memory to
store **8 MB** of data, of which the actual polls, days and answers are a few kilobytes. The
engine outweighs everything it stores.

This feature replaces that server with a **single SQLite file** on a mounted volume, reducing the
running system from two containers to one, and adds the thing the file-based idea was really
after: **the ability to take the data out** as readable JSON.

Those are two separate wants, and they get two separate answers. Storing raw JSON files as the
system of record was considered and rejected — see Rejected Alternatives — because concurrent
submissions to one file lose answers silently, which is precisely the failure the current design
already guards against and tests for.

Nothing about how the product behaves changes. Every requirement of features 001 and 002 that
survives the storage change survives it unaltered; the ones that mention the storage explicitly
are superseded here and named.

## Clarifications

### Session 2026-09-03

- Q: How should the JSON export be offered? → A: On-demand download per poll only; nothing mirrored to disk
- Q: What should feature 001's diagnostic page show now? → A: Remove it entirely
- Q: How is a reliable backup copy produced (FR-003, SC-005)? → A: The system produces a consistent snapshot while running; the creator downloads it
- Q: May a confirmed answer be lost to an abrupt power loss? → A: No — a participant who has seen the confirmation keeps their answer
- Q: How is the storage file protected at rest? → A: Restrictive file permissions and honest documentation; no encryption
- Q: Is the JSON export a format others may rely on? → A: It carries a format version; additive changes stay within a version

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The system runs as one container (Priority: P1)

An operator starts Rundfrage on a fresh machine. One container starts. The data lives in a file
inside a mounted directory, which they can copy, move to another machine, or put in a backup like
any other file.

**Why this priority**: It is the reason for the feature. Everything else here is a consequence or
an addition.

**Independent Test**: Start the system on a machine with no previous state, create a poll, stop
everything, and confirm exactly one application container ran and that the data lives in a single
file inside the mounted directory.

**Acceptance Scenarios**:

1. **Given** a fresh machine, **When** the operator runs the documented start command, **Then**
   exactly one container runs and the application is reachable.
2. **Given** the system has been used, **When** the operator inspects the mounted directory,
   **Then** the entire state is contained in files there and nowhere else.
3. **Given** the system is stopped and started again, **When** the operator opens the admin area,
   **Then** every poll and every answer is still present.
4. **Given** a backup produced by the system while it was running, **When** it is restored on
   another machine, **Then** the same polls and answers appear, including any answer recorded
   moments before the backup was taken.
5. **Given** an empty mounted directory, **When** the system starts for the first time, **Then**
   the storage is created automatically with no manual step.

---

### User Story 2 - The creator takes a poll out as JSON (Priority: P2)

The creator opens a poll in the admin area and downloads it. They get one JSON file containing
the poll and every answer to it, in a form a person can read and another program can process.

**Why this priority**: It is what the file-based idea was for — having the data in hand. It does
not depend on the storage change and could ship separately.

**Independent Test**: Create a poll, record two answers, download it, and confirm the file
contains the title, the days, both participants and their answers.

**Acceptance Scenarios**:

1. **Given** a poll with answers, **When** the creator downloads it, **Then** a JSON file is
   produced containing the title, the message, the candidate days in order, and every response
   with its display name and per-day answers.
2. **Given** a poll with no answers, **When** the creator downloads it, **Then** a valid JSON file
   is produced with an empty list of responses rather than an error.
3. **Given** any poll, **When** the export is produced, **Then** it contains no participant edit
   token and no participant link token.
4. **Given** an export, **When** it is read by a program, **Then** it parses as JSON without
   repair.
5. **Given** a visitor without a creator session, **When** they request an export, **Then** it is
   refused exactly as every other admin function is.

---

### Edge Cases

- **Two participants submit at the same instant**: both answers are recorded. Neither overwrites
  the other. This is the guarantee that ruled out plain JSON files as the system of record.
- **The 1000th and 1001st response arrive together**: the cap holds exactly; one is accepted and
  one refused, never both accepted.
- **The mounted directory is read-only**: the application starts, reports storage as unavailable,
  and does not pretend to accept answers.
- **A backup is taken while someone is answering**: the copy is internally consistent. It either
  contains that answer completely or not at all, never half of it.
- **Someone copies the storage file by hand while the system runs**: unsupported, and documented
  as such. FR-003b exists because this is what people reach for first.
- **Power is cut mid-submission**: an answer already confirmed to its participant is still there
  afterwards. An answer in flight, not yet confirmed, may be absent — that is honest, because
  nobody was told otherwise.
- **Power is cut mid-backup**: the partial backup file is worthless, and the system's own storage
  is unaffected.
- **The mounted directory is world-readable on the host**: the application cannot prevent this,
  and FR-007b makes sure nobody is surprised by what it means.
- **An export from an older version is opened later**: it still parses, and its version field says
  what it is. Nothing claims the application can read it back — importing is out of scope.
- **The mounted directory is empty on first start**: storage is created without a manual step.
- **The data file is deleted while the system runs**: behaves as unreachable storage, the same as
  a stopped database server did.
- **Two application instances point at the same file**: out of scope — the system is documented as
  single-instance, and the constitution's acceptance of serialised writers assumes it.
- **A poll is exported while someone is answering it**: the export is internally consistent — it
  never contains half of a response.
- **A very large export**: a poll at the limits of feature 002 (1000 responses across 100 days)
  produces a file that is still delivered as one download.

## Requirements *(mandatory)*

### Functional Requirements

**Storage**

- **FR-001**: The running system MUST consist of exactly one container. The separate database
  container introduced by feature 001 MUST be removed.
- **FR-002**: All persistent state MUST live in a single file inside a mounted directory.
- **FR-003**: The system MUST be able to produce a consistent copy of its entire state **while
  running**, and the creator MUST be able to download it. Restoring that copy on another machine
  MUST reproduce the same polls and answers.
- **FR-003a**: The produced copy MUST be complete on its own — a single artefact that needs no
  companion file to be restorable.
- **FR-003b**: Copying storage files by hand from the mounted directory while the system is
  running MUST NOT be presented as a supported backup route, and the documentation MUST say so.

  > This is the reason FR-003 asks for a mechanism rather than trusting a file copy. Storage of
  > this kind keeps freshly committed writes in companion files, so copying only the main one
  > yields a backup that is short — measured at anything from a handful of answers to every
  > answer *and the schema*, depending on how much has been folded back into that file at the
  > instant of the copy (research.md R-3). With the system stopped the copy is complete; running,
  > nothing about the file says which of these you got. The damage surfaces only when someone
  > tries to restore — the same class of silent loss that ruled out plain JSON files as the
  > system of record.
- **FR-004**: The storage MUST be created automatically on first start against an empty directory,
  with no manual step (carrying forward FR-013 of feature 001).
- **FR-005**: Applying the schema again against existing storage MUST be safe.
- **FR-006**: Data MUST survive stopping and starting the system.
- **FR-007**: The location of the storage MUST be configurable, with a working default so a fresh
  clone starts with no manual step.
- **FR-007a**: The storage file and any companion files MUST be readable and writable only by the
  account the application runs as, not by every account on the host.
- **FR-007b**: The documentation MUST state plainly that whoever can read the storage file can
  read every display name, every answer, and every participant and response token — and that the
  latter grant the ability to open and revise responses.

  > The exposure itself is not new: feature 002 R-3 already accepted that read access to storage
  > exposes everything, because a token is a lookup key and hashing it would prevent the lookup.
  > What changes is the *bar* for that access. It used to mean reaching a database server with
  > credentials; it now means reading a file in a directory the operator mounts, with ordinary
  > tools. The property is the same, the ease is not, and that belongs in writing.
- **FR-007c**: The storage MUST NOT be encrypted at rest. A key kept in the same configuration
  beside the data protects against nobody who can reach the data, while adding a way to lose
  everything by losing a key. Protection against a stolen disk is the host's business, not the
  application's.

**Behaviour that must not change**

- **FR-008**: Every functional requirement of feature 002 MUST continue to hold unchanged, except
  where this specification supersedes it explicitly.
- **FR-009**: Two responses submitted at the same instant MUST both be recorded. Neither may
  overwrite the other.
- **FR-010**: The 1000-response cap MUST remain exact under concurrent submission — the mechanism
  may change, the guarantee may not.
- **FR-011**: Deleting a poll MUST still remove its days, responses and answers, and the retention
  sweep MUST still erase expired polls.
- **FR-012**: The per-day totals MUST still exclude unanswered days, and unanswered days MUST
  still be stored as the absence of a record.
- **FR-012a**: A response MUST be durably stored before the participant is told it was accepted.
  A confirmation seen by a participant MUST survive an abrupt loss of power or an unclean stop.

  > The specification already argues from this: it is the reason an in-memory store with periodic
  > snapshots was rejected. Stating it as a requirement stops that reasoning from being undone by
  > a configuration default nobody revisits. Telling someone "saved" and later losing it is worse
  > than refusing the answer, because nobody notices.
- **FR-012b**: An unclean stop MUST NOT leave the storage unreadable. Losing recent work is a
  failure; losing everything is a different order of failure.

**Taking the data out**

- **FR-013**: The creator MUST be able to download a single poll as one JSON file.
- **FR-014**: The export MUST contain the poll's title, its message, its candidate days in
  chronological order, and every response with its display name and per-day answers.
- **FR-015**: The export MUST NOT contain any participant edit token or the poll's participant
  token. Handing out a file that grants the ability to change someone's answer would defeat
  FR-029 of feature 002.
- **FR-016**: The export MUST be valid JSON that parses without repair, and MUST use the same
  answer tokens as the API so the two describe the same thing.
- **FR-017**: Requesting an export without a creator session MUST be refused exactly as every
  other admin function is.
- **FR-018**: An export of a poll with no responses MUST succeed and contain an empty list.
- **FR-019**: An export MUST be internally consistent: it may not contain a partially written
  response.
- **FR-020**: The export MUST carry the moment it was produced, so a file found later can be
  placed in time.
- **FR-020a**: The export MUST carry a format version.
- **FR-020b**: Within a version, fields MAY be added. Removing a field, renaming one, or changing
  the meaning of an existing one MUST raise the version.
- **FR-020c**: The version is a signal, not a promise of permanence. Nothing commits to supporting
  an older version, to a migration path, or to a deprecation period — the point is that a change
  is *detectable* rather than silent.
- **FR-021**: The export MUST be produced on demand and delivered as a download. It MUST NOT be
  mirrored into the mounted directory as a standing file.
- **FR-021a**: The download MUST carry a filename that identifies the poll and the moment it was
  produced, so several exports can sit in one folder without overwriting each other.

**Retiring the walking skeleton**

Feature 001's diagnostic existed to prove the chain browser → application → storage while there
was no product to prove it. There is one now, and it is exercised end to end by feature 002's
tests on every run. The scaffolding comes down.

- **FR-022**: The diagnostic page MUST be removed, along with its route.
- **FR-023**: The endpoints that existed only to feed that page MUST be removed with it — the
  storage-status endpoint and the walking-skeleton text endpoint. Keeping an endpoint no page
  reads is dead code that still has to be maintained and secured.
- **FR-024**: The application MUST nevertheless still start when its storage cannot be reached,
  and MUST fail individual requests rather than refusing to run. The reason has changed but not
  disappeared: a container that crash-loops tells an operator far less than one that runs and
  returns errors, and its logs are harder to reach.
- **FR-024a**: When storage cannot be reached, the admin area MUST show that something is wrong
  rather than an empty page or a silent failure. It need not distinguish *why*.
- **FR-024b**: Removing the diagnostic MUST NOT weaken any assertion about the product itself.
  Tests that used the diagnostic page as a convenient surface MUST be re-pointed at the product,
  not deleted.

**Migration**

- **FR-025**: Existing PostgreSQL data MUST NOT be migrated. The system has never been deployed;
  the development database holds test polls only.
- **FR-026**: The PostgreSQL container, its volume, and its configuration MUST be removed rather
  than left dormant.

**Verification**

- **FR-027**: Every behaviour in this specification MUST be introduced test-first.
- **FR-028**: The existing test suites MUST continue to pass unchanged wherever they assert
  behaviour rather than storage technology. Where a test asserts the technology, it MUST be
  changed deliberately and the change recorded.
- **FR-029**: An automated test MUST prove FR-009 and FR-010 against the new storage, not merely
  against the old one.
- **FR-030**: After the removal, no route, component, store or test may remain that exists only to
  serve the retired diagnostic. A scan for orphaned code MUST come back empty.

### Key Entities

This feature introduces no new domain entity. `Poll`, `CandidateDay`, `Response` and `Answer`
carry over from feature 002 unchanged; only where they are stored changes.

- **Export Document**: One poll rendered as JSON for download. Carries a format version, the
  moment it was produced, the poll, its days, and its responses with their answers. Carries no
  token of any kind. Never persisted — it exists only for the duration of the download.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The running system consists of exactly one container, down from two.
- **SC-002**: The images pulled to run the system are at least 400 MB smaller than before.
- **SC-003**: A developer starting from a fresh clone reaches a working system in under 10 minutes
  with a single command, as before.
- **SC-004**: Stopping and starting the system preserves 100% of polls and answers.
- **SC-005**: A backup produced while the system is running, then restored on a second machine,
  reproduces the same polls and answers in 100% of attempts — including answers recorded in the
  seconds before the backup was taken.
- **SC-005a**: A backup taken during continuous submissions contains no partially written
  response, verified by restoring it and reading every response.
- **SC-006**: 100 responses submitted concurrently to one poll result in exactly 100 stored
  responses — none lost, none duplicated.
- **SC-006a**: Of responses confirmed to their participants, 100% are present after the process
  is killed without warning and restarted.
- **SC-006b**: After such a kill, the storage opens and every earlier poll and answer is readable.
- **SC-007**: With a poll holding 1000 responses, the 1001st concurrent submission is refused and
  the stored count remains exactly 1000.
- **SC-008**: Every test that passed before the storage change and asserts behaviour rather than
  technology still passes, with zero assertions weakened.
- **SC-009**: An exported poll parses as JSON on the first attempt and contains every response.
- **SC-009a**: Every export carries a format version and the moment it was produced.
- **SC-010**: Zero exports contain a participant or response token, verified by scanning a
  produced file for the tokens held in storage.
- **SC-011a**: The storage file's permissions grant access to the application's account only,
  verified by inspecting them on a running system.
- **SC-011**: An export of a poll at the feature 002 limits (1000 responses, 100 days) is produced
  and delivered within 10 seconds.
- **SC-012**: With storage unavailable, the application still answers requests and the admin area
  shows an error rather than a blank page, within 5 seconds.
- **SC-013**: After the removal, zero routes, components, stores or tests remain that exist only
  for the retired diagnostic.
- **SC-014**: The number of assertions about product behaviour is not lower after the removal than
  before it.

## Assumptions

- **Single instance.** One application process uses the file. The constitution's acceptance of
  serialised writers assumes this, and running two instances against one file is out of scope.
- **No data to migrate.** The system has never been deployed; the development database holds test
  polls that nobody needs.
- **Backups are the operator's business.** This feature guarantees that a consistent copy can be
  produced on request; when, where and how often it is kept is not its concern.
- **Protecting the host is the operator's business.** The application restricts its own file's
  permissions and says what the file contains; disk encryption and host access control are
  outside it.
- **The answer tokens stay as they are** — `yes`, `maybe`, `no` — so an export and the API
  describe the same thing in the same words.
- **Deployment remains out of scope**, as in features 001 and 002.

## Rejected Alternatives

- **JSON files as the system of record.** The original suggestion. Rejected because two
  participants submitting at the same instant would read the same file, both append, and the
  later write would silently discard the earlier answer. Feature 002 already guards against
  exactly this and tests it. The guard could be rebuilt with file locking or one file per
  response — but that is reconstructing, piece by piece, what a database already provides.
  Readability, the actual attraction of the idea, is delivered by the export instead, which
  produces a clean document rather than internal storage structure.
- **Keeping PostgreSQL.** Correct for many groups sharing one instance, and disproportionate for
  one person hosting one group: 415 MB of engine and a second container for a few kilobytes.
- **An in-memory store with periodic snapshots.** Lighter still, and loses answers on an
  unexpected stop. Answers people took the trouble to give are not something to lose — now a
  requirement rather than a sentiment (FR-012a).

## Dependencies

- Features `001-platform-scaffold` and `002-date-poll`: this feature changes their storage and
  supersedes the requirements named below.
- The project constitution at `.specify/memory/constitution.md` (v2.0.0), whose Technology
  Constraints this feature implements.

## Superseded Requirements

Named rather than left to contradict:

| Superseded | Was | Becomes |
|---|---|---|
| 001 FR-003, SC-010 | "exactly two containers" | exactly one (FR-001) |
| 001 FR-004 | data survives restart via a database volume | via the mounted file (FR-006) |
| 002 research R-9 | row lock via `FOR UPDATE` | the guarantee stands, the mechanism changes (FR-010) |
| 002 Complexity Tracking | Testcontainers justified by needing an empty PostgreSQL | re-justified or dropped as the new storage requires |
| 001 FR-006, FR-007 | a text endpoint the page displays | retired with the page (FR-023) |
| 001 FR-008 to FR-010, FR-012 | the storage check and its three displayed states | retired (FR-022, FR-023) |
| 001 FR-011 | the page renders when the database is down | the application still runs; there is no page (FR-024) |
| 001 SC-003, SC-004, SC-004a, SC-005 | measured against the diagnostic page | measured against the product (FR-024b) |

**What retiring the walking skeleton costs.** Feature 001's own acceptance criteria go with it,
and with them the cheapest place to see at a glance whether storage is healthy. That was a
deliberate trade: the scaffolding proved the chain when nothing else could, and feature 002 now
proves it on every end-to-end run. FR-024b exists so the removal cannot quietly take assertions
with it.

## Out of Scope

- Migrating existing data.
- Running more than one application instance against one file.
- Automated or scheduled backups — the system produces a backup on request; when and where to
  keep it is the operator's business.
- Encryption at rest, and any protection against someone who already has host access.
- Restoring a backup through the application. Restoring means putting the file in place and
  starting the system.
- Importing an export back into the system.
- Exporting anything other than a single poll at a time.
- Mirroring exports into the mounted directory as standing files.
- Any replacement for the retired diagnostic page.
- Any change to what the product does: no new poll type, no new participant behaviour.
- Deployment, hosting, and release automation.
