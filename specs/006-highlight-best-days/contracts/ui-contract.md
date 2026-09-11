# UI Contract: Highlighting the Best Days

**Feature**: 006-highlight-best-days | **Date**: 2026-09-06

One surface: the *yes* row of the per-day summary in the results grid, on both the participant's and
the creator's view. Nothing else in the interface changes.

---

## 1. The mark

| Element | Requirement |
|---|---|
| Where | The **yes** row's cell for a best day. Not the *maybe* or *no* cells, not the date, not the response rows (FR-008a, research R-2) |
| When | Only while the summary is unfolded. Folded, the grid looks exactly as it does today (FR-008a) |
| How many | One per best day. Several when days tie; none when no day has a *yes* (FR-002, FR-001b) |
| Visual | An icon beside the count, plus a weight or background change on the cell. **Never colour alone** (FR-005, SC-004). Legible enough that a reader names the best of 20 days in under five seconds without reading a count (SC-001) |
| Layout | The cell must not change width or height when marked — a mark that reflows the grid would move the columns a reader is comparing |

## 2. What the mark says

Two distinct pieces of text, and keeping them apart is the point (004 FR-016a learned this the hard
way, when a note glued into a link's name broke every navigation built from it):

| | Text | Carried by |
|---|---|---|
| **Name** | „bester Tag" | The mark's accessible name — what a screen reader says on reaching the cell (SC-004) |
| **Description** | „Bester Tag: die meisten Ja, bei Gleichstand die wenigsten Nein" | `title` for hover, and referenced with `aria-describedby` |

The description is never part of the name. A screen reader reaching a marked cell should hear the
count, then "bester Tag" — not a sentence about ranking rules on every one of them.

**Both strings live in the message catalogue** (`locales/de.json`), like every other user-facing
string. `noLiteralStrings.spec.ts` already fails any template carrying one directly.

## 3. Reaching the explanation (FR-007)

| Route | Behaviour |
|---|---|
| Screen reader | The name on the cell; the description via `aria-describedby` |
| Pointer | `title` appears on hover |
| Keyboard | The mark is focusable, so `title` appears on focus |
| Touch | **Not reachable without a pointer.** Accepted (spec Assumptions): the counts the mark is derived from are on screen beside it |

The mark adds one tab stop per best day — normally one. Research R-3 records why that is the only
option that satisfies "hover **and** focus", and why the fifty-marked-days case is accepted rather
than engineered around. All three reachable routes together are **SC-004a**: the rule is available
from the mark alone, with no explanatory text occupying space anywhere.

## 4. States

```text
summary folded          -> no mark anywhere; the grid is byte-identical to today
summary unfolded, one best day    -> one marked cell
summary unfolded, several tied    -> every tied day marked, identically
summary unfolded, no day has yes  -> no mark, and no explanation of its absence
```

**The empty case shows nothing at all** — no "kein bester Tag" line. A poll on the day it is created
has no answers, and a message explaining that no day leads would appear on every new poll, which is
noise rather than information.

## 5. What must not change

- Every count keeps its value and its position (FR-008). Verified field by field against the current
  results view — that comparison is **SC-006**.
- Days keep their chronological order.
- The fold control, its label and its behaviour are untouched (004 FR-004, FR-005).
- The creator's view and the participant's view mark the same days (FR-004, **SC-003**) — one
  component, so this holds unless someone deliberately branches on the viewer.
