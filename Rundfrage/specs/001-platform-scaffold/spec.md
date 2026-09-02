# Feature Specification: Platform Scaffold (Walking Skeleton)

**Feature Branch**: `001-platform-scaffold`
**Created**: 2026-09-02
**Status**: Draft
**Input**: User description: "Die Basis für das System soll aufgesetzt werden. Hierzu gehört das Grundgerüst einer .NET Applikation mit Vue 3 Frontend und Postgresql Datenbank. Damit ergeben sich 2 Dockercontainer die via docker compose erstellt werden sollen. Es soll ein dev und ein release branch mit ensprechender CI Pipeline in Github geben. Wir entwickeln immer auf dev. Ein Push zu main wird auf andere Weise ein Deployment auslösen. In der ersten Version, soll es einen Endpunkt im Backend geben, über den man einen Text ausgeben soll. Dies ist als Durchstich zu realisieren. Alles soll automatisch aufgesetzt werden und in der Webseite ist dann zu sehen ob sich erfolgreich mit der Datenbank verbunden werden konnte oder nicht. Für die Datenbankkommunikation nutzen wir Entity Framework core."

## Overview

This feature establishes the foundation on which every later Rundfrage feature is built: a
running system that proves the complete chain — browser → web application → backend →
database — actually works, plus the automation that keeps it working.

It delivers no end-user survey capability. Its value is that the next feature can be about
surveys instead of about plumbing, and that a broken link in the chain is caught by
automation rather than discovered by hand.

The primary beneficiaries are the **developer** (needs a working environment from a fresh
clone, and fast feedback on every push) and the **operator** (needs a reproducible,
self-contained system that reports its own health).

## Clarifications

### Session 2026-09-02

- Q: What should the database check query to prove "an actual completed query" (FR-008)? → A: Execute a trivial scalar query (`SELECT 1`) through the data access layer
- Q: After what period must the database check time out (FR-012)? → A: 2 seconds
- Q: What logging baseline should the scaffold establish? → A: Serilog, structured output to stdout
- Q: Which UI language, and with or without an i18n layer? → A: German, with an i18n layer from the start
- Q: Under which path do the backend endpoints live, given the shared origin? → A: `/api/v1/...`, versioned from the first endpoint

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Fresh clone starts with one command (Priority: P1)

A developer clones the repository onto a machine that has nothing installed except a
container runtime. They run a single documented command. After it completes, they open a
browser at a documented address and see the Rundfrage web page displaying a text that
demonstrably came from the backend, not from the page itself.

**Why this priority**: This is the walking skeleton. Without it there is nothing to build
on, and every later feature would begin with an unresolved environment problem. It is the
smallest slice that proves frontend and backend are connected and reproducibly buildable.

**Independent Test**: On a clean machine (or after removing all build output, containers,
and volumes), clone the repository, run the documented start command, and confirm the page
renders backend-provided text. Requires no other story.

**Acceptance Scenarios**:

1. **Given** a freshly cloned repository and no previously built images or volumes,
   **When** the developer runs the documented single start command,
   **Then** the system becomes reachable in a browser without any further manual step.
2. **Given** the system is running,
   **When** the developer opens the application's address in a browser,
   **Then** the page displays a text value that was retrieved from the backend at runtime.
3. **Given** the system is running,
   **When** the backend's text endpoint is called directly,
   **Then** it responds successfully with that same text.
4. **Given** the system has been started once,
   **When** the developer stops it and starts it again with the same command,
   **Then** it returns to a working state without manual repair.

---

### User Story 2 - The page reports database connectivity (Priority: P2)

A developer or operator opens the web page and can immediately tell whether the application
reached the database. When the database is reachable, the page says so. When it is not, the
page still loads and clearly reports the failure instead of showing a blank screen, an
error page, or a misleading success.

**Why this priority**: This is what turns Story 1 from a two-tier demo into a real
Durchstich through all three tiers. It is also the diagnostic that makes every later
database problem visible in seconds. It builds on Story 1's page but is separately
verifiable.

**Independent Test**: With the system running, confirm the page reports a successful
database state; then stop the database, reload the page, and confirm the page loads and
reports the failure state. Verifiable without touching CI or branch setup.

**Acceptance Scenarios**:

1. **Given** the database is running and reachable,
   **When** the user loads the web page,
   **Then** the page shows a clearly-labelled successful database connection state.
2. **Given** the database is stopped or unreachable,
   **When** the user loads the web page,
   **Then** the page still renders and shows a clearly-labelled failed database connection
   state, distinguishable at a glance from the successful state.
3. **Given** the database is unreachable,
   **When** the database is started again and the user reloads the page,
   **Then** the page reports the successful state without restarting the application.
4. **Given** the database connection check runs,
   **When** it reports success,
   **Then** that success reflects an actual completed query against the database, not
   merely an opened connection or a cached earlier result.
5. **Given** the system starts for the very first time against an empty database,
   **When** startup completes,
   **Then** the required schema exists without any manual database step.

---

### User Story 3 - Every push is built and tested automatically (Priority: P3)

The repository has a `dev` branch where all development happens and a `main` branch that
represents released state. Pushing to either branch, or opening a pull request against
them, automatically builds the system and runs the full automated test suite. The result is
visible on the commit and on the pull request.

**Why this priority**: It protects Stories 1 and 2 from silent decay. It is genuinely
independent — it can be built, pushed, and observed without either of the other stories
being complete — but it has no value until there is something to build, hence P3.

**Independent Test**: Push a commit to `dev` and observe an automated run start and report
a result. Push a commit that deliberately breaks a test and confirm the run reports
failure. Requires no manual runner setup.

**Acceptance Scenarios**:

1. **Given** the repository has `dev` and `main` branches,
   **When** a commit is pushed to `dev`,
   **Then** an automated pipeline runs that builds the system and executes all automated
   test suites.
2. **Given** a pull request targets `dev` or `main`,
   **When** the pull request is opened or updated,
   **Then** the same pipeline runs and its result is reported on the pull request.
3. **Given** a change breaks any automated test,
   **When** the pipeline runs,
   **Then** the pipeline reports failure and identifies which test failed.
4. **Given** all automated tests pass,
   **When** the pipeline completes,
   **Then** it reports success.
5. **Given** a push is made to `main`,
   **When** the pipeline runs,
   **Then** it builds and tests only; this feature performs no deployment.

---

### Edge Cases

- **Database slower to accept connections than the application**: the application starts
  before the database is ready. The page must report the failure state and recover on a
  later reload once the database is up, rather than crashing at startup or staying
  permanently failed.
- **Wrong or missing database credentials**: reported as a failed connection state on the
  page, with no credential values exposed in the page, in the response, or in logs.
- **Backend unreachable from the page**: the page still renders and reports that it could
  not reach the backend, distinguishable from "backend reachable but database down".
- **Host port already occupied** by another application: startup fails with a message that
  names the conflicting port rather than failing silently.
- **Database check hangs**: the connectivity check aborts after 2 seconds and reports the
  not-reachable state, so the page can never hang waiting for it.
- **Second start on an existing database**: automatic schema setup runs again without
  destroying existing data or failing on already-applied changes.
- **Pipeline run on a fork or from an external contributor**: runs without requiring
  secrets that are unavailable in that context, or is skipped in a clearly reported way.

## Requirements *(mandatory)*

### Functional Requirements

**Local environment**

- **FR-001**: The system MUST start completely from a single documented command executed in
  a freshly cloned repository, with no manual configuration steps in between.
- **FR-002**: Starting the system MUST require no locally installed language runtimes, SDKs,
  or database software beyond a container runtime.
- **FR-003**: The running system MUST consist of exactly two containers: an application
  container that serves both the backend endpoints and the built web assets, and a database
  container. No third container is part of the started system.
- **FR-003a**: The web page and the backend endpoints MUST be served from the same origin, so
  that no cross-origin configuration is required at runtime.
- **FR-003b**: A developer MUST additionally be able to run the frontend with live reload
  outside the container set, with its requests reaching the containerised backend.
- **FR-004**: Database contents MUST survive a normal stop and restart of the system.
- **FR-005**: All connection details and credentials MUST be supplied as environment
  configuration with working local defaults, and no real secret value may be committed to
  the repository.

**Walking skeleton behaviour**

- **FR-006**: The backend MUST expose an endpoint that returns a text value.
- **FR-006a**: All backend endpoints MUST be served under the `/api/v1` path prefix. All
  paths outside that prefix MUST be served to the web application, so that the application's
  own client-side routes and the backend endpoints cannot collide on the shared origin.
- **FR-007**: The web page MUST retrieve that text at runtime and display it, such that
  changing the backend's text changes what the page shows without any frontend change.
- **FR-008**: The backend MUST determine whether it can reach the database by executing a
  trivial scalar query through the data access layer, not by inspecting configuration alone
  and not by merely opening a connection. The query MUST NOT depend on any application table
  existing.
- **FR-009**: The result of that database check MUST be exposed to the web page through an
  endpoint under the `/api/v1` prefix.
- **FR-010**: The web page MUST display the database state in a form that is unambiguous at
  a glance and distinguishes at least: reachable, not reachable, and backend unreachable.
- **FR-011**: The system MUST remain usable when the database is unreachable — the page
  MUST render and report the failure rather than erroring out.
- **FR-012**: The database check MUST complete or time out within 2 seconds so that page
  rendering is never blocked indefinitely. On timeout the check MUST report the
  not-reachable state rather than an error or an indefinite wait.
- **FR-013**: The required database schema MUST be created automatically on first start
  against an empty database, and re-running against an existing database MUST be safe.
- **FR-013a**: Because the connectivity check (FR-008) is schema-independent and therefore
  cannot prove that schema creation succeeded, automatic schema creation MUST be verified
  separately by an automated test that starts against an empty database and asserts the
  expected schema exists afterwards.
- **FR-014**: Failure states MUST NOT expose connection strings, credentials, host names, or
  stack traces to the browser.

**Repository and automation**

- **FR-015**: The repository MUST have a `dev` branch used for all development and a `main`
  branch representing released state.
- **FR-016**: An automated pipeline MUST run on pushes to `dev` and `main` and on pull
  requests targeting them.
- **FR-017**: The pipeline MUST build the complete system and execute every automated test
  suite, and MUST report failure if any build step or test fails.
- **FR-018**: The pipeline scope MUST be limited to building the system and running the
  automated test suites. It MUST NOT build, tag, or publish container images, and MUST NOT
  produce release artifacts.
- **FR-019**: The pipeline MUST NOT perform any deployment; deployment triggered by pushes
  to `main` is explicitly outside this feature.
- **FR-020**: The pipeline MUST run without manually provisioned infrastructure.

**Presentation and language**

- **FR-028**: All user-facing text MUST be German.
- **FR-029**: User-facing text MUST be resolved through an internationalisation layer keyed
  by identifier; components MUST NOT contain literal user-facing strings.
- **FR-030**: Automated tests MUST assert against stable element identifiers or translation
  keys rather than against translated literals, so that changing a translation does not
  break a test.

**Observability**

- **FR-024**: The backend MUST emit structured, machine-readable log entries to standard
  output using Serilog, so that logs are readable through the container runtime without any
  additional infrastructure.
- **FR-025**: The minimum log level MUST be configurable through environment configuration
  without rebuilding the application.
- **FR-026**: Log output MUST NOT contain credentials or connection strings in any form,
  including inside exception messages.
- **FR-027**: Each database connectivity check MUST produce exactly one log entry recording
  the outcome and the elapsed duration of the check.

**Verification**

- **FR-021**: Automated tests MUST cover the full chain from the web page through the
  backend to the database, including the database-unreachable case, and MUST run against the
  same container set that developers start locally.
- **FR-022**: Every behaviour in this specification MUST be introduced test-first, in line
  with the project constitution.
- **FR-023**: The start command, the application address, and the meaning of each displayed
  database state MUST be documented in the repository.

### Key Entities

- **Connectivity Status**: The outcome of one database reachability check. Attributes: the
  state (reachable / not reachable), the moment it was determined, and a short
  non-sensitive description suitable for display. Not persisted; produced on demand. The
  check stores nothing and reads no application table, so this feature introduces no
  persisted domain entity.
- **Schema Version Record**: The record of which automatic schema changes have already been
  applied, so that repeated starts are safe. Owned and maintained by the data access layer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer starting from a fresh clone on a machine with only a container
  runtime reaches a working system in under 10 minutes and with exactly one command.
- **SC-002**: The number of manual setup steps required beyond that single command is zero.
- **SC-003**: With the system running and the database available, the page displays both the
  backend text and a successful database state within 5 seconds of loading.
- **SC-004**: With the database stopped, the page still loads within 5 seconds and reports
  the failure state; it never shows a blank page, an unhandled error, or a false success.
- **SC-004a**: With the database unreachable, the backend's database check returns a
  not-reachable result in no more than 2 seconds, measured over repeated attempts.
- **SC-005**: After the database is restored, a page reload reports success without any
  restart or manual intervention.
- **SC-006**: Every automated pipeline run on `dev` or `main` reports a pass/fail result
  within 10 minutes of the push.
- **SC-007**: A deliberately broken test causes the pipeline to fail in 100% of runs.
- **SC-008**: At least one automated test exercises the complete browser-to-database chain,
  at least one exercises the database-unreachable path, and at least one verifies automatic
  schema creation against an empty database.
- **SC-009**: Stopping and restarting the system preserves previously stored data in 100% of
  attempts.
- **SC-010**: The started system consists of exactly two containers, and the page requires
  zero cross-origin configuration to reach the backend.
- **SC-011**: Every database connectivity check is traceable to exactly one log entry
  containing its outcome and duration, and no log entry produced under any tested failure
  mode contains a credential or connection string.
- **SC-012**: Every user-facing string on the page resolves through the internationalisation
  layer; a scan of the frontend components finds zero literal user-facing strings.

## Assumptions

- **"Release branch" means `main`.** The description names "ein dev und ein release branch"
  and then refers to pushes to `main`; these are read as the same branch.
- **Deployment is out of scope.** The description states deployment is triggered "auf andere
  Weise" on pushes to `main`. This feature stops at build and test.
- **The pipeline is GitHub Actions**, since the description specifies GitHub.
- **The returned text is a placeholder** with no product meaning; its only purpose is to
  prove the frontend receives data from the backend at runtime.
- **No authentication, no user accounts, and no survey functionality** are part of this
  feature.
- **Automatic schema setup applies to the local and CI environments.** Whether production
  schema changes are applied automatically or gated is deferred to the feature that
  introduces production deployment.
- **The technology stack is fixed by the project constitution** (Vue 3 + Vuetify + Pinia +
  Vite, ASP.NET Core on .NET LTS, self-hosted PostgreSQL, Entity Framework Core for data
  access, Vitest / xUnit / Playwright for tests). This specification does not re-decide it;
  the description's technology names restate that existing decision.
- **The connectivity check is a diagnostic**, not a monitoring or alerting system. No
  history is stored and no notifications are sent.
- **Serilog's concrete output format** (plain-text console versus a JSON formatter) and its
  configuration mechanism are plan-level choices; this specification fixes only that logging
  goes through Serilog, is structured, and is written to standard output.

## Resolved Decisions

- **Container topology (FR-003)**: two containers — application (backend plus built web
  assets, single origin) and database. Chosen over a separate frontend container because it
  matches the stated "2 Docker containers", removes cross-origin configuration entirely, and
  is the most direct fit for constitution Principle IV, which requires all assets to be
  served from the application's own origin. Live-reload development runs the frontend dev
  server outside the container set against the containerised backend (FR-003b).
- **Pipeline scope (FR-018)**: build and run all automated test suites only. No image
  building and no publishing, keeping this feature strictly short of release automation and
  consistent with deployment being triggered separately on `main`.
- **API path convention (FR-006a)**: endpoints are versioned as `/api/v1/...` from the first
  endpoint. Unlike the internationalisation decision, this is a naming convention rather than
  an abstraction layer — it introduces no indirection and is reversible at negligible cost —
  so it is not treated as a Principle III deviation.

## Constitution Deviations

The constitution requires deviations to be recorded before implementation begins
(Development Workflow, gate 5) and justified in the plan's Complexity Tracking table.
This feature carries one:

- **Principle III (Simplicity & YAGNI) — internationalisation layer.** FR-029 introduces an
  i18n abstraction while only one language exists. Principle III states that abstractions
  MUST be introduced "in response to a second concrete use, not in anticipation of one", so
  this is a deliberate, explicit deviation rather than an oversight. Rationale: the project
  owner judged the cost of retrofitting i18n across an established Vue codebase to outweigh
  the cost of carrying the indirection from the start. `/speckit-plan` MUST carry this into
  the Complexity Tracking table, naming the rejected simpler alternative (literal German
  strings, i18n introduced with the first second-language requirement).

## Dependencies

- A GitHub repository with Actions enabled.
- A container runtime with Compose support available to every developer.
- Serilog as the backend logging library (added to the constitution's Technology
  Constraints in v1.1.0).
- An internationalisation library for the Vue frontend (FR-029).
- The project constitution at `.specify/memory/constitution.md` (v1.0.0), whose Technology
  Constraints section fixes the stack this scaffold instantiates.

## Out of Scope

- Any survey, questionnaire, or date-poll functionality.
- Deployment, hosting, environment provisioning, or release automation beyond build and test.
- Authentication or authorisation of any kind.
- Production database operations: backups, restores, connection pooling strategy, tuning.
- Monitoring, alerting, log aggregation, or uptime reporting. Emitting logs is in scope
  (FR-024 to FR-027); collecting, shipping, or retaining them elsewhere is not.
- Visual design beyond what is needed to display the text and the database state legibly.
