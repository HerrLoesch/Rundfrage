# Phase 0 Research: Ersteller-Links

**Feature**: 009-creator-links | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md)

Fourteen decisions. Most of them are *reuse* decisions — this feature introduces one new table and
one new access filter, and almost everything else it needs is already in the codebase because
features 002, 005 and 008 built it. Three findings are worth reading even if nothing else is:
**R-6** (maintenance mode already covers the new surface, for free, and would not have if the route
prefix were chosen carelessly), **R-2** (the ownership column is nullable, which is what makes
FR-013's migration a no-op), and **R-9** (the creator surface departs from feature 007's addressing,
and the plan must not "fix" that).

---

## R-1: How a creator token authorises

**Decision**: A route group `/api/v1/e/{creatorToken}` whose endpoint filter resolves the token
once per request into a scoped `CreatorContext`, plus an `OwnerScope` service exposing
`Polls()` and `WishLists()` as the **only** queryables the creator handlers may use — built exactly
the way `RetentionService.LivePolls()` is built, and composed with it for polls.

```csharp
// The only way in, for a creator request.
public IQueryable<Poll> Polls() => retention.LivePolls().Where(p => p.CreatorId == creator.Id);
public IQueryable<WishList> WishLists() => db.WishLists.Where(l => l.CreatorId == creator.Id);
```

**Rationale**: FR-033 says scoping must be enforced *where the data is read*, not by filtering a
full result. The codebase already has one requirement of that exact shape — 002 FR-039b, "a poll
becomes unreachable the moment its deadline passes" — and it was solved with a single queryable
that every read passes through, documented as "The only way in". Copying that shape means FR-033 is
satisfied by construction: a handler that forgets the filter has nothing to call, because
`RundfrageDbContext` is not injected into creator handlers at all.

The composition with `LivePolls()` is not decoration. FR-014 says ownership changes nothing about
retention, so an expired poll must be invisible to its creator exactly as it is to the operator;
writing `db.Polls.Where(p => p.CreatorId == ...)` would have made a creator the one person who can
still see an expired poll.

**Alternatives considered**:

- *A full ASP.NET authentication handler issuing a `ClaimsPrincipal` from the route token.* Rejected:
  it would make the creator look like a second session to every piece of middleware, and
  `RequireAuthorization()` on the admin group would start being a question rather than a fact. The
  constitution's Principle I is also explicit that the token *is* the authorisation; wrapping it in
  an identity invites someone to add a second factor later.
- *Resolving the token inside each handler, as the participant routes do.* Rejected here and only
  here: a participant route reads one aggregate by its own token, so there is nothing to scope. A
  creator route reads *lists*, and a forgotten `Where` is a silent cross-tenant leak rather than a
  visible bug. SC-003 forbids exactly that.
- *A global query filter on `Poll`/`WishList` (`HasQueryFilter`).* Rejected: the same `DbContext`
  serves the operator, who must see everything (FR-038). A filter that has to be switched off for
  half the application is worse than no filter, and `IgnoreQueryFilters()` sprinkled through the
  admin endpoints would be the leak in the other direction.

---

## R-2: Modelling ownership

**Decision**: A nullable `Guid? CreatorId` on `Poll` and on `WishList`. **`NULL` means the
operator.** No row is created for the operator.

**Rationale**: FR-013 requires every existing poll and wish list to become the operator's, and to
behave exactly as before. With a nullable column the migration is `ALTER TABLE ... ADD COLUMN`,
every existing row is already correct, and no data migration runs at all — which is the only version
of FR-013 that cannot go wrong on somebody's production volume.

**Alternatives considered**:

- *A synthetic "operator" row in `Creators`, so the FK is non-nullable.* Rejected on three counts,
  the last of which is decisive: it needs seeding in a migration; it can be deleted by the very
  endpoint FR-020a adds; and it would count against the limit of 100 (FR-009), so the operator would
  be one of their own hundred Ersteller. A sentinel row that every code path must remember not to
  treat as data is the kind of speculative structure Principle III rejects.
- *A discriminator column beside the FK (`OwnerKind`).* Rejected: two columns that can disagree,
  where one nullable column cannot.

**Accepted consequence**: `NULL` as a meaningful value needs saying out loud wherever it is read,
because SQL's three-valued logic makes `CreatorId != @id` quietly exclude the operator's rows. It is
said in the entity's XML doc and it is what R-1's `OwnerScope` exists to stop anybody having to
remember.

---

## R-3: Revocation as an absent token

**Decision**: `Creator.LinkToken` is **nullable**. Present = the link works; `NULL` = revoked. The
unique index stays, because SQLite permits many `NULL`s in a unique index.

**Rationale**: The spec's Q1 settled that revoking is not a lifecycle state but the absence of a
working link (FR-005, FR-020, Key Entities: "Having a link or not is the only state an Ersteller
has, and it is not a lifecycle"). A nullable token column *is* that sentence in the schema. Reissue
(FR-016) is one `UPDATE`, and it invalidates the old link by the only mechanism that can: the old
value is no longer in the table, so the lookup misses and `NeutralNotFound` answers (FR-008).

**Verified rather than assumed**: SQLite treats `NULL`s as distinct in a `UNIQUE` index, so 40
revoked Ersteller do not collide. This is a documented SQLite behaviour and differs from some other
engines; a test asserts it rather than leaving the schema depending on folklore.

**Alternatives considered**:

- *A `RevokedAt` timestamp beside a non-nullable token.* Rejected: it keeps a dead token in the
  table, so "the link stops working" becomes a filter that every lookup must remember — and a
  forgotten filter means a revoked link still works, which is the one failure this column exists to
  prevent. It also stores a fact nobody asked for (Principle IV).
- *Deleting and re-creating the row on reissue.* Rejected: it would break FR-016's promise that the
  content stays theirs, because the content points at the row.

---

## R-4: Deleting an Ersteller

**Decision**: `Creator` → `Poll` and `Creator` → `WishList` are configured with
`OnDelete(DeleteBehavior.Cascade)` **explicitly**, and the endpoint deletes with
`ExecuteDeleteAsync`, relying on the database's own foreign keys.

**Rationale**: FR-020a destroys the Ersteller with everything it owns, and FR-020c says there is
nowhere else for that content to go. Cascade at the database is the same mechanism 002 and 008 use
for poll→response and list→item→claim, and `StorageSetup` already turns `PRAGMA foreign_keys` on for
every connection, so it is enforced rather than hoped for.

**The trap, stated because it is easy to miss**: EF Core's default for an **optional** relationship
is `ClientSetNull`, not `Cascade`. Left at the default, deleting an Ersteller would silently set
`CreatorId = NULL` on all its content — which is to say it would hand it to the operator, the exact
outcome Q1 rejected and FR-020c forbids. The configuration must therefore be explicit, and a test
asserts the content is gone rather than reassigned.

---

## R-5: The write budget

**Decision**: A second rate-limiting policy, `creator-writes`, at **60 per hour per request source**
(FR-036), configurable through `CREATOR_WRITE_LIMIT_PER_HOUR`, applied with
`.RequireRateLimiting(...)` to every creator route that changes something and to none that only
reads (FR-036c).

**Rationale**: FR-036 fixes the number; this decision is only about where it lives. A second policy
rather than a second limiter type, because `RateLimiting` already owns the pattern — in-memory
partitions, the request source never written anywhere (FR-036d), a configurable permit count whose
justification is recorded in that file: the end-to-end suite legitimately exceeds the production
number from one machine, and discovering that at run time once was enough.

**Verified rather than assumed**: partitions in ASP.NET Core's rate limiter are held **per policy**,
so `creator-writes` and `submissions` do not share a bucket for the same IP address. This matters:
an operator testing their own installation would otherwise spend a creator's budget by answering a
poll. A test asserts it, because it is a framework behaviour this code depends on and does not own.

**Alternatives considered**: reusing `SubmissionPolicy` — rejected by the spec at clarification
time, for the reason recorded there (ten writes does not cover adding ten items).

---

## R-6: Maintenance mode needs no change at all

**Finding**: `MaintenanceMiddleware` intercepts everything under `/api/` **except**
`/api/v1/admin/**` and `/api/v1/health`. Creator routes at `/api/v1/e/{token}/...` are neither, so
FR-050 — "while maintenance is on, the creator surface MUST be refused with the notice the
participant surface already uses" — is satisfied by the route prefix and nothing else.

**Why it is recorded rather than assumed**: it is only true because the creator routes are *not*
mounted under the admin group. Mounting them there would have been a defensible-looking choice —
they are authorised, after all — and it would have quietly given a bearer link the one exemption the
middleware grants, letting Ersteller create polls into a database that is about to be replaced by a
restore. The requirement is met by a decision, so the decision is written down and asserted by a
test rather than left to survive the next refactor by luck.

---

## R-7: The route prefix

**Decision**: `/api/v1/e/{creatorToken}/...` on the API, `/e/:creatorToken` in the SPA. **`e` for
Ersteller**, beside `u` (Umfrage), `a` (Antwort), `w` (Wunschliste) and `z` (Zusage).

**Rationale**: 008 research R-8 established the one-letter German initial as the scheme for
capability paths; this is the fifth and follows it. Short matters more here than elsewhere, because
FR-028d makes this the *only* address the surface has and a person will be sent it in a message.

---

## R-8: Reusing the projections rather than writing second ones

**Decision**: `WishListProjection.ListAsync` and the poll listing gain an **owner-scoped overload**
(an `IQueryable` parameter, or a nullable owner filter) rather than acquiring creator-specific
twins.

**Rationale**: this is 008 research R-5's argument applied once more. That projection has two
readers — the wish-list area and the dashboard — "and that is deliberate: FR-049 requires them to
agree, and one projection makes agreement structural rather than something a test has to keep true".
FR-047 now adds a third reader with the same requirement. A second projection would be a second
place for the filled share to be computed, and the first divergence would be invisible because each
side would be self-consistent.

---

## R-9: One address, and the plan must not improve on it

**Decision**: the SPA gets exactly **one** new public route, `/e/:creatorToken`, rendered inside
`BareShell` beside the participant surfaces. Both lists live on that page. A poll's answers and a
wish list's detail open as component state. No child routes, no query parameters, no
`router.replace` (FR-028d, FR-028e).

**Rationale**: this is the spec's Q5 and it deliberately contradicts feature 007's own reasoning,
which is why it is repeated here where an implementer will meet it. 007 FR-014a gave a poll's
answers its own address *because* "the largest screen in the admin area is the only one that cannot
be linked to or survive a reload". The premise differs: the operator holds a session, so an address
costs them nothing, while the Ersteller's whole authorisation *is* the address, and each additional
one is another place the token is written down.

**A reviewer who reads `router.ts` will see the asymmetry and be tempted to fix it.** They should
not. The cost is real and accepted in the spec (no deep link to one poll's answers, no useful back
button), and FR-028g — the lists must carry enough summary to find things without opening each in
turn — is what pays for it.

---

## R-10: Token exposure, and `Referrer-Policy`

**Finding**: the application sets no `Referrer-Policy` header today. Exposure is already low —
Principle IV bans external assets, so there is no cross-origin request for a browser to attach a
referrer to — but with FR-028d the creator's address carries a credential, and the default policy
would send the full URL on any future cross-origin navigation.

**Decision**: set `Referrer-Policy: same-origin` on every response, as a one-line addition beside
the existing cache-control header logic in `Program.cs`.

**Honest scope**: this is defence in depth for a risk that is currently theoretical, and it is
listed as such rather than as a fix for a live defect. It is included because it costs one line and
because the thing it protects — a bearer credential in a URL — is new with this feature.

---

## R-11: Enforcing the cap of 100 safely

**Decision**: the count-and-insert of FR-009 runs inside an **immediate** write transaction, using
`ClaimService.BeginWriteTransactionAsync`'s pattern — `BeginTransaction(Serializable,
deferred: false)` on the underlying `SqliteConnection`, adopted by EF Core.

**Rationale**: `COUNT(*)` then `INSERT` is a read-modify-write, and 008 research R-1 already
established that SQLite's default deferred transaction upgrades its lock too late to make one safe.
The window here is small and the consequence mild — 101 Ersteller rather than 100 — but the pattern
exists, is three lines, and writing the naive version would leave the codebase with two answers to
one question.

**Accepted consequence**: this is the *second* place the immediate-transaction dance appears, which
is exactly the threshold Principle III sets ("in response to a second concrete use"), so the plan
lifts it into one helper and has the existing caller use it — a refactor with no behaviour change,
guarded by the tests that caller already has.

> **Corrected during implementation (2026-09-14).** This entry originally claimed a *third* caller,
> naming `WishListService` alongside `ClaimService`. There is no third: `WishListService` raises a
> wanted count without a transaction of its own, and `ClaimService` is the only existing caller. The
> extraction still stands — two uses is the threshold, not three — but the count was wrong and the
> principle was misquoted, so both are fixed here rather than left as a justification that does not
> survive a `grep`.

---

## R-12: What the admin surfaces gain

**Decision**: `PollSummary` and `WishListSummary` each gain `CreatorId` and `CreatorName`, both
nullable, `NULL` meaning the operator (FR-039). The dashboard's wish-list overview reads
`WishListSummary` and therefore gains the owner without a second change (FR-041, FR-042).

**Rationale**: the summary records are what the areas render, and FR-047/FR-049 require every
surface to agree. Putting the owner on the shared record is the same structural-agreement argument
as R-8.

**Checked against 007 FR-034 and 008 FR-050**: a creator name is operator-written text, permitted on
the dashboard by FR-042 on the same grounds 008 FR-048b permitted wish-list titles. No participant
display name is added anywhere, and `WishListSummary`'s doc comment already records that keeping
names out of the row is what makes FR-050 structural. That property is preserved.

---

## R-13: Logging

**Decision**: creator actions log the **creator id** and counts. Never the token, never the name.

**Rationale**: 002 FR-043a and every existing log statement in `ResponseService`, `ClaimService` and
`RetentionService` follow this rule — "Identifiers and counts only. Never the name, never the
token." FR-037 restates it for the creator token. The name is excluded too, even though it is
operator-written rather than participant data, because a log line naming a person is a step the
feature does not need (Principle IV) and SC-013 is easier to assert when the rule has no exceptions.

---

## R-14: Scale and indexing

**Decision**: non-unique indexes on `Polls.CreatorId` and `WishLists.CreatorId`; a unique index on
`Creators.LinkToken` (R-3) and on `Creators.Name`.

**Rationale**: SC-010 asks for an Ersteller's own lists within two seconds at 100 Ersteller sharing
the installation-wide scales of 007 FR-028c and 008 FR-054. The owner filter of R-1 is the hot path
and it is the only new predicate, so it is the only new index. The `Creators` table is at most 100
rows, so nothing else on it needs one.

**No spike is needed before design, and that is a finding rather than an omission.** 007 needed one
because its dashboard aggregate was linear in day-answers; 008 did not because its scale was two
orders of magnitude below what 007 measured. This feature adds one indexed equality predicate to
queries that already meet their budgets, and reduces the number of rows each one returns. The scale
test is still written (SC-010), because "should be faster" is not a measurement.

**Name uniqueness (FR-002) is enforced by the index, not only by the service**, mirroring the
decision recorded for `WishItems(WishListId, Name)`: "so a second write path cannot quietly create
the duplicate". As there, the column takes `NOCASE` and the service compares with
`OrdinalIgnoreCase`, leaving the service stricter than the index beyond ASCII — the safe direction.
