---
description: "Task list for feature 006: highlighting the best days"
---

# Tasks: Highlighting the Best Days

**Input**: Design documents from `/specs/006-highlight-best-days/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: MANDATORY (Constitution Principle II). Every behaviour task is preceded by the test that
defines it, and that test MUST be observed failing for the intended reason before implementation.

**Organization**: grouped by user story. Both stories are frontend-only and share one rule, which is
why the rule is Foundational rather than owned by either — see the note on Phase 2.

**No backend work exists in this feature.** `ResultsProjection` already counts over the whole poll
while paging only the response rows (research R-1), so nothing in `backend/` is touched.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: parallelizable — different file, no dependency on an incomplete task
- **[Story]**: US1, US2

## Path Conventions

Frontend only: `frontend/src/`, `frontend/tests/unit/`, `e2e/tests/`.

---

## Phase 1: Setup

**Purpose**: a known-good starting point, and nothing else. There was a shared test fixture here;
it was removed because `bestDays.spec.ts` was its only consumer, and a support module used by one
file is an abstraction without a second use (Principle III). The helper is local to that file
instead — see T002.

- [X] T001 Run the full suite and record the baseline (expected: 290 backend, 129 frontend green) so any later red is attributable to this feature

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: the ranking rule as a pure function.

**⚠️ It blocks both stories, and that is deliberate.** The rule is roughly ten lines and answers
every case in `data-model.md` §1, including the ties and the empty cases. Splitting it across the
two stories to make each "own" part of it would divide one function into halves that cannot be
tested apart. US1 and US2 are therefore the rendering and behaviour slices, not arithmetic slices.

**⚠️ Write the tie tests before the winner test.** A `reduce` for the maximum returns one day and
looks correct in the grid while silently picking the first of several (plan, Risks).

- [X] T002 Write failing tests for ties in `frontend/tests/unit/bestDays.spec.ts`, with a local `totalsFor` helper so the nine cases of `data-model.md` §1 read as nine lines rather than nine object literals — local, because this file is its only consumer: two days tied on *yes* and *no*, three tied, and every day tied, each returning all of them and never the first (FR-002, SC-002)
- [X] T003 Write failing tests for the ordinary path in `bestDays.spec.ts`: a clear winner by *yes*, a tie on *yes* broken by fewer *no*, and a poll with a single candidate day, which is trivially best (FR-001a, spec Edge Cases)
- [X] T004 Write failing tests for the minimum-*yes* guard in `bestDays.spec.ts`: no responses at all, only *maybe* and *no* given, and a day nobody answered against a day everyone declined — none of them yields a mark (FR-001b, FR-009, SC-002a)
- [X] T005 Write a failing test in `bestDays.spec.ts` asserting that *maybe* never changes the result: two polls differing only in their *maybe* counts return the same best days (FR-001a)
- [X] T006 Implement the rule in `frontend/src/composables/useBestDays.ts` — candidates with `yes > 0`, then the highest *yes*, then the fewest *no* among those — as a pure `bestDays(totals)` returning a set of day ids, wrapped by a `useBestDays(totalsRef)` returning a `computed` of it. The pure half is what the nine-case table tests (research R-4); the wrapper is what the component consumes and is why the file belongs in `composables/`
- [X] T007 Verify T002–T005 pass and the baseline from T001 is still green

**Checkpoint**: the rule is correct and provably so, without anything having been rendered.

---

## Phase 3: User Story 1 — Read which day works best (Priority: P1) 🎯 MVP

**Goal**: the best day is marked in the unfolded summary, and a reader sees it without reading a
count.

**Independent Test**: mount the results grid with one day clearly ahead, unfold the summary, and
confirm that day — and only that day — carries the mark.

### Tests for User Story 1 ⚠️ Write first, observe failing

- [X] T008 [US1] Render test in `frontend/tests/unit/ResultGrid.spec.ts`: with one day ahead, exactly one mark appears, on that day (FR-001, US1 scenario 1)
- [X] T009 [US1] Render test in `ResultGrid.spec.ts`: the mark sits in the **yes** row's cell only — the *maybe* and *no* cells of the same day carry none (research R-2)
- [X] T010 [US1] Render test in `ResultGrid.spec.ts`: while the summary is folded, no mark exists anywhere in the grid (FR-008a, ui-contract §4)
- [X] T011 [US1] Render test in `ResultGrid.spec.ts`: unfold, fold, unfold again — the mark is there both times and names the same day, computed afresh rather than remembered from the first unfolding (spec Edge Cases)
- [X] T012 [US1] Accessibility test in `ResultGrid.spec.ts`: the mark carries the accessible name „bester Tag", and the rule is a *description* rather than part of that name (FR-005, ui-contract §2)
- [X] T013 [US1] Accessibility test in `ResultGrid.spec.ts`: the mark is focusable and carries the rule as `title`, so hover and keyboard focus both reveal it (FR-007, SC-004a, research R-3)
- [X] T014 [US1] Accessibility test in `ResultGrid.spec.ts`: the mark is distinguishable without colour — it carries an icon or text, not a colour class alone (FR-005, SC-004)
- [X] T015 [US1] Regression test in `ResultGrid.spec.ts`: paging through responses does not change which day is marked, because the totals are whole-poll (FR-003, SC-005, research R-1)
- [X] T016 [US1] Regression test in `ResultGrid.spec.ts`: every count keeps its value and position and the days keep their order — the grid differs only by the mark. Extend the existing limits test at `ResultGrid.spec.ts:240` (100 days, 1000 responses) so the marks are asserted there too (FR-008, SC-006, spec Edge Cases)

### Implementation for User Story 1

- [X] T017 [US1] Render test in `ResultGrid.spec.ts`: when the totals change — a response added, revised or deleted — the mark moves to the day that now leads, without anything being invalidated (FR-006, US1 scenario 3)
- [X] T018 [P] [US1] Add „bester Tag" and the rule sentence to `frontend/src/locales/de.json`; `noLiteralStrings.spec.ts` already fails any template carrying them inline
- [X] T019 [US1] Render the mark in the *yes* row of `frontend/src/components/poll/ResultGrid.vue`, driven by the set from `useBestDays.ts`, with the accessible name, the `title`, and `aria-describedby` pointing at the rule (ui-contract §1–§3)
- [X] T020 [US1] Verify T008–T017 pass and the baseline is still green

**Checkpoint**: US1 is complete and shippable on its own. This is the MVP.

---

## Phase 4: User Story 2 — See honestly when there is no single best day (Priority: P2)

**Goal**: every tied day is marked, and a poll where no day has a *yes* shows no mark at all.

**Independent Test**: mount the grid with two days tied at the top and confirm both are marked;
mount it with no responses and confirm nothing is.

**Note**: if T019 renders from the set the rule returns rather than from a single id, this story
needs **no implementation** — it is the behaviour the rule already provides, verified at the surface
where an operator would see it fail. That is the intended outcome, not a gap. If any task below
requires a change to `ResultGrid.vue`, the rendering in T019 was written against a single day and
should be corrected there.

### Tests for User Story 2 ⚠️ Write first, observe failing

- [X] T021 [US2] Render test in `ResultGrid.spec.ts`: two days tied at the top are both marked, and the third day is not (FR-002, US2 scenario 1)
- [X] T022 [US2] Render test in `ResultGrid.spec.ts`: three days tied among ten leave exactly those three marked (US2 scenario 2)
- [X] T023 [US2] Render test in `ResultGrid.spec.ts`: a poll with no responses shows no mark, and no line explaining that no day leads (FR-001b, SC-002a, ui-contract §4)
- [X] T024 [US2] Render test in `ResultGrid.spec.ts`: a poll answered only with *maybe* and *no* shows no mark, whatever the *no* counts (FR-001b)
- [X] T025 [US2] Render test in `ResultGrid.spec.ts`: the marks are identical for every tied day — no primary and secondary marking (FR-002, ui-contract §4)

### Implementation for User Story 2

- [X] T026 [US2] ~~Correct the rendering~~ **not needed**: T021–T025 passed unchanged, because T019 renders from the set the rule returns. This was the intended outcome (see the Phase note), not a skipped step. Correct the rendering in `frontend/src/components/poll/ResultGrid.vue` **only if** T021–T025 revealed that it was written against a single best day rather than a set
- [X] T027 [US2] Verify T021–T025 pass and the baseline is still green

**Checkpoint**: both stories work, and the tie is proven at the surface rather than only in the rule.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T028 [P] End-to-end test in `e2e/tests/results-summary.spec.ts`: a participant unfolds the summary of an answered poll and sees the best day marked, on the participant path (constitution gate 3)
- [X] T029 [P] Confirm the creator's view marks the same day as the participant's, in `e2e/tests/results-summary.spec.ts` (FR-004, SC-003)
- [X] T030 Run `npm run typecheck` — the `.vue` template is type-checked since feature 005, so a mark bound to a field that does not exist is caught here rather than in a browser
- [X] T031 Run the full suite — backend, frontend, end-to-end — and confirm green (constitution gate 2)
- [X] T032 Open a real poll with twenty days and several answers and confirm SC-001 by hand: the best day is nameable in under five seconds without reading a count

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 Setup**: no dependencies
- **Phase 2 Foundational**: after Setup. **Blocks both stories**
- **Phase 3 US1**: after Phase 2
- **Phase 4 US2**: after Phase 3 — it asserts against the rendering US1 introduces
- **Phase 5 Polish**: after both stories

### User Story Dependencies

- **US1 (P1)**: independent once the rule exists. Ships alone as the MVP
- **US2 (P2)**: verifies the tie and empty behaviour at the surface US1 renders, so it follows US1
  rather than running beside it. Its tests are still independent of US1's

### Within Each Story

Tests written and failing → implementation → verification. No implementation task starts before the
test that defines it has been seen to fail.

### Parallel Opportunities

Very limited, by the shape of the feature rather than by choice: almost every test lands in one of
two files, and two people editing `ResultGrid.spec.ts` at once would spend more time merging than
testing. Only two things are genuinely parallel.

- T018 (the locale file) alongside T008–T017
- T028 and T029 together — both in the end-to-end file, but written as one pass

---

## Parallel Example: Phase 2

```bash
# The rule's tests share one file, so they are written in sequence - but the order matters more
# than the parallelism: ties first, because that is the case a naive implementation drops.
Task: "Tie tests in bestDays.spec.ts"          # T002
Task: "Ordinary path in bestDays.spec.ts"      # T003
Task: "Minimum-yes guard in bestDays.spec.ts"  # T004
```

---

## Implementation Strategy

### MVP (User Story 1 only)

Phase 1 → Phase 2 → Phase 3 → **stop and validate**. A marked best day, correct for every case the
rule covers, with the tie behaviour already right but not yet proven at the surface.

### Incremental delivery

1. Setup + the rule
2. **US1** → validate → ship (MVP)
3. **US2** → validate → ship — the proof that ties and empty polls behave, where a reader would see
   them fail

Stopping after US1 is coherent: the rule is complete, so ties already work. What US2 adds is the
evidence, which is worth having and is not worth blocking a release for.

---

## Notes

- **T002 comes before T003 on purpose.** The tie is the case a naive maximum drops, and writing the
  winner test first makes it easy to write an implementation that passes it and is wrong
- **T004 is not an edge case, it is every new poll.** A poll with no answers has no day with a *yes*,
  and without the guard a day nobody answered would beat a day everyone declined
- **T015 guards a requirement nothing else enforces.** FR-003 is true because of how
  `ResultsProjection` is written today; this test is the only thing that would notice if that changed
- Commit after each task or logical group
