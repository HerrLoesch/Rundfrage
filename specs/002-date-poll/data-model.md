# Phase 1 Data Model: Date Poll

**Feature**: 002-date-poll | **Date**: 2026-09-02

Feature 001 introduced no domain entity at all — its migration was deliberately empty. This is
the first real schema. Four tables, and two things deliberately absent from all of them.

---

## What is deliberately not stored

Two absences carry as much design weight as the columns, and both are constitutional:

- **No column anywhere holds an IP address, a user agent, or any other network metadata**
  (FR-042). The rate limiter in R-5 sees a request source but only in process memory, never here.
  This is why `PollResponse` has no `CreatedFromAddress` column even though one would be the
  obvious way to implement duplicate prevention — Principle I forbids that route entirely.
- **No column holds a participant identity.** `DisplayName` is a label (FR-022). Nothing joins on
  it, nothing enforces uniqueness on it, and two responses with the same name are two different
  responses.

---

## 1. `Poll`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | Primary key. Used only in admin routes, never in a participant link. |
| `Title` | `varchar(300)` | Required (FR-008). Limit from FR-015. |
| `Message` | `varchar(2000)` null | Optional (FR-009). |
| `ParticipantToken` | `varchar(22)` | Unique, indexed. 128 bits base64url (R-3). The capability. |
| `CreatedAt` | `timestamptz` | UTC instant. |
| `RetentionDeadline` | `timestamptz` | Derived once at creation, see below. |

**Rules**

- At least one candidate day must exist (FR-010); enforced at creation, since a poll and its days
  are written in one transaction.
- At most 100 candidate days, at most 1000 responses (FR-015). The response cap is enforced under
  a row lock on this table (research.md R-9).
- Deleting a poll cascades to its days and responses — a real delete, not a flag (FR-037).

**`RetentionDeadline` is computed once, at creation** (FR-039a), as: the last candidate day, taken
to 23:59:59 in Europe/Berlin, plus 30 days, converted to a UTC instant. It is stored rather than
recomputed so that the value the creator was shown is the value that applies, and so a single
indexed comparison drives both the access filter and the erasure sweep.

> **There is no status column.** Whether a poll is expired is *derived* by comparing
> `RetentionDeadline` to the current instant on every access (FR-039b). A stored flag would need
> a writer, and between the deadline passing and that writer running the flag would be wrong —
> which is exactly the window FR-039b was written to close.

---

## 2. `CandidateDay`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | Primary key. |
| `PollId` | `uuid` | Foreign key, cascade delete. |
| `Date` | `date` | A whole calendar day (FR-011). No time, no offset. |

**Rules**

- Unique on (`PollId`, `Date`) — a day selected twice is stored once (FR-012).
- Ordered by `Date` for display regardless of selection order (FR-013).
- Past dates are permitted (FR-014).
- Stored as `date`, not `timestamptz`: a candidate day is a label on a calendar, not an instant.
  Interpreting it against Europe/Berlin happens only where it is compared to "now" or used to
  compute the retention deadline (FR-011a) — never in storage, which is why a summer-time change
  cannot shift a stored day.

---

## 3. `PollResponse`

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` | Primary key. Used in the admin delete route (FR-037a). |
| `PollId` | `uuid` | Foreign key, cascade delete. |
| `DisplayName` | `varchar(100)` | Required (FR-022). A label. Limit from FR-015. |
| `EditToken` | `varchar(22)` | Unique, indexed. The capability to revise this one response. |
| `SubmittedAt` | `timestamptz` | UTC instant. |

**Rules**

- The edit token grants access to this response and nothing else (FR-029).
- Revising updates in place; it never inserts a second row (FR-030).
- No uniqueness on `DisplayName` — duplicates are expected and correct.

---

## 4. `DayAnswer`

| Column | Type | Notes |
|---|---|---|
| `ResponseId` | `uuid` | Foreign key, cascade delete. |
| `CandidateDayId` | `uuid` | Foreign key, cascade delete. |
| `Availability` | `smallint` | `Yes` = 1, `Maybe` = 2, `No` = 3. **Three values, not four.** |

Composite primary key (`ResponseId`, `CandidateDayId`).

**The absence of a row is the *no answer* state** (research.md R-8, FR-024). This is why the enum
has three values: there is no `NoAnswer` to store, because nothing is stored. Two consequences
follow, and both are load-bearing:

1. **FR-033 becomes true by construction.** The per-day totals are a grouped count over the rows
   that exist, so they cannot count what was never written. There is no filter to remember and
   therefore none to forget.
2. **Storage falls roughly by half** at the FR-015 limits for a typical poll where people answer
   some days and skip others — which is what Principle IV asks for.

---

## Relationships

```text
Poll 1---* CandidateDay          cascade delete
Poll 1---* PollResponse          cascade delete
PollResponse 1---* DayAnswer     cascade delete
CandidateDay 1---* DayAnswer     cascade delete
```

Deleting a poll therefore removes everything beneath it in one operation, which is what makes
FR-037's "removed, not hidden" and SC-007's "100% of its responses" straightforward to assert.

---

## Lifecycle

```text
Poll:      created ──► reachable ──(RetentionDeadline passes)──► unreachable ──► erased
                                    derived on every access          background sweep
                                    (FR-039b, instant)               (FR-039c, <= 24 h)

           any point ──(creator deletes)──► erased immediately (FR-037)

Response:  submitted ──► revisable via EditToken (FR-028)
                     ──► erased with its poll, or individually by the creator (FR-037a)
```

**`unreachable` is not a stored state.** It is the interval between the deadline passing and the
sweep running, during which the row still exists but every route refuses it. Naming it here
matters because it is the state a test has to construct to verify SC-031, and it is invisible in
the schema.

---

## Derived, never stored

| Value | Derived from | Why not stored |
|---|---|---|
| Per-day totals of *Ja* / *Vielleicht* / *Nein* | Grouped count over `DayAnswer` | A maintained counter would be a second source of truth to keep correct across submit, revise and delete. |
| Number of responses | Row count over `PollResponse` | Same. It is also what FR-033a relies on to make the uncounted *no answer* state legible. |
| Whether a poll is expired | `RetentionDeadline` vs now | See the note under `Poll`. A flag would be wrong for as long as its writer lagged. |

---

## Configuration (not persisted)

| Variable | Purpose |
|---|---|
| `ADMIN_USER` | Operator account name (FR-045). |
| `ADMIN_PASSWORD_HASH` | PBKDF2 string produced by `--hash-password` (FR-045a, research.md R-2). |

Neither is a database row. There is no user table, because there is no user management (FR-045).
