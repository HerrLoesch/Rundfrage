# Implementation Plan: Ersteller-Links (Creator Links)

**Branch**: `009-creator-links` | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/009-creator-links/spec.md`

## Summary

A third kind of person. Today the installation holds an operator who can do everything and
participants who can answer one thing; this feature adds the **Ersteller** — someone the operator
names and hands one unguessable link to, who creates their own date polls and wish lists through it,
sees only their own, and can touch nothing that configures the installation. The operator keeps
seeing all of it, now with an owner beside every row.

Technically this is **one new table, two nullable columns, one migration, one new access filter and
one new public route**. The proportion of reuse is the point: the capability token, the neutral 404,
the immediate write transaction, the maintenance gate, the rate limiter, the admin shell, both
projections and every poll and wish-list form already exist and are used as they are. What is
genuinely new is small — ownership, and the scope that enforces it.

**Three things in this plan deserve attention before anyone writes code.**

First, **isolation is structural or it is nothing**. `OwnerScope` (research R-1, data-model §5) is
built the way `RetentionService.LivePolls()` was built and for the same reason: creator handlers are
not given `RundfrageDbContext` at all, so a forgotten filter is a compile error rather than a
cross-tenant leak. SC-003 is the requirement that cannot be satisfied by care.

Second, **two framework defaults would silently produce the wrong behaviour** and both are called
out rather than left to be discovered. EF Core's optional-relationship default (`ClientSetNull`)
would make deleting an Ersteller *hand its content to the operator* — the exact outcome the spec's
Q1 rejected (R-4). And mounting creator routes under the admin group would grant them the
maintenance-mode exemption that group carries (R-6). Neither is a bug that shows up in normal use.

Third, **this feature deliberately contradicts feature 007 on addressing**, and the plan must not
tidy that away. 007 gave every admin destination its own address; the creator surface has exactly
one, because here the address carries the credential (spec Q5, R-9). A reviewer will see the
asymmetry; the reasoning is recorded in three places so they find it before they "fix" it.

**One refactor is in scope and is declared rather than smuggled in**: the immediate-write-transaction
dance reaches its second caller (R-11), which is exactly the threshold Principle III sets. It is
lifted into one helper with no behaviour change, guarded by the tests its existing caller already
has.

**No open risk gates this design.** Unlike 007, no spike is needed: this feature adds one indexed
equality predicate to queries that already meet their budgets, and reduces the rows each returns
(R-14). The scale test is written anyway, because "should be faster" is not a measurement.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); TypeScript 5.9 for Vue 3.5
**Primary Dependencies**: Vue 3.5.42, Vuetify 4.2, Pinia 4.0.3, Vue Router 5.3.1, vue-i18n 11.4.10,
Vite 8.2.2; ASP.NET Core on .NET 10 with EF Core 10.0.11 (`Microsoft.EntityFrameworkCore.Sqlite`),
Serilog.AspNetCore 10.0.0. **No new dependency on either side.**
**Storage**: SQLite, single file on a mounted volume. **One migration** (`AddCreators`): one new
table, two nullable FK columns with explicit `ON DELETE CASCADE`, four indexes. No data migration —
FR-013 is satisfied by the column being nullable (research R-2, data-model §3)
**Testing**: Vitest 4.1 + Vue Test Utils 2.5 (frontend unit/component); xUnit (backend unit and
integration, via the existing `ApiFactory`/`SqliteFixture`); Playwright (end-to-end)
**Logging**: Serilog to stdout. New statements carry the creator id and counts only — never the
token, never the name (R-13, FR-037, SC-013)
**Target Platform**: Linux server backend, modern browsers
**Performance Goals**: SC-010 — an Ersteller's own lists on screen within two seconds at 100
Ersteller sharing the installation-wide scales of 007 FR-028c and 008 FR-054; the operator's areas
stay inside the budgets those features already set
**Constraints**: No request an Ersteller can construct returns content they do not own (FR-033,
SC-003); "not yours" is byte-identical to "no such thing" (FR-034); no route reachable with a
creator token accepts an uploaded file (FR-028a, SC-004); exactly one address carries the creator
token (FR-028d, SC-011a); revocation destroys nothing (FR-020, SC-006a); no creator token in any log
line (FR-037, SC-013)
**Scale/Scope**: At most 100 Ersteller, enforced (FR-009) — the only new enforced maximum. No
per-Ersteller content quota (FR-036a). Creator writes: 60/hour per request source (FR-036).
Backend: 1 entity, 1 migration, **13 paths carrying 20 operations** (contracts/openapi.yaml), 3
registered types (`CreatorService`, `OwnerScope`, `CreatorProjection`). Frontend: 2 routes,
~4 components, 2 stores, 1 navigation entry

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against `.specify/memory/constitution.md` (v2.0.1). **Pre-Phase-0: pass.
Post-Phase-1: pass.** Complexity Tracking below is empty.

- [x] **I. Zero-Signup Participation**: Nothing is added between a survey link and the answer form.
      The participant surfaces are untouched (FR-048), and the end-to-end participant tests pass
      unchanged (SC-008). The Ersteller is a *creator*, and the constitution is explicit that
      creators may be asked to authenticate while participants may not — so this feature sits on the
      permitted side of that line. It goes further than the constitution requires: the Ersteller has
      no account either, only a link-scoped secret, which is the mechanism Principle I names for
      capabilities that cannot identify a person.
- [x] **II. Test-First Development**: Every behaviour change is ordered test-first in `tasks.md`.
      Four suites are named in quickstart.md as the ones to write before the code they describe;
      `CreatorIsolationTests` is first because it defines what the feature is for.
- [x] **III. Simplicity & YAGNI**: No new project, service boundary, layer or dependency. One new
      table where a second (an "operator" row) was rejected on measurement of its consequences
      (R-2). The one abstraction extracted is extracted on its **third** concrete use, which is the
      threshold this principle sets rather than an anticipation of one (R-11). A global query
      filter, an authentication handler and a discriminator column were each rejected for being
      machinery beyond the requirement (R-1, R-2).
- [x] **IV. Data Minimization & Operator-Controlled Storage**: The Ersteller row holds a name, a
      token and a creation date — no password, no email, no contact detail, no last-seen, no usage
      counter, no audit trail (data-model §1). The request source used for the new rate limit is
      held in memory and written nowhere (FR-036d). The retention outcome is unchanged and explicit:
      a poll still dies on its deadline, a wish list still dies only when somebody deletes it, and
      FR-020a adds a second operator-reachable deletion path rather than removing one. No
      third-party asset is introduced; `Referrer-Policy` is *tightened* (R-10).
- [x] **Technology Constraints**: Vue 3 + Vuetify + Pinia + Vite, ASP.NET Core on .NET 10, SQLite on
      a mounted volume, Serilog, Vitest/xUnit/Playwright. Nothing outside the pinned list.

**One consequence is worth naming under Principle IV rather than leaving implicit.** FR-040 gives
the operator parity over an Ersteller's content, and FR-040b refuses both a notification and an
audit trail. That is *less* recorded, not more, which is what this principle asks for — but it means
an Ersteller cannot discover that their list was edited. The spec accepts this in writing; the plan
does not soften it.

## Project Structure

### Documentation (this feature)

```text
specs/009-creator-links/
├── plan.md              # This file
├── research.md          # Phase 0 — R-1 … R-14
├── data-model.md        # Phase 1 — the table, the two columns, OwnerScope, the lifecycle
├── quickstart.md        # Phase 1 — developer guide, and the five rules that are easy to break
├── contracts/
│   ├── openapi.yaml     # Phase 1 — the new and changed endpoints
│   └── ui-contract.md   # Phase 1 — areas, surfaces, states, test ids
├── checklists/
│   └── requirements.md  # from /speckit-specify and /speckit-clarify
└── tasks.md             # Phase 2 output — NOT created by /speckit-plan
```

### Source code (repository root)

New files are marked **new**; everything else is an existing file this feature edits.

```text
backend/src/Rundfrage.Api/
├── Data/
│   ├── Entities/
│   │   ├── Creator.cs                       # new — the one new entity
│   │   ├── Poll.cs                          # + CreatorId (nullable)
│   │   └── WishList.cs                      # + CreatorId (nullable)
│   ├── Migrations/…_AddCreators.cs          # new — one migration, no data migration
│   ├── RundfrageDbContext.cs                # + Creators, indexes, EXPLICIT cascade (R-4)
│   └── RestoreService.cs                    # + creator counts in the preview (FR-052)
├── Creators/                                # new folder — a sibling of Polls/ and Wishes/
│   ├── CreatorService.cs                    # new — create, rename, reissue, revoke, delete
│   ├── OwnerScope.cs                        # new — THE access filter (R-1)
│   └── CreatorProjection.cs                 # new — CreatorSummary rows
├── Endpoints/
│   ├── Admin/CreatorAdminEndpoints.cs       # new — /admin/creators/**
│   ├── Admin/PollAdminEndpoints.cs          # + owner on PollSummary
│   ├── Admin/WishListAdminEndpoints.cs      # (unchanged; owner arrives via the projection)
│   └── Creator/CreatorEndpoints.cs          # new — /e/{creatorToken}/**
├── Polls/PollService.cs                     # + owner on create; PollSummary gains owner fields
├── Polls/PollImport.cs                      # an imported poll is the operator's (FR-053)
├── Wishes/WishListService.cs                # + owner on create
├── Wishes/WishListProjection.cs             # + owner-scoped overload (R-8)
├── Data/SqliteWriteTransaction.cs           # new — the extracted helper (R-11)
├── Http/RateLimiting.cs                     # + CreatorWritePolicy at 60/hour (R-5)
└── Program.cs                               # + DI, + the /e group, + Referrer-Policy (R-10)

frontend/src/
├── components/
│   ├── admin/AdminNav.vue                   # + the "Ersteller" entry
│   ├── admin/CreatorsView.vue               # new — the Ersteller area
│   ├── admin/PollList.vue                   # + owner column
│   ├── admin/PollAnswersView.vue            # + owner
│   ├── admin/WishListsView.vue              # + owner column
│   ├── admin/WishListDetailView.vue         # + owner
│   ├── admin/DashboardView.vue              # + owner on the wish-list overview rows
│   └── creator/CreatorSurface.vue              # new — the whole creator surface, one page
├── stores/creators.ts                       # new — the operator's view of Ersteller
├── stores/creator.ts                        # new — the holder's own two lists
├── api/client.ts                            # + the creator calls
├── router.ts                                # + /admin/ersteller, + /e/:creatorToken (ONE route)
└── locales/de.json                          # + creator.*, + nav.creators

backend/tests/
├── Rundfrage.Api.IntegrationTests/
│   ├── OwnerScopeTests.cs                   # new — the filter itself, before any endpoint (R-1)
│   ├── CreatorIsolationTests.cs             # new — the same promise at route level
│   ├── CreatorSurfaceTests.cs               # new — token resolution and the neutral 404
│   ├── CreatorCreationTests.cs              # new — create, edit, delete and export one's own
│   ├── CreatorRevocationTests.cs            # new — revoke destroys nothing; delete destroys right
│   ├── CreatorSurfaceRefusalTests.cs        # new — no upload, no admin, no dashboard, no session
│   ├── CreatorLimitTests.cs                 # new — 100/101, and the concurrent case (R-11)
│   ├── CreatorMaintenanceTests.cs           # new — the prefix is gated (R-6)
│   ├── CreatorRateLimitTests.cs             # new — 60/hour, separate bucket from submissions (R-5)
│   ├── CreatorLoggingTests.cs               # new — no token, no name, no actor (R-13)
│   ├── MigrationOnPopulatedDatabaseTests.cs # new — FR-013, SC-009
│   ├── CreatorScaleTests.cs                 # new — SC-010
│   ├── AdminAuthorizationTests.cs           # + the five /admin/creators routes (FR-010)
│   └── RestoreTests.cs                      # + the creator counts and a link round-trip
└── Rundfrage.Api.UnitTests/
    └── CreatorValidationTests.cs            # new — name, uniqueness, the cap's refusal text

frontend/tests/unit/
├── CreatorSurface.spec.ts                   # new — the holder's page
├── CreatorsView.spec.ts                     # new — the Ersteller area
├── AdminNav.spec.ts                         # + the "Ersteller" entry, settings still last
└── adminRoutes.spec.ts                      # + /admin/ersteller
e2e/                                         # the hand-walk of quickstart.md, as a Playwright spec
```

**Structure Decision**: the existing web-application layout is kept exactly as it is. The backend
gains one folder, `Creators/`, as a **sibling** of `Polls/` and `Wishes/` — not an abstraction over
them. That mirrors the decision `Program.cs` already records for feature 008: "A sibling of the poll
services, not an abstraction over both: they share mechanisms — tokens, the write transaction, the
shell — and no behaviour (Principle III)." Ownership is a third such mechanism, and `OwnerScope` is
where it lives; neither `PollService` nor `WishListService` learns what an Ersteller is beyond
accepting a nullable owner on create.

The frontend gains one folder, `components/creator/`, beside `poll/` and `wish/`, for the same
reason: it is a surface, not a variant of an existing one.

## Phase 2 approach (what `/speckit-tasks` will expand)

Ordered so that each stage is independently testable and the riskiest thing is proved first.

1. **Schema and ownership** — `Creator`, the two nullable columns, the explicit cascade, the
   migration, and `MigrationOnPopulatedDatabaseTests` proving FR-013 against a *populated* database.
2. **`OwnerScope` and isolation** — `OwnerScopeTests` **first**, against the filter directly, then
   the filter. The route-level suite follows in stage 4, but the promise is asserted here, because it
   is the requirement that cannot be retrofitted and the filter is reachable before any endpoint is.
3. **Operator management of Ersteller** — create, rename, reissue, revoke, delete; the cap tested
   before it is written, inside an immediate transaction; the extracted transaction helper.
4. **The creator surface, backend** — the `/e` group, the reused poll and wish-list services, the
   export, the rate-limit policy, and the refusal suites (no upload, no admin, maintenance gated).
5. **The admin surfaces gain an owner** — projections, summaries, dashboard rows.
6. **Frontend** — the Ersteller area, then the creator surface, then the owner columns.
7. **Restore preview and backup** — the creator counts (FR-051, FR-052).
8. **Scale and end-to-end** — SC-010, and the quickstart walk as a Playwright spec.

Stages 1–3 deliver User Story 4 and half of User Story 1; stage 4 completes User Stories 1 and 2;
stage 5 delivers User Story 3.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

**No violations.** No new project, service, layer or dependency is introduced, and the single
abstraction extracted (`SqliteWriteTransaction`, R-11) is extracted on its second concrete use,
which is literally what Principle III asks for — "in response to a second concrete use, not in
anticipation of one".

Two decisions that *look* like complexity and are recorded here so a reviewer does not have to
rediscover the reasoning:

| Looks like | Why it is not | Where it is argued |
|---|---|---|
| A second rate-limiting policy | The number is fixed by FR-036; a policy is the framework's unit for it, and reusing the participant policy was rejected at clarification time because ten writes does not cover adding ten items | research R-5 |
| A creator surface that does not follow 007's addressing rules | 007's rule follows from the operator holding a session; the premise does not hold when the address *is* the credential | spec Q5, research R-9 |
