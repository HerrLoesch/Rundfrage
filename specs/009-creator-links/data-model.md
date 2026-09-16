# Data Model: Ersteller-Links

**Feature**: 009-creator-links | **Date**: 2026-09-13

One new table, two new nullable columns, four new indexes, one migration. Everything else in this
document is **derived and stored nowhere**, which is the same discipline features 002, 007 and 008
applied to expiry, the dashboard's figures and the closed state.

---

## 1. The new table

### `Creators`

| Column | Type | Null | Notes |
|--------|------|------|-------|
| `Id` | `TEXT` (GUID) | no | Primary key |
| `Name` | `TEXT(100)` `COLLATE NOCASE` | no | Operator-written label (FR-001, FR-003) |
| `LinkToken` | `TEXT(22)` | **yes** | The capability. `NULL` = revoked (R-3, FR-018, FR-020) |
| `CreatedAt` | `TEXT` (ISO-8601) | no | Shown in the Ersteller area (FR-044) |

**Indexes**

- `UNIQUE (Name)` — FR-002. `NOCASE`, so the index agrees with the service's `OrdinalIgnoreCase`
  comparison for ASCII and the service stays stricter beyond it (R-14, mirroring `WishItems`).
- `UNIQUE (LinkToken)` — the lookup key on every creator request. SQLite treats `NULL`s as distinct,
  so any number of revoked Ersteller coexist (R-3). **This is a SQLite-specific guarantee and is
  asserted by a test rather than assumed.**

**What is deliberately absent**

- **No password, no email, no contact detail of any kind** (FR-003, Principle IV). There is nothing
  to notify an Ersteller with, which is why FR-040b's refusal to notify is a property of the schema
  rather than a policy someone could reverse.
- **No `RevokedAt`, no status column.** Revocation is the absence of a token (R-3), for the same
  reason `Poll` has no `IsExpired` and `WishList` has no `ClosedAt`: a stored state is wrong for as
  long as its writer lags, and here it would be wrong in the dangerous direction.
- **No `LastSeenAt`, no usage counter, no audit trail** (FR-040b, Out of Scope).

---

## 2. The two new columns

### `Polls.CreatorId` and `WishLists.CreatorId`

| Column | Type | Null | Notes |
|--------|------|------|-------|
| `CreatorId` | `TEXT` (GUID) | **yes** | FK → `Creators.Id`. **`NULL` means the operator** (R-2) |

**Indexes**: non-unique on each, because the owner filter of R-1 is the hot path (R-14).

**Delete behaviour**: `ON DELETE CASCADE`, configured **explicitly** in `OnModelCreating`. EF Core's
default for an optional relationship is `ClientSetNull`, which would set `CreatorId = NULL` on delete
— handing the content to the operator, precisely the outcome FR-020c forbids (R-4).

**`NULL` is a value with a meaning, and SQL's three-valued logic will bite.** `WHERE CreatorId <>
@id` excludes the operator's rows rather than including them. Every read that must respect ownership
goes through `OwnerScope` (§4) so that no handler writes that predicate by hand.

---

## 3. Migration

One migration, `AddCreators`:

1. `CREATE TABLE Creators` with its two unique indexes.
2. `ALTER TABLE Polls ADD COLUMN CreatorId TEXT NULL REFERENCES Creators(Id) ON DELETE CASCADE`.
3. The same on `WishLists`.
4. Two non-unique indexes.

**No data migration runs.** FR-013 requires every pre-existing poll and wish list to become the
operator's, and a nullable column added to existing rows is already `NULL`, which already means the
operator (R-2). This is the whole of FR-013 and it is why the column is nullable.

> SQLite cannot add a column with a foreign key through `ALTER TABLE` in every version; EF Core's
> SQLite provider falls back to table rebuild (`__temp__` copy) when it must. That path preserves
> data and is exercised by `SchemaCreationTests`; the migration is applied against a populated
> database in a test rather than only against an empty one, because "the migration ran" and "the
> rows survived" are different claims.

---

## 4. Derived, stored nowhere

| Figure | Derived from | Requirement |
|--------|--------------|-------------|
| Whether an Ersteller's link works | `LinkToken IS NOT NULL` | FR-005, FR-020, FR-044a |
| Owner label shown to the operator | `CreatorId IS NULL` ? operator : `Creators.Name` | FR-039 |
| Polls owned / wish lists owned | `COUNT(*)` grouped by `CreatorId` | FR-044 |
| How many Ersteller exist, against the cap | `COUNT(*) FROM Creators` | FR-009 |
| Everything a creator may see | `OwnerScope` (§5) | FR-032, FR-033 |

No total is maintained. This is the rule 007 set for the dashboard and 008 kept for the filled
share: a stored aggregate would be stale across five deletion paths, and here there would now be
six.

---

## 5. `OwnerScope` — the only way in

The structural answer to FR-033. A scoped service resolved from the route token, exposing the only
queryables a creator handler is given:

```csharp
public IQueryable<Poll> Polls() =>
    retention.LivePolls().Where(p => p.CreatorId == creator.Id);

public IQueryable<WishList> WishLists() =>
    db.WishLists.Where(l => l.CreatorId == creator.Id);
```

Three properties, each load-bearing:

1. **Creator handlers do not receive `RundfrageDbContext`.** There is no unscoped queryable in
   reach, so FR-033 holds by construction rather than by review.
2. **Polls compose with `LivePolls()`.** FR-014 says ownership changes nothing about retention;
   without the composition a creator would be the only person who can still see their expired poll.
3. **Wish lists deliberately do not compose with any retention filter**, matching
   `WishEndpoints.LoadAsync`'s existing note: "There is no retention filter here and there must not
   be one: a wish list stays reachable until the operator deletes it."

A miss through `OwnerScope` returns `NeutralNotFound` — the same payload as a token that never
existed. FR-034 forbids distinguishing "not yours" from "no such thing", and reusing the existing
single payload is how 002 SC-012 and 008 FR-024 already do it.

---

## 6. Lifecycle

```
                 create (FR-004)          reissue (FR-016)
   [nothing] ──────────────────▶ [link works] ◀──────────────── [revoked]
                                      │                            ▲
                                      └──── revoke (FR-018) ───────┘
                                                 content untouched

   [link works] ──┐
   [revoked]  ────┴── delete (FR-020a) ──▶ [nothing]
                                            content destroyed by cascade (R-4)
```

Two transitions and one terminal action. **Revoke and delete are different edges**, which is the
whole of the spec's Q1: revoking moves along the top row and touches no other table; deleting leaves
the diagram and takes the Ersteller's polls, wish lists, answers and entries with it.

Ownership itself has **no** transitions. `CreatorId` is written once at creation and never updated
(FR-012, FR-040a) — including by a delete, which is why R-4's cascade must not be left at EF Core's
`ClientSetNull` default.

---

## 7. Validation rules

| Rule | Where | Requirement |
|------|-------|-------------|
| Name present, ≤ 100 chars | Service, before insert | FR-001, FR-002 |
| Name distinct, case-insensitively | Service **and** unique index | FR-002, R-14 |
| At most 100 Ersteller | Service, inside an immediate write transaction | FR-009, R-11 |
| The cap's refusal names how a place is freed | Endpoint payload → catalogue | FR-009a |
| Deleting frees a name and a place | Falls out of the row being gone | FR-002, FR-009a |
| A creator's poll or wish list obeys every 002/008 limit | Existing services, unchanged | FR-022, FR-023 |

**No limit is added on how much one Ersteller may own** (FR-036a). The only new enforced maximum in
this feature is the 100 of FR-009.

---

## 8. What does not change

- `Poll`, `CandidateDay`, `PollResponse`, `DayAnswer`, `WishList`, `WishItem`, `WishClaim` keep every
  existing column, index, limit and cascade. Two of them gain one nullable column.
- Retention is untouched: `RetentionService.LivePolls()` and the hourly sweep do not know that
  owners exist, and must not learn (FR-014).
- `WishList` still has no expiry and no derived deletion date (008 FR-037). An Ersteller's wish list
  outlives its Ersteller's link and dies only when somebody deletes it — or when the Ersteller itself
  is deleted (FR-020a).
- Participant tokens, the personal edit token and the claim token are unchanged (FR-048).

---

## 9. Backup and restore

- **Backup** (FR-051) needs no change: it copies the file, so the new table travels with it.
- **Restore preview** (FR-052) needs one: `RestoreService.ReadCountsAsync` gains a creator count,
  read through the same tolerant table-presence check `ReadWishCountsAsync` uses — a backup taken
  before this feature has no `Creators` table and is still a perfectly valid backup, so its creator
  count is zero rather than a refusal.

`RestorePreview` gains `CreatorsInBackup` and `CreatorsLost`. The record's own doc comment already
explains why this is not optional: without it the preview "would say *you lose 0 polls* while twelve
wish lists went with them" — and after this feature, while a hundred Ersteller links went with them.
