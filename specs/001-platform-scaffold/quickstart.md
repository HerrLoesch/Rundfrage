# Quickstart: Rundfrage Platform Scaffold

**Feature**: 001-platform-scaffold | **Date**: 2026-09-02

This is the developer-facing guide the feature must make true (FR-023). It describes the
system as designed; it becomes the basis of the repository `README.md` during implementation.

---

## Start the system

From the repository root, on a machine with only a container runtime installed:

```bash
docker compose up --build
```

That is the whole setup (FR-001, FR-002), and it is what SC-001 budgets at under 10 minutes
on a fresh clone. There is no `.env` to copy and no database to
create — Compose supplies working development defaults inline, and the schema is created
automatically on first start (FR-013, research.md R-11).

Then open:

| | |
|---|---|
| **Application** | http://localhost:8080 |
| Text endpoint | http://localhost:8080/api/v1/message |
| Database status | http://localhost:8080/api/v1/status/database |

Stop with `Ctrl-C`, or `docker compose down`. Data survives both (FR-004). Use
`docker compose down -v` to also drop the volume and get a genuinely fresh database.

---

## What the page shows

The page displays the backend text and one of three states (FR-010). All text is German
(FR-028), resolved through the i18n layer (FR-029):

| State | Meaning | How to reproduce |
|---|---|---|
| **Datenbank erreichbar** | Backend answered and `SELECT 1` succeeded. | Normal operation. |
| **Datenbank nicht erreichbar** | Backend answered, but the database query failed or exceeded its 2-second budget. | `docker compose stop db`, then reload. |
| **Backend nicht erreichbar** | The page could not reach the backend at all. | `docker compose stop app`, then reload. |

Recovery needs no restart: bring the service back up and reload the page (SC-005).

> The distinction between the second and third state is the reason
> `/api/v1/status/database` answers **200 even when the database is down** — a 503 would be
> read by the frontend as "backend unreachable". See `contracts/openapi.yaml`.

---

## Develop with live reload

The container set stays at two services (FR-003, SC-010). The frontend dev server runs
outside it and proxies to the containerised backend (FR-003b):

```bash
docker compose up -d          # backend + database
cd frontend && npm run dev    # Vite on :5173, proxying /api -> :8080
```

The browser still sees a single origin, so no CORS configuration exists in either mode
(FR-003a, research.md R-10).

---

## Run the tests

Test-first is mandatory (Principle II, FR-022). Each suite has a defined scope
(research.md R-12):

```bash
# Backend: unit + integration (integration needs a running Docker daemon for Testcontainers)
dotnet test backend/

# Frontend: unit + component
cd frontend && npm run test:unit

# End-to-end: against the real container set
docker compose up -d --build
npx playwright test
```

> `dotnet test` and `npm test` need the .NET SDK and Node installed locally. FR-002's
> "container runtime only" promise covers *starting the system*, not developing it.

Tests assert against `data-testid` attributes and translation keys, never against German
literals, so changing a translation cannot break a test (FR-030).

---

## Configuration

| Variable | Default | Purpose |
|---|---|---|
| `POSTGRES_USER` | `rundfrage` | Database role. |
| `POSTGRES_PASSWORD` | `rundfrage_dev` | Development-only default. Never used outside local work (FR-005). |
| `POSTGRES_DB` | `rundfrage` | Database name. |
| `LOG_LEVEL` | `Information` | Serilog minimum level; change without rebuilding (FR-025). |

Override by creating a `.env` file (git-ignored) or by exporting the variables.
`.env.example` documents the full set.

---

## Read the logs

Serilog writes structured entries to stdout, so the container runtime is the only tool needed
(FR-024):

```bash
docker compose logs -f app
LOG_LEVEL=Debug docker compose up      # more detail, no rebuild
```

Every database check produces exactly one entry with its outcome and duration (FR-027).
Credentials and connection strings never appear in log output, including inside exception
messages — a unit test enforces this (FR-026, research.md R-5).

---

## Branches and CI

Development happens on `dev`; `main` represents released state (FR-015). Pushes to either
branch, and pull requests targeting them, run the pipeline: build plus all three test suites
(FR-016, FR-017). The pipeline builds and tests only — it publishes no images and performs no
deployment (FR-018, FR-019). Deployment on `main` is triggered separately and is outside this
feature.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| Port 8080 already in use | Another process holds the port. | Stop it, or change the published port in `compose.yaml`. |
| Page shows "Datenbank nicht erreichbar" right after first start | The app started before PostgreSQL accepted connections. | Reload after a moment. Startup migration retries for ~30 s (research.md R-2). |
| Status stays "nicht erreichbar" after the database is up | Migration was abandoned during a long outage. | `docker compose restart app` — the next start applies the schema (known limitation, research.md R-2). |
| Integration tests fail to start a container | Docker daemon not running. | Start Docker; Testcontainers requires it. |
