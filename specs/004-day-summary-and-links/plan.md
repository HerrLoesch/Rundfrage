# Implementation Plan: Day Summary Above the Date, and Links That Are Links

**Branch**: `dev` (feature directory `004-day-summary-and-links`) | **Date**: 2026-09-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/004-day-summary-and-links/spec.md`

## Summary

Move the per-day tally from the foot of the results grid to the head of it, above each day's
date, and start it folded. Turn every displayed address into a link that opens in a new tab.

Both changes are presentational. Nothing is stored, nothing is requested, no endpoint moves, and
the numbers shown are the ones the results already carry. **Three files change, two of them
components.**

The interesting part is not the change but where it is verified. The summary's correctness lives
in things jsdom cannot see — position after a sideways scroll, focus order, what a screen reader
is told — so most of the proof is end-to-end by necessity, not preference. Feature 003 ended with
a four-pixel misalignment that every component test happily passed; this plan is written by
someone who has just paid that bill.

## Technical Context

**Language/Version**: TypeScript on Node 24 — the backend is untouched
**Primary Dependencies**: Vue 3.5.42, Vuetify 4.2.0, vue-i18n 11, vue-router 5 — unchanged, none added
**Storage**: untouched. No migration, no schema change, no new column (FR-019)
**Testing**: Vitest + Vue Test Utils for structure and state; Playwright for layout, focus and the
accessibility tree; the xUnit suites are unaffected and only have to keep passing
**Logging**: unchanged. A presentational change has nothing to record
**Target Platform**: modern browsers; one Linux container
**Project Type**: Web application, single origin, `/api/v1`
**Performance Goals**: unfolding within 1 s at 100 days (SC-005); no regression on the 5 s page
budgets inherited from 001
**Constraints**: each day's summary stays over its own column while scrolling (FR-013); folded
content is neither focusable nor announced (FR-006); the opened tab gets no handle on the opener
(FR-016b)
**Scale/Scope**: 100 candidate days; the grid pages at 50 responses, so unfolding builds
3 × 101 = 303 cells

## Constitution Check

Checked against `.specify/memory/constitution.md` (v2.0.1).

- [x] **I. Zero-Signup Participation (NON-NEGOTIABLE)**: nothing is added between a link and the
      answer form. The participant surface gains a fold-out and loses nothing; FR-008 forbids
      remembering the fold state, precisely because remembering it would mean storing something
      about an anonymous reader.
- [x] **II. Test-First Development (NON-NEGOTIABLE)**: every behaviour below is introduced by a
      failing test. Two existing tests must be re-pointed rather than relaxed (research R-7), and
      a re-pointed test that still fails is a defect in the feature.
- [x] **III. Simplicity & YAGNI**: no new dependency, no new component, no new store, no new
      request. The accordion component was considered and rejected on the merits — it cannot wrap
      table rows, and working around that would break FR-013 (research R-2).
- [x] **IV. Data Minimization & Operator-Controlled Storage**: nothing is collected, nothing is
      stored, no asset is fetched from anywhere but this origin. The fold state is deliberately
      not persisted (FR-008).
- [x] **Technology Constraints**: Vue 3 + Vuetify + vue-i18n, as pinned. Nothing else.

**Post-design re-check (after Phase 1)**: no deviation. The design adds one boolean to one
component and one attribute set to two links.

## Project Structure

### Documentation (this feature)

```text
specs/004-day-summary-and-links/
|-- plan.md                  # This file
|-- spec.md                  # 26 requirements, 8 success criteria, 4 clarifications
|-- research.md              # Phase 0 - 7 decisions, three of them measured
|-- quickstart.md            # Phase 1
|-- contracts/
|   `-- ui-contract.md       # Phase 1 - the observable interface: elements, states, semantics
`-- checklists/
    `-- requirements.md
```

**No `data-model.md`.** There is no entity, no field and no relationship in this feature — FR-019
and FR-020 say so explicitly, and the specification carries no Key Entities section because there
is nothing to carry. A file stating "nothing changes" would be a file to keep in step with
nothing.

**A UI contract instead.** What this feature does expose is an interface: which elements exist,
what identifies them, what states they carry and what assistive technology is told. That is the
contract the tests are written against, and it belongs in `contracts/` for the same reason an
OpenAPI document did in feature 003.

### Source Code (repository root)

```text
frontend/src/
|-- components/
|   |-- poll/
|   |   |-- ResultGrid.vue           # summary moves tfoot -> thead, gains the fold-out
|   |   `-- ShareLink.vue            # code element -> anchor (both callers benefit)
|   `-- admin/PollList.vue           # code element -> anchor
`-- locales/de.json                  # the control's label and states, the new-tab note

frontend/tests/unit/
|-- ResultGrid.spec.ts               # RE-POINTED: folded default, position, unfold
`-- PollList.spec.ts                 # gains the anchor assertions

e2e/tests/
|-- date-poll-journey.spec.ts        # RE-POINTED: must unfold before reading the counts
`-- results-summary.spec.ts          # NEW - keyboard, accessibility tree, scrolling, new tab
```

**Structure Decision**: nothing moves and nothing is created. `ResultGrid` already owns the
totals; it keeps owning them, three rows higher. `ShareLink` is already shared between the
creator's confirmation and the participant's confirmation, so changing it once satisfies two of
the three addresses in FR-015 — which is the reason the third clarification came out as "all
three": the alternative was to make one caller behave differently from the other.

## Requirement Coverage

All 26 requirements and 8 success criteria have a home. Most are cited inline above or in
research; the rest are grouped here so the tasks phase has somewhere to put them.

**Carried by `ResultGrid`**: FR-001, FR-001a, FR-002, FR-003, FR-004, FR-005, FR-006, FR-007,
FR-008, FR-008a, FR-009, FR-010, FR-011, FR-012, FR-013, FR-014.

**Carried by `ShareLink` and `PollList`**: FR-015, FR-015a, FR-016, FR-016a, FR-016b, FR-017,
FR-018.

**Verification obligations rather than designs**: FR-019 and FR-020 are satisfied by not doing
anything — a task checks that the change set touches no backend file and adds no request.
FR-021 and SC-008 require the existing suites to pass with nothing weakened.

**Measured criteria**: SC-005 (unfolding at 100 days) becomes a named component task. SC-001
(nothing counted on arrival) and SC-004 (keyboard alone) become named tests. SC-002 (every day's
counts above its own date after one action) and SC-003 (zero counts below the responses) are the
two halves of the move itself — they are asserted in the component test that pins document order,
and the UI contract's document-order table is what that test reads. SC-006 (one click, new tab,
admin tab untouched) and SC-007 (address still selectable, copy still works) each become a named
test.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| *(none)* | — | — |

No deviation. Worth recording what was considered and dropped, so it is not rediscovered:

- **The library's accordion component** — cannot wrap `<tr>`, and moving the summary out of the
  table to accommodate it breaks FR-013, measured (research R-1, R-2).
- **`aria-controls` naming the three rows** — the attribute does accept a list of ids, measured.
  Not used, because the rows do not exist while folded and a dangling reference is worse than an
  absent optional attribute.
- **Persisting the fold state** — rejected by FR-008 rather than deferred: it would mean storing
  something about an anonymous reader.
