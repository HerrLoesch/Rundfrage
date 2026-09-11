# Phase 1 Data Model: Importing Exported Data, and a Maintenance Mode

**Feature**: 005-import-and-maintenance-mode | **Date**: 2026-09-04

**No stored entity changes.** `Poll`, `CandidateDay`, `PollResponse` and `DayAnswer` keep their
shapes, and there is no migration in this feature. That is worth stating plainly, because a feature
about importing data sounds like one that changes the schema, and this one does not: an import
writes the same rows the existing creation path writes.

What is new is one piece of state that is deliberately **not** in the database, and three transient
shapes that exist only for the length of a request.

---

## 1. Maintenance state — persistent, outside the database

A marker file in the data directory, beside `rundfrage.db` and not inside it (R-6).

| Property | Value |
|---|---|
| Location | `${DATA_DIR}/maintenance` |
| Meaning | File present → maintenance is on. File absent → off. |
| Content | The UTC instant it was switched on, ISO-8601. Nothing else. |
| Permissions | Owner read/write, as `StorageSetup.SecureFile` already applies to the storage file |

**Why the presence of the file, and not a value inside it**: the state has to survive being read
when the content is unreadable. A truncated or empty file after a power cut must still mean "on" —
the safe direction — rather than throwing or defaulting to "off" and reopening the participant side.
The instant is informational; the file's existence is the state.

**Why not in the database** (the full comparison is R-6): FR-030 requires it to survive a restore,
and a restore replaces every row. A flag in the database would be switched off by restoring a backup
taken before maintenance began — silently reopening the site in the middle of the maintenance
window, which is the one moment it must not happen.

**Invariants**

- Switching on when already on, or off when already off, is not an error (spec Edge Cases).
- Nothing except the operator's explicit action changes it. In particular a restore must not
  (FR-023), and R-1 confirms a restore touches only the database file and its companions.
- It is never read from participant-supplied input and never written by a participant path.

---

## 2. Import document (JSON) — transient, request-scoped

The shape `PollExport` already writes, read in the other direction. Version 1 only; a higher
declared version is refused rather than interpreted (FR-007). What is taken is reproduced exactly
as the file records it (FR-009), and every limit the by-hand creation path enforces is enforced
here too (FR-014) — the right-hand column below names the constant each one comes from.

```text
formatVersion  int          required   must equal 1
exportedAt     timestamp    ignored on import — informational in the file
poll.title     string       required   1..300 chars          → Poll.TitleMaxLength
poll.message   string?      optional   0..2000 chars         → Poll.MessageMaxLength
poll.days[]    date[]       required   1..100 entries        → Poll.MaxCandidateDays
responses[]              optional   0..1000 entries       → Poll.MaxResponses
  .displayName string       required   1..100 chars          → PollResponse.DisplayNameMaxLength
  .answers[]
    .date        date       required   must match a poll.days entry
    .availability string    required   one of yes | maybe | no
```

**Refusal vs. skip.** These are different outcomes and the distinction is the feature's core
behaviour (FR-012, FR-013, and Assumptions):

| Condition | Outcome |
|---|---|
| Not JSON, or not this document shape | **Refuse** — nothing is created |
| `formatVersion` > 1 | **Refuse** |
| Title, message or day count outside its limit | **Refuse** — the poll cannot be created at all |
| Poll's last candidate day already past retention | **Skip the poll**, report why |
| A response's `date` is not among the poll's days | **Skip that answer**, report it |
| Unrecognised `availability` | **Skip that answer**, report it |
| Display name too long | **Skip that response**, report it |
| Response count over the limit | **Refuse** — see note |
| Duplicate dates in `poll.days` | De-duplicate, as poll creation already does (002 FR-012) |
| A response answering the same day twice | **Skip the response**, report it — no rule picks a winner |

> Note on the response limit: it is a refusal rather than a truncation on purpose. Skipping the
> 1001st response would mean choosing which answers to discard, and no ordering in the file makes
> that choice defensible. A file over the limit is a file the operator must look at.

**A day a response did not answer has no entry, and gains none** (FR-010). Absence is the state
in the file and the state in storage; inventing a fourth value on the way in would assert that
something was recorded when nothing was.

**Not carried by the format, and therefore assigned on import**: both tokens (fresh, FR-011),
`CreatedAt` (the import instant), `SubmittedAt` (the import instant; the file records none, so file
order is the only order — spec Assumptions), and `RetentionDeadline` (by the ordinary rule from the
last candidate day, FR-015).

---

## 3. Import summary — transient, the response itself

Not stored, and there is no history (FR-003a).

```text
imported        bool          whether a poll was created
pollId          guid?         present when one was
participantUrl  string?       the new link, so the operator can copy it immediately
counts
  days          int           candidate days taken
  responses     int           responses taken
  answers       int           individual day answers taken
skipped[]                     one entry per thing not taken
  kind          string        poll | response | answer
  reason        string        a code the interface renders — not free prose
  detail        string?       which day, which name — never the whole item
```

`skipped` being empty and `imported` being true is the clean case. **`imported` false with a
populated `skipped` is a normal outcome, not an error** — a file holding one expired poll produces
exactly that, and FR-004 requires it to read as "nothing was taken" rather than as success.

---

## 4. Restore outcome — transient, the response itself

```text
restored        bool
counts
  polls         int          polls now present
  responses     int
expired[]       string[]     titles of polls restored that are already past retention (FR-016a)
lostSincebackup int          polls that existed before and are not in the backup (FR-018 preview)
```

The preview under FR-018 uses the same shape before anything is replaced, so the operator sees the
same numbers they will be asked to confirm.

---

## 5. State transitions

**Maintenance mode** — two states, both operator-driven:

```text
        ┌──────────── operator switches on ───────────┐
        │                                             ▼
    [ off ]                                        [ on ]
        ▲                                             │
        └──────────── operator switches off ──────────┘

    restart      : state preserved (FR-029)
    restore      : state preserved (FR-030, FR-023)
    health check : reports healthy in BOTH states (FR-031)
```

**What the maintenance middleware does**, and the split that makes FR-028 hold. It sits ahead of
routing and divides requests three ways — one gate, not a check repeated per endpoint, so a route
added later cannot forget it:

```text
/api/v1/admin/**   -> pass through untouched     FR-028  operator keeps working, incl. switching off
/api/v1/health     -> pass through untouched     FR-031  a deliberate state is not a fault
everything else    -> the maintenance notice     FR-026  no poll content, no submission accepted
```

The third branch covers reads and writes alike, which is what makes FR-027 true without a separate
rule: a submission never reaches the handler, so there is nothing that could record an answer and
then report success.

**A restore**, with the ordering that makes FR-019 and FR-021 true by construction rather than by
care:

```text
1. refuse unless maintenance is on              FR-024        — nothing touched
2. receive the upload                           FR-005a       — nothing touched
3. open it as a database; integrity_check       FR-019, R-4   — nothing touched
4. suspend the retention sweep                  R-2           — nothing touched
5. take a safety copy of the current storage    FR-021
6. BackupDatabase(upload -> live)               FR-020, R-1
7. run migrations against the result            FR-022, R-3
8. resume the sweep; delete the upload          FR-005a
```

Steps 1–4 cannot lose anything: at the first point where the live storage is written, the file has
already been proven readable and the sweep is already held off. Step 5 is what makes a failure in
6 or 7 recoverable.
