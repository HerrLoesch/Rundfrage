# Phase 1 Data Model: Platform Scaffold

**Feature**: 001-platform-scaffold | **Date**: 2026-09-02

This feature introduces **no persisted domain entity**. The clarification session settled on a
schema-independent `SELECT 1` probe (FR-008) and removed the earlier `Persistence Probe`
entity, so nothing application-owned is stored. What follows is therefore short by design: one
transient value object, one framework-owned table, and one frontend state machine.

---

## 1. Persisted schema

### `__EFMigrationsHistory` (owned by EF Core)

The only table this feature creates. Produced by applying the deliberately empty initial
migration (research.md R-1).

| Column | Type | Notes |
|---|---|---|
| `MigrationId` | `character varying(150)` | Primary key. Identifier of the applied migration. |
| `ProductVersion` | `character varying(32)` | EF Core version that applied it. |

**Rules**
- Created automatically on first start against an empty database (FR-013).
- Re-applying against an existing database is a no-op (FR-013, edge case "second start").
- Persisted in the named Compose volume, so it survives stop/restart (FR-004, SC-009).
- Verified by an integration test that starts against an empty Testcontainers database and
  asserts the table exists afterwards (FR-013a).

### `RundfrageDbContext`

Declares **no `DbSet<>`**. It exists to own the connection, host the migration pipeline, and
execute the probe query. The first survey feature adds entities here.

> Configured **without** `EnableRetryOnFailure`. Retry lives explicitly around the startup
> migration instead, so the probe cannot exceed its 2-second budget (research.md R-3).

---

## 2. Transient value object

### `ConnectivityStatus`

Produced on demand by `DatabaseProbe`, never stored, never cached (acceptance scenario 2.4).

| Field | Type | Notes |
|---|---|---|
| `State` | `DatabaseState` | Result of the probe. See enum below. |
| `CheckedAt` | `DateTimeOffset` (UTC) | When this result was determined. |
| `DurationMs` | `int` | Elapsed time of the check; logged (FR-027) and returned. |

### `DatabaseState` (backend enum)

| Value | Meaning |
|---|---|
| `Reachable` | `SELECT 1` completed successfully within the budget. |
| `Unreachable` | The query failed, or the 2-second budget elapsed (FR-012). |

**The backend has exactly two states.** It cannot report its own absence; the third UI state
is derived in the frontend (research.md R-4).

**Rules**
- Carries no exception text, host name, or connection string — FR-014 forbids sending those
  to the browser, so they are absent from the type itself rather than filtered later.
- Language-neutral: `State` is a token, not German text. The frontend maps it to a
  translation key (FR-029, research.md R-9).
- Exactly one log entry per check, carrying outcome and `DurationMs` (FR-027).

---

## 3. Frontend state machine

The Pinia `status` store resolves the tri-state required by FR-010:

| UI state | Derived when | Translation key |
|---|---|---|
| `reachable` | Response 2xx **and** `state === "reachable"` | `status.database.reachable` |
| `unreachable` | Response 2xx **and** `state === "unreachable"` | `status.database.unreachable` |
| `backendUnreachable` | Request failed, timed out, or returned non-2xx | `status.backend.unreachable` |
| `loading` | Request in flight | `status.loading` |

```text
        ┌──────────┐
        │ loading  │ ── request fails / non-2xx ──► backendUnreachable
        └────┬─────┘
             │ 2xx
             ├── state=reachable   ──► reachable
             └── state=unreachable ──► unreachable

   any state ── reload ──► loading      (recovery per SC-005, needs no restart)
```

**Consistency rule this imposes on the API**: because any non-2xx is read as
*backendUnreachable*, `GET /api/v1/status/database` **MUST return 200 even when the database is
down**, carrying `state: "unreachable"` in the body. Returning 503 for a database outage would
render as a backend outage and break FR-010's required distinction. This is pinned in
`contracts/openapi.yaml`.

**Testing rule**: components expose `data-testid` attributes and are asserted through those
and through translation keys, never through German literals (FR-030).

---

## 4. Configuration (not persisted)

| Variable | Default | Purpose |
|---|---|---|
| `POSTGRES_USER` | `rundfrage` | Database role. |
| `POSTGRES_PASSWORD` | `rundfrage_dev` | Development-only default (FR-005). |
| `POSTGRES_DB` | `rundfrage` | Database name. |
| `ConnectionStrings__Default` | composed from the above, `Timeout=2` | Probe budget (R-3). |
| `LOG_LEVEL` | `Information` | Serilog minimum level (FR-025). |

Defaults are supplied inline in `compose.yaml` so a fresh clone starts with no `.env` file and
no manual step (FR-001, SC-002, research.md R-11).
