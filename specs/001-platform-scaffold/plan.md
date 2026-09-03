# Implementation Plan: Platform Scaffold (Walking Skeleton)

**Branch**: `001-platform-scaffold` | **Date**: 2026-09-02 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-platform-scaffold/spec.md`

## Summary

Build the Rundfrage walking skeleton: a two-container system (`app` + `db`) started by a
single `docker compose up`, where an ASP.NET Core application serves both the `/api/v1`
endpoints and the built Vue 3 assets from one origin, reaches PostgreSQL through Entity
Framework Core, and renders a German page reporting the backend text and the live database
state. Around it: automatic schema creation on startup, Serilog structured logging to
stdout, and a GitHub Actions pipeline that builds and runs every test suite on `dev` and
`main`.

The technical approach is a multi-stage container build (Node builds the frontend, the .NET
publish stage receives `dist/` as `wwwroot/`), a startup path that applies migrations with
bounded retries but never crashes when the database is absent, and a status contract that is
language-neutral — the backend returns a state enum, the frontend maps it to a translation
key.

## Technical Context

**Language/Version**: C# 14 on .NET 10 (LTS; SDK 10.0.201 verified locally) + TypeScript on
Node 24 LTS (24.14.1 verified locally)
**Primary Dependencies**: ASP.NET Core 10 Minimal APIs, EF Core 10 with
Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3; Vue 3.5, Vuetify 4, Pinia 4, vue-i18n 11,
Vite 8 (versions as resolved at implementation time — the constitution pins the libraries,
not their major versions)
**Storage**: PostgreSQL 17 (`postgres:17-alpine`), self-hosted, named volume for persistence
**Testing**: xUnit + Microsoft.AspNetCore.Mvc.Testing + Testcontainers.PostgreSql (backend);
Vitest + Vue Test Utils (frontend); Playwright (end-to-end against the real container set)
**Logging**: Serilog with `Serilog.AspNetCore`, `Serilog.Sinks.TextWriter` and
`Serilog.Formatting.Compact` — structured to stdout, level via `LOG_LEVEL`. The TextWriter
sink replaced the Console sink during implementation: `Console.Out` is itself a TextWriter, so
one sink serves both production and tests, and the tested code path is the production one
**Target Platform**: Linux containers (Docker 29.7.2 / Compose v5.4.0 verified) + modern
browsers
**Project Type**: Web application — single ASP.NET Core host serving API and SPA, plus a
PostgreSQL container
**Performance Goals**: Page renders text and database state within 5 s (SC-003/SC-004);
database check resolves or aborts within 2 s (FR-012, SC-004a)
**Constraints**: Exactly two containers (FR-003); same origin, no CORS (FR-003a); app must
start and serve when the database is down (FR-011); no credentials in logs or browser
(FR-014, FR-026)
**Scale/Scope**: Scaffold only — no domain entities, no users, no survey functionality

## Constitution Check

Checked against `.specify/memory/constitution.md` (v1.1.0). Any unchecked box requires a
justified row in Complexity Tracking below.

- [x] **I. Zero-Signup Participation**: No participant-facing flow exists in this feature; no
      account, login, or gate is introduced. The principle is trivially satisfied and becomes
      binding at the first survey feature.
- [x] **II. Test-First Development**: Every task in `tasks.md` will place the failing test
      before its implementation. Three suites are provisioned so no behaviour lacks a home:
      xUnit (backend), Vitest (frontend), Playwright (end-to-end).
- [ ] **III. Simplicity & YAGNI**: One deliberate deviation — the internationalisation layer
      (FR-029) with a single language. Justified in Complexity Tracking. All other
      dependencies are either constitution-mandated or required by a specific requirement.
- [x] **IV. Data Minimization & Operator-Controlled Storage**: No participant data exists
      yet. All assets are served from the application's own origin (FR-003a) — no CDN, no
      external fonts, no analytics. Data stays in the self-hosted PostgreSQL container.
      FR-026 forbids credentials in logs; FR-014 forbids leaking internals to the browser.
- [x] **Technology Constraints**: Stays within Vue 3 + Vuetify + Pinia + Vite, ASP.NET Core
      on .NET LTS, self-hosted PostgreSQL, Serilog, Vitest/xUnit/Playwright. Three additions
      are justified in Complexity Tracking: vue-i18n, Testcontainers, Mvc.Testing.

**Post-design re-check (after Phase 1)**: Still one deviation. The design did not introduce
further abstraction — no repository pattern, no service layer, no mediator. The database
check is a single function; the two endpoints are minimal API handlers. The empty initial
migration (see research.md R-1) was chosen specifically to avoid inventing a domain table
that Principle III would forbid.

## Project Structure

### Documentation (this feature)

```text
specs/001-platform-scaffold/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── openapi.yaml
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
backend/
├── Rundfrage.slnx
├── src/
│   └── Rundfrage.Api/
│       ├── Program.cs                  # Host, Serilog, EF Core, static files, SPA fallback
│       ├── Endpoints/
│       │   ├── MessageEndpoint.cs      # GET /api/v1/message          (FR-006)
│       │   └── StatusEndpoint.cs       # GET /api/v1/status/database  (FR-009)
│       ├── Data/
│       │   ├── RundfrageDbContext.cs   # No entities yet (research.md R-1)
│       │   └── Migrations/             # Initial empty migration
│       ├── Diagnostics/
│       │   └── DatabaseProbe.cs        # SELECT 1, 2 s budget         (FR-008, FR-012)
│       └── Rundfrage.Api.csproj
└── tests/
    ├── Rundfrage.Api.UnitTests/        # Probe logic, state mapping, log redaction
    └── Rundfrage.Api.IntegrationTests/ # Endpoints via Mvc.Testing; schema via Testcontainers

frontend/
├── src/
│   ├── main.ts
│   ├── App.vue
│   ├── components/
│   │   └── SystemStatus.vue            # Renders text + tri-state           (FR-007, FR-010)
│   ├── stores/
│   │   └── status.ts                   # Pinia: fetch, map failure to state (FR-010)
│   ├── locales/
│   │   └── de.json                     # All user-facing strings            (FR-028, FR-029)
│   └── api/client.ts
├── tests/unit/                         # Vitest + Vue Test Utils
├── vite.config.ts                      # Dev proxy /api -> app:8080         (FR-003b)
└── package.json

e2e/
├── tests/                              # Playwright, against docker compose (FR-021)
└── playwright.config.ts

docker/
└── Dockerfile                          # Multi-stage: node build -> dotnet publish
compose.yaml                            # Exactly two services: app, db      (FR-003)
.env.example                            # Defaults, no real secrets          (FR-005)
.github/workflows/ci.yml                # Build + all suites on dev/main     (FR-016)
README.md                               # Start command, address, states     (FR-023)
```

**Structure Decision**: Web application with a single hosting process. The backend project
owns the origin: it serves `/api/v1/*` itself (FR-006a) and everything else from `wwwroot/`,
which the container build fills with the Vite output. That is what makes FR-003 (two containers) and
FR-003a (one origin, no CORS) true at the same time. During development FR-003b is served by
the Vite dev proxy rather than by a second container, so the container count never changes.
End-to-end tests live at the repository root rather than inside `frontend/`, because they
exercise the whole system and are run against Compose, not against Vite.

**Repository root placement — resolved (T001 executed 2026-09-02).** The tree above is rooted
at the git repository root, `/Users/hendrik/repos/Rundfrage`. The Spec Kit project previously
sat one level below it, at `/Users/hendrik/repos/Rundfrage/Rundfrage`, because `specify init`
was run with `"here": false` and created a subdirectory. That was not cosmetic:

- **GitHub Actions only reads `.github/workflows/` from the git repository root.** Placed in
  the nested folder, the pipeline in FR-016 would never run.
- Every Spec Kit git hook already degrades silently for the same reason (`has_git()` in
  `.specify/extensions/git/scripts/bash/git-common.sh:9` looks for `.git` beside `.specify`).

**The project has been flattened.** `.specify/`, `.claude/`, `CLAUDE.md` and `specs/` were
moved with `git mv` into `/Users/hendrik/repos/Rundfrage`, which is now the single root for
source, specs, and CI. Verified afterwards: `has_git()` returns true, and
`check-prerequisites.sh` runs branch validation instead of skipping it.

That newly-working validation surfaced a second issue. It requires a branch named
`NNN-slug`, but development happens on `dev` (FR-015). The resolution is the tooling's own
escape hatch — `get_current_branch()` in `.specify/scripts/bash/common.sh:49` prefers the
`SPECIFY_FEATURE` environment variable over the branch name. **All Spec Kit commands for this
feature must therefore run with `SPECIFY_FEATURE=001-platform-scaffold`**, which keeps the
`dev` workflow intact without renaming branches. Persist it with
`export SPECIFY_FEATURE=001-platform-scaffold`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| **i18n layer with one language** (`vue-i18n`) — deviates from Principle III, which requires abstractions to follow a second concrete use | FR-029 mandates that no component contains a literal user-facing string. Recorded as a deliberate deviation in the spec's Constitution Deviations section. | Literal German strings in components, introducing i18n with the first real second-language requirement, would be the Principle III-conformant choice. Rejected by the project owner, who judged the cost of retrofitting i18n across an established Vue codebase to outweigh carrying the indirection from the start. |
| **`Testcontainers.PostgreSql`** — a test dependency beyond the constitution's named test stack | FR-013a requires a test that starts against an *empty* database and asserts the schema exists afterwards. That needs a disposable database per test run. | Reusing the Compose database was rejected: it is not empty after the first run, so the test would pass for the wrong reason. Hand-rolled container lifecycle scripts were rejected as more code to maintain for the same result. |
| **`Microsoft.AspNetCore.Mvc.Testing`** — test dependency beyond the named stack | Needed to exercise the two endpoints in-process, including the database-unreachable path, without paying Playwright's cost for every case. | Testing endpoints only through Playwright was rejected: it makes fast failure-path coverage (FR-011, FR-014) slow and brittle. Raw `HttpClient` against a manually started host was rejected as re-implementing what this package provides. |

All three are additive and confined; none introduces an architectural layer. No repository
pattern, service layer, or mediator is introduced — the endpoints call the probe directly.
Verified against the built code on 2026-09-02: a scan for `IRepository`, `MediatR`,
`AutoMapper` and service-layer interfaces finds nothing.

**As-built additions not present in the table above.** None adds a capability or a layer;
they are recorded here so the table matches reality:

| Addition | Why it appeared |
|---|---|
| `Microsoft.EntityFrameworkCore.Design` | Required by `dotnet ef migrations add`. Part of the constitution-mandated EF Core toolchain, not a new capability. |
| `Serilog.Sinks.TextWriter` (replacing `Serilog.Sinks.Console`) | `WriteTo.TextWriter` does not exist in the Console sink. `Console.Out` is itself a TextWriter, so one sink now serves both production and tests — the tested code path *is* the production path. Sink count unchanged. |
| `Microsoft.EntityFrameworkCore`, `.Relational`, `Microsoft.Extensions.Logging.Abstractions` in the test projects | Version alignment only. Npgsql pins EF Core 10.0.4 while Design pins 10.0.11, which produced MSB3277 conflict warnings; explicit references resolve them. All three were already present transitively. |

The build is warning-free after these alignments.
