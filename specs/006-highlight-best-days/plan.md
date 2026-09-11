# Implementation Plan: Highlighting the Best Days

**Branch**: `006-highlight-best-days` | **Date**: 2026-09-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/006-highlight-best-days/spec.md`

## Summary

The per-day summary already carries the counts that answer *"which day works best?"*. It leaves the
comparing to the reader. This marks the answer instead — the day with the most *yes*, ties broken by
the fewest *no*, and every tied day marked rather than one picked arbitrarily.

**The feature is smaller than it looks, and clarification is why.** Two answers cut it down:

- The mark lives inside the summary and appears with it, so nothing outside those three rows changes
  and 004's layout is untouched.
- The rule is explained on the mark itself, so no permanent text is added anywhere.

Phase 0 removed the rest. `ResultsProjection` already counts over the whole poll while paging only
the response rows, so a mark derived from the existing `totals` satisfies FR-003 by construction:
**no backend change at all**, no API field, no migration (R-1).

What remains is one pure function and three lines of an existing `v-for`.

## Technical Context

**Language/Version**: TypeScript on Node 24 — frontend only
**Primary Dependencies**: Vue 3.5, Vuetify 4, vue-i18n 11 — **no new dependency**
**Storage**: none. Nothing is stored, nothing is migrated (spec Assumptions)
**Backend**: unchanged. `PollView.totals` already carries whole-poll counts (R-1)
**Testing**: Vitest for the rule and the component; Playwright for the participant path
**Target Platform**: modern browsers, including touch and keyboard-only
**Project Type**: Web application, single origin
**Performance Goals**: none beyond the existing 5 s page budget. The rule is one pass over at most
100 day totals, computed from data already in memory
**Constraints**: the mark reflects every response, not the page (FR-003); it is not colour-alone and
is announced (FR-005); it occupies no space while folded (FR-007, FR-008a)
**Scale/Scope**: the feature 002 limits unchanged — 100 candidate days, 1000 responses

## Constitution Check

Checked against `.specify/memory/constitution.md` (v2.0.1).

- [x] **I. Zero-Signup Participation (NON-NEGOTIABLE)**: nothing is added between a link and the
      answer form. This changes only what the results grid emphasises, on a view participants
      already reach anonymously.
- [x] **II. Test-First Development (NON-NEGOTIABLE)**: the rule is a pure function (R-4), so every
      case in the specification's Edge Cases is a failing test before it is a passing one. The
      component and accessibility behaviour follow the same order.
- [x] **III. Simplicity & YAGNI**: no new project, layer, dependency or stored field. The one
      abstraction — a function for the rule — is introduced because it is the only way to test the
      arithmetic without rendering it, which Principle II requires. The rule is deliberately not
      configurable (spec Assumptions).
- [x] **IV. Data Minimization & Operator-Controlled Storage**: no data is collected, stored or
      transmitted. The mark is derived at the point of display from counts already on screen, and
      the export is untouched — a consumer computing its own ranking must not find one baked in.
- [x] **Technology Constraints**: Vue 3 + Vuetify + vue-i18n, Vitest and Playwright. Nothing added.

**Post-design re-check (after Phase 1)**: no deviation, and the design shrank on the way. R-1
removed the backend work the feature appeared to need; R-2 reduced the change to one cell per marked
day rather than a column. Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/006-highlight-best-days/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output — derived values only, nothing stored
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── ui-contract.md   # The mark: appearance, states, accessibility semantics
├── checklists/
│   └── requirements.md
├── spec.md
└── tasks.md             # Created by /speckit-tasks, not here
```

### Source Code (repository root)

```text
frontend/src/
├── composables/useBestDays.ts     # NEW — pure bestDays() plus the composable wrapper (R-4)
├── components/poll/ResultGrid.vue # exists — the yes row gains the mark (R-2)
└── locales/de.json                # exists — the mark's name and the rule's sentence

frontend/tests/unit/
├── bestDays.spec.ts               # NEW — every Edge Case as one line of input
└── ResultGrid.spec.ts             # exists — gains the rendering and accessibility cases

e2e/tests/
└── results-summary.spec.ts        # exists — gains the participant-facing check
```

**Structure Decision**: `composables/` already exists and holds one file, `useProblemText.ts` — a
real composable, `useX`-named and consuming `useI18n()`. A bare pure function beside it would give
the folder two meanings, so the new file exports both: `bestDays(totals)`, which is what the
nine-case table tests (R-4), and `useBestDays(totalsRef)` returning a `computed` of it, which is
what `ResultGrid.vue` consumes — the component already uses `computed` for `totalsByDay`, so this
adds no new idiom. No new folder, no backend file, and `ResultGrid.vue` is the only component
touched.

## Risks carried into implementation

- **FR-003 holds because of how `ResultsProjection` is written today** (R-1), not because anything
  here enforces it. A test must fail if the projection ever starts paging its counts, or the mark
  will begin moving as the reader pages and nothing will say why. That test is **SC-005**, and it is
  the only thing standing between this requirement and a silent regression somewhere else.
- **The tie is the case that gets dropped.** A naive `reduce` for the maximum returns one day, and
  the interface would look correct with a single mark while quietly picking the first of several.
  FR-002 is the requirement most likely to pass review and fail in use.
- **`aria-describedby` must point at text that exists** — 004 learned this the hard way when a note
  glued to a link's name broke eleven end-to-end tests. The rule's sentence is a description, never
  part of the mark's name.
- **The pathological tie** — fifty days marked, fifty tab stops — is recorded in R-3 as accepted
  rather than engineered around.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

No violations. No new project, layer, service, dependency or stored field.
