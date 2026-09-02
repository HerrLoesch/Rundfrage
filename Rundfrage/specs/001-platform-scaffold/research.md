# Phase 0 Research: Platform Scaffold

**Feature**: 001-platform-scaffold | **Date**: 2026-09-02

Twelve questions had to be resolved before the design could be written. Several arise from
requirements that are individually clear but interact — most importantly FR-011 ("the app
must serve when the database is down") against FR-013 ("schema is created automatically at
startup"), and FR-012's 2-second budget against EF Core's retry behaviour.

---

## R-1: What schema does FR-013 create, when the feature has no entities?

**Decision**: Generate an initial EF Core migration that is deliberately **empty**. Applying
it creates only EF Core's own `__EFMigrationsHistory` table. That table is the schema whose
automatic creation FR-013a verifies and whose persistence SC-009 verifies.

**Rationale**: The clarification session settled on a schema-independent `SELECT 1` probe and
removed the `Persistence Probe` entity, which leaves the feature with no domain table. An
empty initial migration is still worth its keep: it establishes the migration pipeline, the
`Migrations/` folder, the startup application path, and the history table — all of which the
first real feature extends rather than invents. The walking skeleton proves the mechanism,
not the payload.

**Alternatives considered**:
- *Introduce a small table anyway to have something to migrate* — rejected. Principle III
  forbids structures without a concrete use, and the clarification explicitly rejected a
  probe table.
- *Skip migrations entirely and add them with the first entity* — rejected. FR-013 and
  FR-013a require automatic schema creation to exist and be tested now; deferring would leave
  the riskiest part of the data path unproven in the very feature meant to prove it.

**Consequence to flag**: SC-009 ("preserves previously stored data") is thin until real
entities exist — the only preserved data is the migration history row. The test asserts that
a restart does **not** re-apply the migration, which is a genuine, if minimal, persistence
proof.

---

## R-2: How can migrations run at startup without breaking FR-011?

**Decision**: Apply migrations in a startup task wrapped in a bounded retry loop (5 attempts,
exponential backoff, ~30 s total). On final failure, log at `Error` and **let the host start
anyway**. The application always serves; the status endpoint reports the database as
unreachable.

**Rationale**: FR-013 wants migrations applied automatically; FR-011 and acceptance scenario
2.2 require the page to render and report failure when the database is down. A conventional
`MigrateAsync()` at startup would throw and kill the host, turning "database down" into
"nothing responds" — which would fail FR-011 and SC-004. Retrying absorbs the ordinary case
(PostgreSQL still accepting its first connections), and swallowing the terminal failure
preserves the required degraded behaviour.

**Alternatives considered**:
- *Gate startup on the database via Compose `depends_on: condition: service_healthy`* —
  rejected. It makes the app unstartable without a database, which is precisely the scenario
  the spec requires to be exercised (see R-6).
- *Apply migrations lazily on first request* — rejected. It makes the first user request
  slow and unpredictable, and spreads schema concerns across the request path.

**Known limitation to document**: if the database is unavailable for the whole startup
window, the schema is not created during that run. The next start applies it. This is
acceptable because no feature data depends on the schema yet, but it must be revisited when
real entities arrive.

---

## R-3: How is the 2-second budget (FR-012) actually enforced?

**Decision**: Three layers, all required:
1. `Timeout=2` in the connection string — bounds *connection establishment*.
2. `CancellationTokenSource(TimeSpan.FromSeconds(2))` passed into
   `Database.ExecuteSqlRawAsync("SELECT 1", ct)` — bounds the whole operation.
3. **No global `EnableRetryOnFailure`** on the DbContext.

**Rationale**: Point 3 is the non-obvious one. Npgsql's connection timeout and EF Core's
command timeout are separate budgets, and an execution strategy with retries multiplies
whichever applies — a probe with retry enabled would blow a 2-second cap regardless of the
other two settings. Because R-2 needs retries at startup, the retry logic is placed
explicitly around the migration call instead of globally on the context. The probe then
inherits no retry behaviour, and the migration keeps the resilience it needs.

**Alternatives considered**:
- *Global `EnableRetryOnFailure` plus a suspended execution strategy for the probe* —
  rejected as needlessly indirect: it configures a behaviour and then works around it at the
  one place it is measured.
- *Rely on the cancellation token alone* — rejected. A blocked TCP connect can outlive a
  cooperative cancellation; the connection-level timeout is the reliable bound.

---

## R-4: FR-010 requires three states, but the backend can only report two

**Decision**: The backend reports exactly two states, `reachable` and `unreachable`. The
third state, `backendUnreachable`, is **derived in the frontend** when the request to
`/api/v1/status/database` itself fails (network error, timeout, or non-2xx).

**Rationale**: A backend that is down cannot report that it is down. Modelling the third
state where it is actually observable keeps the contract honest and makes FR-010's
distinction testable — a Playwright test stops the app container and asserts the third state,
and stops only the database for the second.

**Alternatives considered**:
- *A single boolean plus an error string* — rejected: FR-010 demands three distinguishable
  states, and FR-014 forbids sending error internals to the browser.

---

## R-5: Keeping credentials out of logs (FR-026)

**Decision**: Four concrete measures:
1. The connection string is never logged; only a fixed, non-parameterised message is.
2. Database failures log the exception **type name** and a sanitised message, never the
   exception's full `ToString()` at `Information` or below.
3. The `Npgsql` log source is set to `Warning`, so the provider does not emit connection
   details on its own.
4. A unit test asserts that a log entry produced with a deliberately wrong password contains
   neither the password nor the connection string.

**Rationale**: FR-026 extends the ban to exception messages, which is where credentials
realistically leak. Measure 4 turns the requirement from a convention into something the
suite enforces, per Principle II, and is what makes SC-011's second half verifiable.

**Alternatives considered**:
- *A Serilog destructuring policy that redacts secrets globally* — rejected as an abstraction
  ahead of its use (Principle III): the feature has exactly one place that logs a database
  failure. Worth revisiting when several call sites exist.

---

## R-6: Compose service dependencies and the DB-down test path

**Decision**: `app` declares a plain `depends_on: [db]` (start-order only, no
`condition: service_healthy`). The `db` service *does* declare a healthcheck, used by test
tooling to wait for readiness — not to gate the app.

**Rationale**: Gating app startup on database health would make acceptance scenario 2.2
unreachable: stopping the database would stop the app rather than produce the required failure
display. Start-order-only dependency combined with R-2's retrying startup gives both the
normal path and the degraded path. The healthcheck is still valuable so end-to-end tests can
wait deterministically instead of sleeping.

---

## R-7: Running the pipeline within the SC-006 budget

**Decision**: One workflow, three parallel jobs — `backend` (xUnit, incl. Testcontainers),
`frontend` (Vitest), `e2e` (Compose up, then Playwright). Triggers: `push` to `dev`/`main`
and `pull_request` targeting them.

**Rationale**: SC-006 caps feedback at 10 minutes. The e2e job is the long pole because it
builds images; running the two fast suites alongside it rather than behind it keeps the wall
clock near the e2e duration. GitHub's `ubuntu-latest` runners provide a working Docker daemon,
which both Testcontainers and Compose require — and being GitHub-hosted, they satisfy FR-020
(no manually provisioned infrastructure). Any suite reporting a failure fails the whole
workflow, which is what SC-007 measures. FR-018 forbids publishing images, so the e2e
job builds locally and pushes nothing.

**Alternatives considered**:
- *A single sequential job* — rejected: it serialises ~3 suites behind an image build and
  risks the 10-minute budget for no benefit.
- *Reusing a prebuilt image from a registry* — rejected: FR-018 explicitly forbids image
  publishing in this feature.

---

## R-8: Where the repository root must be

**Decision**: Flatten. Move `.specify/`, `.claude/`, `CLAUDE.md` and `specs/` from
`/Users/hendrik/repos/Rundfrage/Rundfrage` up into `/Users/hendrik/repos/Rundfrage`, making
the git root and the project root the same directory. Source, CI, and specs then share one
root.

**Rationale**: `specify init` was run with `"here": false`, creating a subdirectory inside the
existing repository. Two concrete breakages follow. First, **GitHub Actions only reads
`.github/workflows/` at the git repository root** — placed in the nested folder, the FR-016
pipeline would simply never trigger. Second, every Spec Kit git hook already fails silently,
because `has_git()` in `.specify/extensions/git/scripts/bash/git-common.sh:9` tests for `.git`
beside `.specify`, which is one level too deep; that is why branch creation and branch
validation have been skipped in every command so far.

**Alternatives considered**:
- *Keep the nesting, place `.github/` at the true git root, and prefix every CI step with
  `cd Rundfrage`* — works for CI, but leaves the hook breakage unfixed and bakes a path seam
  into every workflow, script, and future contributor's mental model.
- *Re-initialise git inside the nested folder* — rejected: it abandons the existing history
  and leaves a stray outer repository.

**Requires confirmation before implementation.** This moves files the user created.

---

## R-9: The status contract must stay language-neutral

**Decision**: `GET /api/v1/status/database` returns a machine-readable state token
(`reachable` / `unreachable`) plus a timestamp. It returns no human-readable German text. The
frontend maps the token to a translation key (`status.database.reachable`, etc.).

**Rationale**: This satisfies three requirements at once — FR-014 (no internals to the
browser), FR-029 (no literal user-facing strings in components), and FR-030 (tests assert on
keys and identifiers, not translated text). It also keeps the API usable by a future
second-language frontend without change, which is the only way the i18n deviation pays off.
SC-012 is then checkable by scanning components for literals, since none can legitimately
exist.

---

## R-10: Frontend live reload without a third container (FR-003b)

**Decision**: Vite dev server proxies `/api` to the containerised app
(`server.proxy: { '/api': 'http://localhost:8080' }`). Compose still runs exactly two
services.

**Rationale**: The proxy makes the browser see a single origin in development too, so FR-003a
holds in both modes and no CORS configuration is ever needed. The container count in FR-003
and SC-010 is unaffected, because the dev server is a developer tool, not part of the started
system.

---

## R-11: Working defaults without a manual setup step

**Decision**: Compose supplies defaults inline via `${POSTGRES_PASSWORD:-rundfrage_dev}`-style
substitution, so `docker compose up` works on a fresh clone with **no `.env` file present**.
`.env.example` is committed as documentation; `.env` is git-ignored for overrides.

**Rationale**: FR-001 and SC-002 require zero manual steps. The common pattern of "copy
`.env.example` to `.env` before starting" is exactly the manual step SC-002 sets to zero, so
the defaults have to live in `compose.yaml` itself. FR-005 is still satisfied: the committed
values are non-production development defaults, and no real secret is committed.

---

## R-12: Test layering across three suites

**Decision**:
- **xUnit unit** — probe timeout behaviour, state mapping, log redaction (R-5 measure 4).
- **xUnit integration** — both endpoints via `Mvc.Testing`; schema creation against an empty
  Testcontainers database (FR-013a); the database-unreachable response path.
- **Vitest** — the status component's three visual states and the store's failure mapping,
  asserted via `data-testid` and translation keys (FR-030).
- **Playwright** — the full chain against Compose: happy path, database stopped, database
  restored, and app stopped (the third state from R-4).

**Rationale**: FR-021 requires end-to-end coverage against the real container set, but routing
every failure case through Playwright would be slow and flaky. Putting failure paths in fast
in-process tests and reserving Playwright for genuine whole-system journeys keeps SC-006's
10-minute budget realistic while satisfying SC-008's three named cases.
