<!--
SYNC IMPACT REPORT
==================

--- Amendment 2026-09-02: v1.0.0 → v1.1.0 (MINOR) ---
Bump rationale: MINOR — Technology Constraints materially expanded with a logging
entry. No principle removed or redefined; existing constraints unchanged.

Added:
  - Technology Constraints → "Logging": Serilog, structured output to standard output,
    level via environment configuration, no external sinks.

Reason: Feature 001-platform-scaffold requires a logging baseline. Technology
Constraints states that plans MUST NOT introduce a framework outside its list, and
logging was absent from that list, so Serilog could not be adopted at plan level
without this amendment.

Templates requiring updates:
  ✅ .specify/templates/plan-template.md — Constitution Check "Technology Constraints"
     gate and Technical Context updated to name Serilog.
  ✅ specs/001-platform-scaffold/spec.md — FR-024..FR-027, SC-011, Dependencies.

--- Initial ratification ---
Version change: (unversioned template) → 1.0.0
Bump rationale: MAJOR — first ratification. Template placeholders replaced with
concrete, binding governance for the Rundfrage project.

Modified principles:
  - [PRINCIPLE_1_NAME] → I. Zero-Signup Participation (NON-NEGOTIABLE)
  - [PRINCIPLE_2_NAME] → II. Test-First Development (NON-NEGOTIABLE)
  - [PRINCIPLE_3_NAME] → III. Simplicity & YAGNI
  - [PRINCIPLE_4_NAME] → IV. Data Minimization & Operator-Controlled Storage
  - [PRINCIPLE_5_NAME] → removed (no fifth principle requested)

Added sections:
  - Technology Constraints (replaces [SECTION_2_NAME]; stack is pinned per project decision)
  - Development Workflow & Quality Gates (replaces [SECTION_3_NAME])

Removed sections:
  - Fifth principle slot — the project selected four governing areas.

Note on Principle IV: the originally requested "local-first" framing was adapted.
Link-shared surveys with no participant account require server-side storage, so
strict on-device-only storage is unachievable. The privacy intent is preserved as
data minimization, operator-controlled storage, and a ban on third-party tracking.

Templates requiring updates:
  ✅ .specify/templates/tasks-template.md — "Tests are OPTIONAL" contradicted
     Principle II; updated to mandate test-first task ordering.
  ✅ .specify/templates/plan-template.md — Constitution Check gate populated with
     the four principles; Technical Context defaults aligned to the pinned stack.
  ✅ .specify/templates/spec-template.md — reviewed; no constitution conflict
     (already mandates independently testable user scenarios).
  ✅ .specify/templates/checklist-template.md — reviewed; no constitution
     references, no change required.
  ✅ .claude/skills/speckit-*/ command files — reviewed; guidance is agent-generic,
     no outdated agent-specific references found.
  ✅ CLAUDE.md — reviewed; SPECKIT block is generic, no principle references to update.

Follow-up TODOs:
  - None. Test framework selections (Vitest, xUnit, Playwright) follow from the
    pinned stack and may be amended at PATCH level if the project prefers others.
-->

# Rundfrage Constitution

Rundfrage is a survey and date-finding tool for groups whose members will not create
an account on yet another website. Participants answer through a shared link: simple
forms with free-text and single-value questions, plus availability polls where each
person marks which of several proposed dates works for them.

## Core Principles

### I. Zero-Signup Participation (NON-NEGOTIABLE)

Answering a survey MUST NOT require an account, a login, an email confirmation, or the
installation of anything. A participant reaching a survey link MUST be able to submit a
complete response in a single session without leaving the page they landed on.

No feature may add a step between the link and the answer form. Any capability that
cannot work anonymously — response editing, duplicate prevention, result ownership —
MUST be solved with link-scoped secrets (unguessable URLs, per-response edit tokens) and
never by identifying the participant.

Survey *creators* MAY be asked to authenticate. Survey *participants* MUST NOT be.

Rationale: This is the product's reason to exist. The moment participation costs a
registration, Rundfrage is worse than the incumbents it replaces.

### II. Test-First Development (NON-NEGOTIABLE)

TDD is mandatory. For every behavior change the cycle is: write the test → observe it
fail for the intended reason → write the minimum code to pass → refactor.

- A commit that introduces behavior without a test that failed before it MUST NOT merge.
- Bug fixes MUST begin with a regression test that reproduces the bug.
- Tests MUST assert observable behavior, never internal structure that refactoring would
  legitimately break.
- Test tasks MUST be ordered before their implementation tasks in every `tasks.md`.

Rationale: A tool that collects other people's answers gets exactly one chance to record
them correctly. Tests written after the fact document what the code does; tests written
first define what it must do.

### III. Simplicity & YAGNI

Build the simplest thing that satisfies the current specified requirement.

- Abstractions MUST be introduced in response to a second concrete use, not in
  anticipation of one.
- Additional projects, services, layers, or dependencies MUST be justified in the
  Complexity Tracking table of the feature plan before they are created.
- A new dependency MUST be justified against the cost of writing the needed behavior
  directly; convenience alone is not sufficient justification.
- Speculative configurability, plugin points, and "we might need it" parameters MUST be
  rejected in review.

Rationale: Rundfrage's scope is small and its lifetime is long. Complexity added early is
paid for on every subsequent change.

### IV. Data Minimization & Operator-Controlled Storage

The system MUST collect only the data a survey's own questions ask for.

- No third-party analytics, tracking scripts, advertising, external fonts, or CDN-hosted
  assets. All assets MUST be served from the application's own origin.
- Participant IP addresses and user-agent strings MUST NOT be persisted alongside
  responses. Operational logs MUST NOT contain response content.
- All survey and response data MUST live in the self-hosted database under the operator's
  control. Transmitting survey content or responses to any external service is prohibited.
- Every survey MUST have a defined retention outcome — an expiry or an explicit deletion
  path — and deletion MUST remove the responses, not merely hide them.

Rationale: People answer without an account precisely because they are not signing up for
a relationship with a service. Storing more than the answers themselves breaks that deal.

## Technology Constraints

The stack below is binding. Changing any entry requires a constitution amendment, not a
plan-level decision.

- **Frontend**: Vue 3 using the Composition API, with Vuetify for components, Pinia for
  state, and Vite as the build tool.
- **Backend**: ASP.NET Core Web API on the current .NET LTS release (.NET 10 at
  ratification).
- **Persistence**: PostgreSQL, self-hosted. No managed third-party database services.
- **Logging**: Serilog, emitting structured entries to standard output. Log level is set
  through environment configuration. No external log sinks or aggregation services.
- **Testing**: Vitest with Vue Test Utils for frontend unit and component tests; xUnit for
  backend unit and integration tests; Playwright for end-to-end tests covering the
  participant flow.
- **Boundary**: The frontend MUST communicate with the backend exclusively through the
  documented HTTP API. Direct database access from the frontend is prohibited.

Feature plans MUST record concrete versions in their Technical Context section. Plans
MUST NOT introduce a language, framework, or datastore outside this list.

## Development Workflow & Quality Gates

Work follows the Spec Kit flow: `/speckit-specify` → `/speckit-plan` → `/speckit-tasks` →
`/speckit-implement`, on a feature branch per specification.

Gates that MUST pass before a change is considered complete:

1. **Constitution Check** — the feature plan's Constitution Check section is filled in and
   passing, both before Phase 0 research and again after Phase 1 design.
2. **Tests green** — the full unit, integration, and end-to-end suites pass. A failing or
   skipped test blocks the merge; disabling a test to unblock a merge is prohibited.
3. **Participant path verified** — any change touching the answer flow has an end-to-end
   test proving a survey can still be completed from a bare link with no account.
4. **Lint and format clean** — the project's configured linter and formatter report no
   violations.
5. **Complexity justified** — every entry in the plan's Complexity Tracking table names
   the simpler alternative that was rejected and why.

Deviations from any principle MUST be recorded in the plan's Complexity Tracking table
before implementation begins, never discovered afterwards in review.

## Governance

This constitution supersedes all other development practices. Where a habit, a template,
or a tool's default conflicts with it, this document wins.

**Amendments** MUST be proposed as a change to this file, stating the motivating problem,
the principle text added or altered, and the migration path for work already in flight.
An amendment takes effect when it is merged.

**Versioning** follows semantic versioning:

- **MAJOR** — a principle is removed or redefined in a way that invalidates existing work.
- **MINOR** — a principle or section is added, or its guidance materially expanded.
- **PATCH** — clarifications, rewording, and non-semantic refinements.

**Compliance review**: every review verifies the change against these principles. Reviewers
MUST reject changes that add unjustified complexity, that ship behavior without a
preceding failing test, or that put anything between a participant and the answer form.
When a template or command file drifts from this constitution, the constitution is
corrected first and the dependent artifacts are brought back into line in the same change.

For per-feature runtime guidance, read the plan for the feature currently being built, as
directed by `CLAUDE.md`.

**Version**: 1.1.0 | **Ratified**: 2026-09-02 | **Last Amended**: 2026-09-02
