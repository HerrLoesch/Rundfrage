# Implementation Plan: Date Poll (Terminfindung)

**Branch**: `dev` (feature directory `002-date-poll`) | **Date**: 2026-09-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/002-date-poll/spec.md`

## Summary

Rundfrage's first product feature, built on the 001 scaffold: a Doodle-style date poll. A single
operator signs in to a protected admin area and creates a poll — title, short message, candidate
days. Anyone holding the resulting link opens it and marks each day *Ja*, *Vielleicht* or *Nein*
without an account, a login, or an email address.

The technical shape follows from four requirements that pull hardest:

- **Principle I** forbids identifying participants, so every participant capability is a
  capability *token* in a URL: one token grants sight of a poll and the right to answer it,
  another grants the right to revise one specific response.
- **FR-027 / FR-040 / SC-012** require unknown, malformed, expired and deleted links to be
  byte-identical. That is one shared code path, not four handlers.
- **FR-039b** makes expiry take effect on access rather than when a job runs, so every read is
  filtered by the retention deadline and the background erasure becomes a separate, purely
  physical concern.
- **FR-011a** fixes day boundaries to Europe/Berlin, which has a container consequence the
  scaffold does not currently satisfy (research.md R-6).

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.201) + TypeScript on Node 24 — unchanged from 001
**Primary Dependencies**: ASP.NET Core 10 Minimal APIs, EF Core 10 with Npgsql 10.0.3; Vue 3.5,
Vuetify 4, Pinia 4, vue-i18n 11, Vite 8 — all already present
**Storage**: PostgreSQL 17, self-hosted; this feature introduces the first real schema

**Vuetify is actually used.** The first implementation pass built every component from plain
markup and scoped CSS while Vuetify sat in the bundle unused — 488 KB of stylesheet for zero
components, and a constitution requirement satisfied on paper only. The interface now renders
through Vuetify: cards, forms, tables, dialogs, alerts, one theme with named colours for the
three answer states, and Material Design icons. `vue-router` and `@mdi/font` are the only
additions.
**Testing**: xUnit + Mvc.Testing + Testcontainers (backend); Vitest + Vue Test Utils (frontend);
Playwright against the container set
**Logging**: Serilog, structured to stdout — extended with the eight events of FR-043a
**Target Platform**: Linux containers, modern browsers — still exactly two containers
**Project Type**: Web application, single origin, `/api/v1` prefix (inherited from 001)
**Performance Goals**: results view usable within 5 s at 1000 responses x 100 days (SC-016);
expiry effective within one request of the deadline (SC-031); erasure within 24 h (SC-032)
**Constraints**: no participant identification (Principle I); byte-identical not-found across
four causes (SC-012); no IP persisted with a response (FR-042, FR-027b); no name, answer, token
or request source in any log (FR-043b)
**Scale/Scope**: 100 candidate days, 1000 responses per poll, one operator account

## Constitution Check

Checked against `.specify/memory/constitution.md` (v1.1.1).

- [x] **I. Zero-Signup Participation (NON-NEGOTIABLE)**: No account, login, email or install is
      required to answer. The poll page renders the form on the first load with nothing in
      between (FR-019, FR-021). The two capabilities that cannot work anonymously — revising a
      response, and refusing a flooder — are solved with a per-response token (FR-026, FR-028)
      and a transient in-memory rate limit (FR-027a-c), never by identifying anyone. The display
      name is explicitly a label, not an identity (FR-022).
- [x] **II. Test-First Development (NON-NEGOTIABLE)**: Every behaviour is introduced by a failing
      test first. The three suites from 001 are extended, not replaced.
- [ ] **III. Simplicity & YAGNI**: Two deviations, both justified in Complexity Tracking — the
      inherited i18n layer, and paginating the results grid. Every other addition is either
      constitution-mandated or forced by a named requirement.
- [x] **IV. Data Minimization & Operator-Controlled Storage**: Only the display name and the
      answered days are stored (FR-041). Unanswered days are stored as nothing at all
      (research.md R-8). No IP or user-agent is persisted (FR-042); the rate limiter holds a
      request source only in memory for the length of its window (FR-027b). Retention is a real
      erasure, not a hide (FR-039c). All assets stay same-origin (FR-044, verified in 001).
- [x] **Technology Constraints**: Vue 3 + Vuetify + Pinia + Vite, ASP.NET Core on .NET LTS,
      self-hosted PostgreSQL, Serilog, Vitest/xUnit/Playwright. **No new runtime dependency is
      introduced**: cookie authentication, rate limiting, password hashing and the background
      service all come from the framework or the BCL (research.md R-1, R-4, R-5).

**Post-design re-check (after Phase 1)**: Still two deviations, unchanged. The design added no
architectural layer — no repository, no service layer indirection, no mediator, no CQRS split.
Two designs were chosen specifically to *avoid* complexity Principle III would have questioned:
storing absence instead of a fourth enum value (R-8), and native radio groups instead of a custom
tri-state widget (R-11).

## Project Structure

### Documentation (this feature)

```text
specs/002-date-poll/
|-- plan.md              # This file
|-- spec.md              # 76 requirements, 32 success criteria
|-- research.md          # Phase 0 output - 12 decisions
|-- data-model.md        # Phase 1 output
|-- quickstart.md        # Phase 1 output
|-- contracts/
|   `-- openapi.yaml     # Phase 1 output
`-- checklists/
    `-- requirements.md
```

### Source Code (repository root)

```text
backend/src/Rundfrage.Api/
|-- Data/
|   |-- RundfrageDbContext.cs        # gains four entity sets
|   |-- Entities/                    # Poll, CandidateDay, PollResponse, DayAnswer
|   `-- Migrations/                  # second migration, first with real tables
|-- Endpoints/
|   |-- MessageEndpoint.cs           # from 001
|   |-- StatusEndpoint.cs            # from 001
|   |-- Admin/                       # SignInEndpoints, PollAdminEndpoints
|   `-- Public/                      # PollEndpoints, ResponseEndpoints
|-- Security/
|   |-- PasswordHash.cs              # PBKDF2 verify + generate (FR-003, FR-045a)
|   |-- SignInThrottle.cs            # 5 attempts / 15 min (FR-005, FR-005a)
|   `-- CapabilityToken.cs           # 128-bit token mint (FR-017, SC-006)
|-- Polls/
|   |-- PollService.cs               # creation, limits, retention deadline
|   |-- ResponseService.cs           # submit, revise, delete, 1000-cap (FR-015a)
|   `-- ResultsProjection.cs         # per-day totals, paged rows (FR-033, FR-036c)
|-- Retention/
|   `-- RetentionService.cs          # access filter + hosted erasure (FR-039b/c/d)
|-- Time/
|   `-- BerlinClock.cs               # single day-boundary authority (FR-011a/b)
`-- Http/
    `-- NeutralNotFound.cs           # the one 404 (FR-027, FR-040, SC-012)

frontend/src/
|-- components/
|   |-- SystemStatus.vue             # from 001
|   |-- admin/                       # SignInForm, PollForm, PollList, DeleteConfirm
|   `-- poll/                        # PollView, AnswerForm, ResultGrid, ShareLink
|-- stores/                          # status.ts (001), session.ts, polls.ts, answering.ts
|-- locales/de.json                  # every new string (FR-028, FR-029)
`-- router.ts                        # NEW - the SPA gains real routes

e2e/tests/
|-- walking-skeleton.spec.ts         # from 001
|-- database-status.spec.ts          # from 001
|-- zero-signup.spec.ts              # Principle I, FR-047
|-- admin-access.spec.ts             # FR-048
`-- date-poll-journey.spec.ts        # create -> answer -> revise -> delete
```

**Structure Decision**: Extend the 001 application rather than place anything alongside it. The
container count stays at two and the origin stays single, so FR-044 and the inherited SC-010 hold
without further work.

Three groupings are deliberate rather than habitual. **`Security/`, `Time/` and `Http/` each hold
one single-purpose class that many endpoints must agree on** — the token mint, the day-boundary
authority, and the neutral 404. Each exists because a requirement demands one consistent answer
across many call sites, and duplicating any of them is exactly how SC-012 or FR-011a would
quietly break. **`Endpoints/Admin` and `Endpoints/Public` are split** because the two have
opposite access rules: everything under `Admin` requires a session, everything under `Public`
must never require one. Splitting by rule makes FR-048's blanket assertion testable by route
prefix instead of by enumerating handlers. **The frontend gains a router**, which 001 did not
need: this feature has four destinations reachable by URL (sign-in, admin list, poll page,
response revision).

## Requirement Coverage

All 76 requirements and 32 success criteria are accounted for. Most are cited inline above or in
the design artifacts; the rest are grouped here so `/speckit-tasks` has an explicit home for each
rather than inferring one.

**Requirements carried by the contract and data model without being named in prose**

| Requirement | Where it is realised |
|---|---|
| FR-020, FR-025 | `GET /polls/{pollToken}` needs no session and returns the form's data in one request; `POST .../responses` completes in that same session. Asserted by `zero-signup.spec.ts`. |
| FR-023 | The `Availability` enum — exactly three values per day. |
| FR-024a | `ResponseRow.answers` omits unanswered days; the empty cell is a frontend rendering rule tested in Vitest alongside FR-053. |
| FR-031 | No participant route accepts a response id — only `editToken`. The capability *is* the route parameter, so the requirement is structural. |
| FR-034 | `PollView` with `responseCount: 0` and an empty `responses` array; the empty state is a frontend concern. |
| FR-035 | Nothing is cached: every `GET` recomputes totals and rows, so a reload reflects a new response. |
| FR-038 | The confirmation and its response count are a frontend concern; the count comes from `PollSummary.responseCount`. |
| FR-039d | `RetentionService` sweeps idempotently and logs the number removed — the eighth event of FR-043a. |
| FR-054 | Deliberately a *limit* on scope, not a behaviour. It is satisfied by not doing more, and needs no task. |

**Requirements that are verification obligations**

FR-046 (test-first) and FR-049 (a test proving deletion removes responses) are not designed
anywhere — they constrain how every other task is executed. FR-046 is a property of the task
ordering in `tasks.md`; FR-049 becomes a named integration test.

**Success criteria**

SC-006, SC-007, SC-010, SC-012, SC-016, SC-031 and SC-032 shaped the design and are cited above.
The remaining 25 are measurable outcomes rather than design inputs: each becomes a specific
assertion during the verification phase. They fall into four groups —

- **Participant experience** (SC-001, SC-002, SC-003, SC-005, SC-008, SC-009, SC-013): end-to-end
  assertions in `zero-signup.spec.ts` and `date-poll-journey.spec.ts`.
- **Operator access** (SC-004, SC-015, SC-018, SC-019): `admin-access.spec.ts` plus integration
  tests for the throttle and the configuration check.
- **Data and logging discipline** (SC-011, SC-014, SC-020, SC-021, SC-022, SC-023, SC-024):
  integration tests that inspect stored rows and captured log entries directly, in the shape
  feature 001 established with its log-redaction test.
- **Presentation and time** (SC-017, SC-025, SC-026, SC-027, SC-028, SC-029, SC-030): Vitest
  component assertions, including the greyscale checks for SC-026 and SC-029, plus date tests
  fixed at a winter and a summer date for SC-027 and SC-028.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **i18n layer with one language** (`vue-i18n`), carried over from 001 and still a Principle III deviation | FR-029 forbids literal user-facing strings in components. This feature contributes most of the application's text, so the layer is now doing real work rather than guarding two labels. | Literal German strings remain the Principle III-conformant choice. Rejected by the project owner during the 001 constitution round; reversing it now would mean unpicking the layer *and* rewriting every string this feature adds. |
| **Paginating the results grid** before a second concrete use exists | FR-036c and SC-016 require 1000 responses across 100 candidate days — 100,000 cells — to stay usable within 5 s. Some reduction is therefore mandatory, not anticipatory. | Rendering the whole grid was measured against the requirement and fails on size alone. Virtualised scrolling was rejected as the more complex of the two reductions and would need a new dependency; paging the response rows needs neither (research.md R-7). |

Both are confined. No repository pattern, service layer indirection, mediator, or CQRS split is
introduced; `PollService` and `ResponseService` are ordinary classes the endpoints call directly,
matching the shape 001 established. Verified against the built code on 2026-09-03: a scan for
`IRepository`, `MediatR`, `AutoMapper`, `IUnitOfWork` and CQRS markers finds nothing, and the
only runtime dependency added beyond the constitution's stack is `vue-router`.

**As-built deviation not present in the table above.**

| Deviation | Why |
|---|---|
| The submission rate limit is configurable (`SUBMISSION_LIMIT_PER_HOUR`), defaulting to FR-027a's ten | The end-to-end suite submits far more than ten answers per hour from one machine and began failing halfway through a run once the limiter worked. A test environment raises the number explicitly; an unconfigured deployment still gets ten, and an unparseable or non-positive value falls back to ten rather than silently disabling the limit — a configuration typo must not become an open door. |
