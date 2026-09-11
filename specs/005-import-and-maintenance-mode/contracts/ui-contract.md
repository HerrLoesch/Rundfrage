# UI Contract: Importing Exported Data, and a Maintenance Mode

**Feature**: 005-import-and-maintenance-mode | **Date**: 2026-09-04

Two surfaces: controls in the admin area, and the one page a participant sees while maintenance is
on. The language is German throughout, as elsewhere in the application.

---

## 1. The maintenance notice (participant-facing)

The only page a participant can reach while maintenance mode is on. It replaces every participant
route — poll link, personal link, results (FR-026).

| Element | Requirement |
|---|---|
| Heading | „Wartung" — `<h1>`, the page's only first-level heading |
| Body | States that maintenance is in progress and that answering will be possible again afterwards (FR-032) |
| Poll content | **None.** No title, message, day, name or result (FR-026) |
| Existence disclosure | The page is identical whether or not a poll exists behind the link (FR-032) |
| Retry control | None. A reload is the retry, and a button implying progress would be a promise the page cannot keep |

**Accessibility**

- `<main>` landmark; the heading is the first focusable content.
- Announced as a status region (`role="status"`), so a screen reader reaching the page after a
  client-side navigation hears why the poll is not there.
- Contrast meets WCAG AA in both Vuetify themes, as the rest of the application does.
- No animation, no automatic reload, no focus trap.

**The disclosure rule is the load-bearing one.** A notice that varied — a different message for an
unknown token than for a real poll — would leak exactly what 002 SC-012's neutral 404 was built to
hide. Same bytes, every time.

---

## 2. Maintenance control (admin)

| Element | Requirement |
|---|---|
| Control | A switch, labelled „Wartungsmodus" |
| State | Always visible, never inferred from a previous action (FR-025) |
| On-state | Visually distinct and persistent — the operator must be able to tell at a glance across a session |
| Since | „seit <Zeitpunkt>" when on, from the state's `since` |
| Confirmation | Switching **on** confirms, because participants lose access. Switching **off** does not |
| Idempotence | Switching on when on, or off when off, is a no-op that reports success |

While on, the admin area shows a persistent banner. Not a toast: a toast is missed, and the failure
mode of this feature is leaving the site down after the work is finished.

---

## 3. Import (admin)

Two entries, deliberately not adjacent and deliberately not alike (FR-001).

### 3a. „Umfrage aus Datei einlesen" (JSON)

| Element | Requirement |
|---|---|
| Input | File picker, `.json` |
| Action | Single button. No confirmation — the operation is additive and touches nothing existing |
| Result | The summary from §4 |

### 3b. „Sicherung wiederherstellen" (whole storage)

| Element | Requirement |
|---|---|
| Placement | Visually separated from 3a and marked as affecting everything |
| Precondition | Disabled while maintenance mode is off, with the reason stated inline (FR-024) |
| Step 1 | Upload → preview: what the backup holds, and **how many polls and responses will be lost** (FR-018) |
| Step 2 | Explicit confirmation naming the loss. Not a bare „OK" |
| Result | The summary from §4 |

The disabled state must say *why* and what to do — „Erst den Wartungsmodus einschalten" — rather
than being an inert control the operator has to reason about.

---

## 4. Result summary (admin, both imports)

Shown once, as the answer to the request. Not retained, and there is no history to return to
(FR-003a) — so the interface must not imply one.

| Element | Requirement |
|---|---|
| Outcome line | What was taken. For a JSON import that created nothing: „Nichts übernommen" (FR-004) |
| Participant link | On success, the new link, copyable, with the note that previously shared links do not reach it (FR-007, FR-011) |
| Skipped list | One row per item: what, and why. Rendered from the `reason` code, never raw text from the file |
| Empty skipped list | Absent entirely. An empty „Übersprungen" heading reads as a defect |
| Persistence hint | Because nothing is stored, the summary stays until dismissed — it is never replaced by a background update |

**„Nichts übernommen" must not look like an error.** A file holding one expired poll produces
exactly that, and it is the specified outcome (FR-004, FR-012). It is reported in the same
neutral register as a success with skips — not in the red used for a refusal.

**Refusal is different and looks different.** A file that was refused (§ openapi 400) created
nothing and has no summary; it shows an error with the `code` rendered into German prose.

**Accessibility**

- The summary receives focus when it appears, so the outcome is announced rather than silently
  rendered below the fold.
- The skipped list is a `<ul>`, not a table — it is a list of reasons, not a grid.
- The copyable link follows the pattern feature 004 established for participant links: a real
  anchor, followable, opening in a new tab.
