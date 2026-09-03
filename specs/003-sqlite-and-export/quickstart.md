# Quickstart: SQLite Storage and JSON Export

**Feature**: 003-sqlite-and-export | **Date**: 2026-09-03

What this feature must make true, described as the developer and the operator will experience it.
It replaces the storage half of the 001 quickstart and leaves the 002 quickstart standing —
nothing about creating a poll or answering one changes.

---

## Starting it

```bash
docker compose up --build
```

Same command, one difference worth noticing: **one container comes up instead of two** (FR-001).
There is no database service to wait for, no healthcheck, no `depends_on`. The first start creates
the storage file in an empty directory (FR-004); every start after that finds it there (FR-006).

| | |
|---|---|
| **Anwendung** | <http://localhost:8080> |
| Adminbereich | <http://localhost:8080/admin> |

The `/status` diagnostic page from feature 001 is **gone**, along with the two endpoints that fed
it (FR-022, FR-023). It existed to prove during scaffolding that data reached the page; the
product now proves that on every end-to-end run.

---

## Where the data lives

One file in a mounted directory. That is the whole storage story.

```bash
# .env (git-ignored)
DATA_DIR=/data              # inside the container; the compose volume mounts here
APP_PORT=8080
LOG_LEVEL=Information
```

`POSTGRES_USER`, `POSTGRES_PASSWORD` and `POSTGRES_DB` are gone (FR-026). There is no host, no
port, no credential to configure — reaching the storage means having the path.

**Which is exactly the exposure to be clear about**: whoever can read that file can read every
poll and every answer, without a password (FR-007b). The file carries permissions for the
application's account only (FR-007a), and that is the honest limit of what the application can do
about it. It is deliberately **not** encrypted (FR-007c) — a key kept beside the data in the same
configuration stops nobody who can reach the data, and adds a way to lose everything by losing a
key. Protecting the directory is the host's job.

---

## Backing it up

**Use the button, not `cp`.** This is the one operational instruction in this feature that costs
data if ignored.

In the admin area, *Sicherung herunterladen* produces a consistent copy while the system keeps
running and serving (FR-003) and hands it over as a single file that needs no companions
(FR-003a). Nothing reads a backup back in — there is no import.

**Restoring** means putting that file in the mounted directory as `rundfrage.db` and starting:

```bash
docker compose down
docker run --rm -v rundfrage_rundfrage-data:/data -v "$PWD":/in alpine \
  sh -c 'cp /in/rundfrage-2026-09-03T101500Z.db /data/rundfrage.db && chown -R 1654:1654 /data'
docker compose up -d
```

The `chown` is not cosmetic, and it covers the **directory**, not only the file. Found by doing
it: with the file owned correctly inside a directory owned by root, the application reports
`attempt to write a readonly database` and the admin area says storage is unavailable — because
the engine needs to create its two companion files beside the restored one, and cannot. The
backup is fine; the permissions are not, and the message does not say so.

Copying the storage file out of the mounted directory by hand **while the system runs** is
unsupported (FR-003b), and not for form's sake. Measured:

```text
Situation at the moment of the copy                   Hand copy of the main file
-----------------------------------------------------------------------------------
System stopped                                        complete
A connection open, commits still in the companion     20 of 40
A read in progress, pinning an older view             40 of 60
Fresh storage, first connection still open            UNUSABLE — "no such table"
```

Stop the system and `cp` is fine. Leave it running — which is exactly when someone reaches for
`cp`, precisely because they did not want to interrupt anyone — and the copy is silently short.
Sometimes by a few answers, sometimes by every one of them including the schema, and nothing about
the file says which. It weighs about the right amount and fails on the day it is needed.

---

## Exporting a poll

In the admin area, each poll offers *Als JSON exportieren*. The file carries the poll, its days in
order, and every response with its per-day answers (FR-014). The filename names the poll and the
moment it was taken, so several exports share a folder without overwriting each other (FR-021a):

```text
grillabend-2026-09-03T101500Z.json
```

```json
{
  "formatVersion": 1,
  "exportedAt": "2026-09-03T10:15:00Z",
  "poll": {
    "title": "Grillabend",
    "message": "Wer kann wann?",
    "days": [{ "date": "2026-09-12" }, { "date": "2026-09-13" }]
  },
  "responses": [
    {
      "displayName": "Anna",
      "answers": [
        { "date": "2026-09-12", "availability": "yes" },
        { "date": "2026-09-13", "availability": "maybe" }
      ]
    }
  ]
}
```

Three things about that document are load-bearing:

- **No token appears anywhere in it** (FR-015). The participant link is the right to answer; an
  edit token is the right to change someone's answer. A file that carried either would hand that
  right to whoever the file is forwarded to — undoing 002's link security through an omission
  rather than a decision.
- **An unanswered day is absent**, not `"none"`. Absence means "no answer" in storage, and the
  export keeps that meaning instead of inventing a fourth value that claims something was
  recorded.
- **`formatVersion` is a signal, not a promise.** Adding a field keeps the version; removing or
  renaming one raises it (FR-020b). Nothing commits to supporting version 1 forever (FR-020c).

A poll nobody has answered exports with an empty `responses` array — a valid export, not an error
(FR-018).

Exports are produced on demand and never stored (FR-021). There is nothing to clean up.

---

## Running the tests

```bash
dotnet test backend/Rundfrage.slnx    # no longer needs a running Docker daemon
cd frontend && npm run test:unit
```

Integration tests give each test class its own temporary storage file instead of a container
(research R-6). `Testcontainers.PostgreSql` is removed with the database it started.

**What must still be true afterwards** — this is the feature's real acceptance bar, and it is
mostly about *not* breaking things:

| Guarantee | Why it needs re-proving |
|---|---|
| The response cap holds exactly under concurrency (FR-010, SC-006) | The row lock it relied on was PostgreSQL's. Measured: 200 concurrent attempts against a cap of 50 → 50 accepted, 150 rejected, 0 errors, 50 stored. |
| A confirmed answer survives a power cut (FR-012a, SC-007) | Durability is a storage setting now, and the common advice for this journal mode is the setting that gives it up. |
| Expired polls stop being reachable at the deadline (FR-011) | The deadline filter is one of four queries that had to change shape. |
| Every existing test still passes, with no assertion weakened (FR-028, SC-008) | The point of the whole exercise. |

The two retired end-to-end specs (`walking-skeleton`, `database-status`) are **not** simply
deleted. What they were really proving — that the application starts and serves when its storage
is unreachable, and that a failure surfaces to the operator rather than silently — moves onto the
product itself (FR-024a, FR-024b). Deleting a test whose subject disappeared is fine; deleting the
assertion it carried is not.

---

## Upgrading an existing installation

There is nothing to migrate (FR-025). The system has never been deployed, so no PostgreSQL data
exists to move, and a migration path would be code written for a case that does not occur. For a
local instance with test data in it:

```bash
docker compose down -v      # discards the old database volume
docker compose up --build
```
