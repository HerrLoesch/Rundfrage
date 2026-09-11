# Quickstart: Highlighting the Best Days

**Feature**: 006-highlight-best-days | **Date**: 2026-09-06

A small feature: one pure function, three lines of an existing `v-for`, and no backend work at all.
Read R-1 before assuming otherwise.

## Running it

Unchanged:

```bash
docker compose up --build          # http://localhost:8080
./start-services.sh                # backend in the container, frontend on Vite

cd frontend && npx vitest run      # the rule and the component
cd frontend && npm run typecheck   # .vue templates included since feature 005
cd e2e && npx playwright test      # needs the system running
```

## Where the work is

| Concern | File |
|---|---|
| The rule | `frontend/src/composables/useBestDays.ts` (new) — pure `bestDays()` plus a `useBestDays()` wrapper |
| The mark | `frontend/src/components/poll/ResultGrid.vue` — the **yes** row's `<td>` only |
| The words | `frontend/src/locales/de.json` |
| Rule tests | `frontend/tests/unit/bestDays.spec.ts` (new) |
| Render tests | `frontend/tests/unit/ResultGrid.spec.ts` (exists) |

## The rule, in full

```text
candidates = days where yes > 0
if empty -> {}
topYes   = max yes among candidates
leaders  = candidates with yes == topYes
result   = leaders with the minimum no among leaders
```

`maybe` is not in it, on purpose. Every case is tabulated in `data-model.md` §1, one row per test.

## Three things that will bite you

### The tie is the case that gets dropped

A `reduce` that keeps "the day with the highest yes so far" returns **one** day, and the grid will
look right — a single mark, nothing obviously wrong — while quietly picking whichever tied day came
first. FR-002 is the requirement most likely to pass review and fail in use. Write the tie tests
first: two tied, three tied, all tied.

### A day nobody answered is not a good day

With fewest-*no* as the tie-break, a day with no answers at all (0 yes, 0 no) beats a day everyone
declined (0 yes, 5 no), because zero is fewer than five. That is why `yes > 0` is the first line of
the rule and not an afterthought. FR-009 depends entirely on it; the case is the last row of
`data-model.md` §1.

### FR-003 is already true, and nothing here keeps it true

`ResultsProjection` counts over the whole poll and pages only the response rows, so a mark derived
from `totals` cannot drift as the reader pages (R-1). Nothing in this feature enforces that. Write
the test that pages through a poll and asserts the mark does not move — it is the only thing that
would notice if the projection ever starts paging its counts.

## Two decisions that look like oversights

- **The mark is invisible while the summary is folded.** Chosen (clarification Q2): the change stays
  inside what 004 built and adds no permanent space. A reader who never unfolds never sees it.
- **`maybe` does not affect the ranking.** Chosen (clarification Q1): a weighting would mean the
  system inventing a constant about other people's answers. This is exactly why FR-007 requires the
  rule to be readable from the mark.
