# Phase 0 Research: Wunschliste (Wish List)

**Feature**: 008-wishlist | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md)

Every decision below is recorded because a later reader would otherwise have to rediscover why the
obvious alternative was not taken. Where an existing feature already settled the same question, the
decision is to reuse its answer rather than invent a second one — and the entry says so.

---

## R-1: Capacity under concurrency (FR-017, FR-017a, SC-002)

**Decision**: Enforce an item's capacity the way feature 002 enforces the 1000-response limit —
count and insert inside one *immediate* SQLite write transaction, begun with
`BeginTransaction(IsolationLevel.Serializable, deferred: false)`, exactly as
`ResponseService.SubmitAsync` does today. The claim service reuses that helper verbatim.

**Rationale**: SQLite has one writer at a time, so the transaction *is* the lock — but only if it
takes the write lock at its first statement. A deferred transaction takes a read lock and asks to
upgrade at the insert, and that upgrade cannot wait: the second writer is refused outright whatever
the busy timeout says (003 research R-2). Beginning immediately turns the race into a queue. This is
a solved problem in this codebase; solving it a second way would mean two places where the same
class of bug can appear.

**Alternatives considered**:

- *A unique index on (WishItemId, Slot)* with an explicit slot number per claim. It would enforce
  capacity in the database, but it needs a slot allocator, and freeing a slot on withdrawal (FR-022a)
  turns into hole-filling. Capacity is a count, not an identity.
- *Optimistic concurrency with a row version on the item.* A retry loop in the participant path for
  a race that a queue already resolves. Principle III.
- *Checking capacity outside a transaction.* Exactly the defect SC-002 tests for, with 100
  simultaneous submissions.

---

## R-2: One personal link per submission, without a submission table (FR-022, FR-022b)

**Decision**: `WishClaim` carries a `ClaimToken` column, non-unique, indexed. Every claim created by
one submission receives the *same* freshly minted token; the personal link
`/z/{claimToken}` resolves to "the claims carrying this token". There is **no** submission entity
and no second table.

**Rationale**: FR-022b's requirement is *"one link covers exactly the entries of this submission"*,
which is a grouping, not a lifecycle. A grouping is a shared column. A table would add a row that
exists only to own a token, a foreign key on every claim, a cascade rule and a migration — for a
concept with no attributes of its own beyond the token and the moment, both of which the claim
already needs. Principle III asks for the second concrete use before the abstraction; there is none.

**Consequences, stated**: the token repeats across the rows of one submission (22 characters each),
and "the submission" exists only as a query. Deleting the last claim of a group makes the link resolve
to nothing, which is the edge case the spec already names (a personal link whose entry is gone).

**Alternatives considered**:

- *A `WishSubmission` table* (the literal reading of the spec's "Submission" entity). Rejected as
  above. The spec's Key Entities section describes a concept, not a table; the data model maps it to
  a column and says so.
- *A token per claim.* Contradicts FR-022b — a participant taking three items would keep three links.

---

## R-3: "Closed" is derived, never stored (FR-028a, FR-028c)

**Decision**: No status column. A wish list is closed when `TargetDate` has ended, evaluated on
every access against `BerlinClock`. `BerlinClock` gains one member —
`EndOfDayUtc(DateOnly)` — and `IsClosed` is `clock.Now > clock.EndOfDayUtc(list.TargetDate)`. The
existing `RetentionDeadlineFor` is refactored to use it, so there is one expression for "when does
this day end in Europe/Berlin" rather than two.

**Rationale**: This is the same decision `Poll.RetentionDeadline` already documents — *"there is
deliberately no status column beside it… a stored flag would be wrong for as long as its writer
lagged behind the deadline"*. FR-028c requires it explicitly, and it is what makes FR-028c's reopen
rule free: moving the target date reopens the list with no state to fix up.

**Consequence**: closedness is computed per request and cannot be indexed. Irrelevant at the scale of
FR-054 — the comparison is on a column already loaded for display, and the overview's ordering
(R-7) sorts on `TargetDate` itself, which *is* indexable.

**Alternatives considered**:

- *A `ClosedAt` column maintained by a background job.* A second writer for a value the clock already
  knows, wrong between the deadline and the sweep, and a migration hazard when the target date moves.
- *Filtering closed lists out of reads,* the way `RetentionService.LivePolls()` filters expired polls.
  Wrong here: a closed list stays fully readable (FR-028b). Closedness gates two write paths
  (claim, withdraw) and nothing else.

---

## R-4: The capacity cap is an operator-side rule only (FR-010, FR-010a)

**Decision**: The 1000 wished-places cap is validated in the wish-list write paths the operator
uses — create list, add item, raise wanted count — and nowhere else. The participant claim path
never evaluates it. Per-item capacity (R-1) is the only thing that can refuse a claim, alongside a
closed list and the rate limit.

**Rationale**: FR-010a says so, and the clarification behind it explains why: a limit the operator
cannot see until a participant hits it is a refusal nobody can act on. Enforcing it once, where it
is raised, also means one validation function (`WishListService.Validate`) rather than a check
duplicated into the hot path.

**Alternatives considered**: *A check-constraint or trigger.* The codebase has none; validation is
in the service layer with the refusal codes the interface renders into German (`ErrorCodes`).

---

## R-5: The dashboard reuses the wish-list list endpoint (FR-048a, FR-049)

**Decision**: One projection, two readers. `GET /api/v1/admin/wish-lists` returns the summary rows
(title, target date, closed, entries, places, untaken items, complete items), ordered as FR-048c
requires. The wish-list area renders them as its list; the dashboard renders the same payload as its
overview. The dashboard therefore makes two requests: the existing `GET /admin/dashboard` for the
poll figures, and this one.

**Rationale**: FR-049 demands the dashboard and the area agree. Two code paths producing "the same"
figures is how they stop agreeing; one projection makes agreement structural rather than tested-for.
It also keeps 007's `DashboardView` record flat and unchanged — the comment in `DashboardProjection`
explains why nothing nested lives there, and that reasoning survives intact.

**Consequence, accepted**: the dashboard now performs two reads, so one can fail while the other
succeeds. That is handled rather than avoided: each region of the dashboard carries its own
empty/unreadable state (FR-052), which the spec requires per region anyway. 007's "one route, one
read" rationale was about not splitting *one* set of figures across six calls; this is a second,
independent set.

**Alternatives considered**:

- *Extend `DashboardView` with a `wishLists` array.* One request, but the dashboard and the area then
  have separate projections to keep in step, which is the FR-049 risk. Also breaks the flat-record
  property 007 documented as checkable at a glance.
- *A dashboard-specific wish-list endpoint.* Two projections for one payload. Same risk, more code.

---

## R-6: Amending feature 007's FR-034 (FR-048b)

**Decision**: Edit `specs/007-admin-shell-dashboard/spec.md` in this feature's branch: FR-034 keeps
its rule and gains a narrowing clause plus a dated amendment note naming 008 FR-048b. Nothing else in
007 changes, and 007's delivered behaviour is untouched.

**Rationale**: The constitution's governance section is explicit — *"When a template or command file
drifts from this constitution, the constitution is corrected first and the dependent artifacts are
brought back into line in the same change."* The same principle applies between features: a
requirement that the new feature contradicts must be corrected in the same change, not left as a
contradiction for the next reader to discover. This is a task, not a footnote (see plan T-series).

**Alternatives considered**: *Leave 007 as written and note the exception only in 008.* Produces two
documents that disagree about the dashboard, with nothing saying which wins.

---

## R-7: The filled share — counts on the wire, formatting in the interface (FR-045, FR-045a)

**Decision**: The API returns integers — `entryCount`, `placeCount`, `untakenItemCount`,
`completeItemCount` — and never a percentage. The interface computes the share for display and
**rounds down**, so 999 of 1000 reads 99 % and only a genuinely complete list reads 100 %.
"Complete" is `entryCount == placeCount`, evaluated as a comparison, not from the rounded percentage
(FR-045b).

**Rationale**: A rounded percentage on the wire cannot be checked against a hand count, which SC-004
requires in 100 % of cases. Rounding down is the honest direction: a list one name short must not
present as finished. Deriving "complete" from the counts rather than from the percentage means the
label cannot disagree with the number beside it.

**Alternatives considered**: *Server-side percentage.* Loses the counts the tests compare against and
puts a presentation decision behind the API boundary.

---

## R-8: Addresses and naming (FR-041, FR-055)

**Decision**:

| Surface | Address | Note |
|---|---|---|
| Wish-list area | `/admin/wunschlisten` | Middle nav section, after `terminfindungen`, before `einstellungen` |
| One wish list | `/admin/wunschlisten/:wishListId` | Own address, reloadable (FR-043) |
| Participant | `/w/:listToken` | Beside `/u/:pollToken` |
| Personal link | `/z/:claimToken` | Beside `/a/:editToken` — *z* for Zusage |
| API (admin) | `/api/v1/admin/wish-lists…` | Kebab-case, matching `/api/v1/admin/polls` |
| API (participant) | `/api/v1/wish-lists/{listToken}`, `/api/v1/claims/{claimToken}` | Anonymous |

Code identifiers are English (`WishList`, `WishItem`, `WishClaim`), interface strings are German and
live in `locales/de.json` under a new `wish.*` block plus `error.*` additions. The navigation label
is `nav.wishLists` = "Wunschlisten".

**Rationale**: German addresses for operator-facing routes continue 007 R-5; single-letter
participant prefixes continue 002. English code with German strings is the existing split and is why
`Poll` and "Terminfindung" coexist without a glossary.

---

## R-9: Rate limiting reuses the existing policy (FR-023, FR-023a)

**Decision**: Apply the existing `RateLimiting.SubmissionPolicy` to the claim and withdrawal routes.
No new policy, no new partition, no new configuration variable.

**Rationale**: FR-023 asks for "at most 10 per hour per request source", and one shared partition
satisfies that by construction. The policy already holds the source in memory only, which is what
FR-023's privacy half requires; a second limiter would be a second place to get that wrong.

**Consequence, stated**: poll submissions and wish-list claims share one budget. A participant who
answers a poll ten times in an hour cannot then claim a cake. This is compliant — "at most 10" — and
the shared budget is the simpler of the two honest readings. `SUBMISSION_LIMIT_PER_HOUR` already
exists for the e2e environment and covers the new routes with no change.

**Alternatives considered**: *A separate `claims` policy with its own ten.* Twenty per hour per
source in total, two partitions to reason about, one more environment variable, no requirement
asking for it.

---

## R-10: The restore preview must count wish lists (integration defect, not a new feature)

**Decision**: Extend `RestoreService`'s preview counts and its backup-shape check to include wish
lists: the preview reports wish lists and claims in the backup and lost, beside the existing polls
and responses. The restore mechanism itself does not change — it already replaces the whole file.

**Rationale**: This is the one place where adding a feature *breaks an existing promise* rather than
merely adding to it. 005 FR-018 makes the operator confirm a restore against a statement of what it
will destroy. Once wish lists exist, that statement is silently short: it would say "you lose 3
polls" while also destroying twelve wish lists nobody was told about. The whole-file restore is
correct; the preview's arithmetic is what goes wrong.

**Scope note, honestly**: 008's Out of Scope excludes "changes to backup or restore beyond adding the
navigation entry and the dashboard report". This change is inside that exclusion by its literal
wording and outside it by its intent — the exclusion exists to stop the feature redesigning restore,
not to license a misleading confirmation. It is recorded here, carried as its own task, and flagged
to the operator of this plan rather than smuggled in.

**Alternatives considered**: *Leave the preview as it is and document the gap.* Rejected: the gap is
visible only to somebody who reads this file, and its cost is data destroyed after a confirmation
that understated the loss.

---

## R-11: Schema, migration and cascade (data-model.md)

**Decision**: One EF Core migration adding three tables — `WishLists`, `WishItems`, `WishClaims` —
with cascade deletes down the chain, a unique index on `WishLists.ListToken`, a unique index on
`(WishListId, Name)` for items (FR-008), a non-unique index on
`WishClaims.ClaimToken` (R-2), and a non-unique index on `WishLists.TargetDate` for the overview's
ordering. No index on `DisplayName` and no uniqueness on it — deliberately, exactly as
`PollResponse.DisplayName` documents.

**Rationale**: Mirrors the existing model's conventions (GUID v7 keys, `HasMaxLength` from entity
constants, cascade from owner to owned). The unique index on item names enforces FR-008 in the
database rather than only in the code that happens to check.

**Note on retention**: wish lists are deliberately **not** added to `RetentionService`. Its
`LivePolls()` filter and its erasure sweep stay poll-only, because FR-037 forbids any automatic
erasure of wish lists. An integration test asserts that the sweep leaves wish lists alone — the
cheapest way to notice if somebody later "tidies up" by generalising the sweep.

---

## R-12: Per-list figures in one grouped query (FR-054, SC-009)

**Decision**: The overview projection computes every list's figures with a fixed number of aggregate
queries (grouped counts over `WishItems` and `WishClaims`), never one query per list. Same rule 007
FR-028b imposed on the dashboard, for the same reason.

**Rationale**: At FR-054's scale — 200 lists, 200,000 claims — a per-list query is 200 round trips
inside one request. The figures are counts over two small tables; SQLite aggregates them in one pass.

**Measurement**: unlike 007's SC-011, no measurement task is needed before design: 200,000 rows is
two orders of magnitude below the 2.5 million day-answers 007 measured at 0.66 s (007 research R-4),
and these are counts rather than a group-by over a wider table. A scale test is still written
(quickstart names it) so the claim is asserted rather than assumed.

---

## R-13: Maintenance mode and the neutral 404 come for free — and are tested anyway (FR-024, FR-025)

**Decision**: Add no maintenance check and no shape check to the new participant routes. The existing
middleware intercepts everything under `/api/` that is not `/api/v1/admin` or `/api/v1/health`, and
the new routes inherit that. Token lookups follow `PollEndpoints`: no well-formedness short-circuit
before the lookup, and `NeutralNotFound.Result()` for every miss.

**Rationale**: Both behaviours are already properties of the pipeline rather than of each handler,
which is precisely why they were built that way ("a per-handler check is a rule somebody eventually
forgets to repeat"). Inheriting them is the point of the design.

**Tested anyway**: two integration tests — a claim refused with `{"code":"maintenance"}` while
maintenance is on, and an unknown/malformed/deleted list token producing one identical response —
because "it is inherited" is a claim about wiring, and wiring is what regresses.

---

## R-14: Test strategy (Principle II)

- **Backend unit** (xUnit): validation rules (title, description, item name, wanted count, distinct
  names, wished-places cap), the closed-derivation around the Europe/Berlin day boundary including a
  summer-time transition, and the filled-share counts.
- **Backend integration** (xUnit, `ApiFactory` + `SqliteFixture`): the claim path end to end,
  capacity refusal, **the concurrency test with 100 simultaneous claims for one place** (SC-002,
  modelled on `ConcurrentWriteTests`), editing rules including the refused lowering, deletion,
  withdrawal through the personal link, the personal link granting nothing else, the neutral 404,
  maintenance refusal, the retention sweep leaving wish lists alone, and the overview scale test.
- **Frontend unit** (Vitest + Vue Test Utils): the participant page's open/full/closed rendering, the
  navigation entry and its `aria-current` (following `AdminNav`'s existing tests), the share
  formatting and the "complete" label, the empty vs unreadable states.
- **End-to-end** (Playwright): one `wish-list-journey.spec.ts` covering create → share → claim from a
  bare link with no session → capacity refusal → withdraw → edit → status in the area → the
  dashboard overview; plus an assertion in the existing zero-signup spec that a wish-list link shows
  no navigation and no route into the admin area.

Every task in `tasks.md` is ordered test-first; the concurrency test and the capacity code are the
pair to write first, because they are the requirement that cannot be retrofitted.
