---
description: "Task list for Day Summary Above the Date, and Links That Are Links"
---

# Tasks: Day Summary Above the Date, and Links That Are Links

**Input**: Design documents from `/specs/004-day-summary-and-links/`
**Prerequisites**: plan.md, spec.md, research.md, contracts/ui-contract.md, quickstart.md

**Tests**: Test tasks are MANDATORY (Constitution Principle II). Every behaviour task is preceded
by the test task that defines it, and that test MUST be observed failing first.

**Organization**: grouped by user story. US1 (the summary) is the MVP; US2 (the addresses) is
independent of it and could ship on its own — or first, since it is far smaller.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel — different files, no dependency on an incomplete task
- **[Story]**: US1 or US2; Setup and Polish carry no story label

## Two things to know before starting

**Three files change.** `ResultGrid.vue`, `ShareLink.vue`, `PollList.vue`, plus translations. No
backend file, no request, no stored value (FR-019, FR-020). If the change set grows past that,
something has been misunderstood.

**Most of the proof is in a browser, and that is not a preference.** Position after a sideways
scroll, focus order, what a screen reader is told, a second tab — jsdom computes none of it.
Feature 003 shipped a logo four pixels off centre with every component test green. Research R-6
lists which requirement is checked where and why; this list follows it.

---

## Phase 1: Setup

**Purpose**: the words. Both stories add translation keys to the same file, so the file is touched
once, here — which is also what lets the two story phases run in parallel afterwards.

- [X] T001 Add the summary control's keys to `frontend/src/locales/de.json`: its name, and the two state names it announces when folded and unfolded (FR-005)
- [X] T002 Add the new-tab note to `frontend/src/locales/de.json` — the visually hidden text appended to every address so a screen reader hears that a new tab will open before it opens (FR-016a)

---

## Phase 2: Foundational

**There is none, and that is worth stating rather than filling.** The two stories touch different
components; their only shared file is the translations, handled in Phase 1. Inventing a
foundational task here would create a dependency that does not exist and would stop the two
phases running side by side.

---

## Phase 3: User Story 1 — The tally is at the top, and out of the way until wanted (P1) 🎯 MVP

**Goal**: the three per-day rows move above the dates and start folded, on both surfaces.

**Independent test**: open a poll with answers — no counts anywhere, dates first. Unfold: three
rows appear above the dates. Nothing below the responses.

### Tests that must fail first

- [X] T003 [US1] Re-point the totals test in `frontend/tests/unit/ResultGrid.spec.ts`: on arrival there is no `summary-row` and no `totals-row` at all. This is the failing test for the whole story, and it is a re-point rather than a deletion — the counts it checks do not change, only where and when they appear (FR-021, research R-7)
- [X] T004 [US1] Extend `frontend/tests/unit/ResultGrid.spec.ts`: after the control is operated, exactly three `summary-row` elements exist, carrying `data-state` `yes`, `maybe`, `no` in that order, each with the counts the old footer showed (FR-001)
- [X] T005 [US1] Extend `frontend/tests/unit/ResultGrid.spec.ts`: every `summary-row` precedes the row carrying the dates in document order — the assertion that makes "above the date" mean something a test can read (FR-001, SC-002)
- [X] T006 [US1] Extend `frontend/tests/unit/ResultGrid.spec.ts`: no element carries `totals-row`, and the table has no footer. The old identifier must not be reused for the new rows, or a test nobody updated would pass against something it was not written for (FR-002, SC-003)
- [X] T007 [US1] Extend `frontend/tests/unit/ResultGrid.spec.ts`: each summary row's first cell sits in the same column as the participant names and carries the state's name and mark (FR-001a, FR-010)
- [X] T008 [US1] Extend `frontend/tests/unit/ResultGrid.spec.ts`: a day nobody answered shows `0` in all three rows rather than an empty cell, and the three numbers for a day still need not add up to the response count — an unanswered day is counted in none of them (FR-009, FR-011)
- [X] T009 [US1] Extend `frontend/tests/unit/ResultGrid.spec.ts`: with no responses at all, neither `summary-toggle` nor any `summary-row` exists, and the existing empty-state message still stands alone (FR-012)
- [X] T010 [US1] Extend `frontend/tests/unit/ResultGrid.spec.ts`: `summary-toggle` reports `aria-expanded="false"` on arrival and `"true"` after being operated; folding or unfolding issues no request and writes nothing anywhere; and a freshly mounted grid is folded again, so nothing about the previous reader survives (FR-005, FR-007, FR-008)
- [X] T011 [US1] Extend `frontend/tests/unit/ResultGrid.spec.ts` with the size case: a poll of 100 days and one page of 50 responses, handed in as props, unfolds within 1 second. The grid pages at fifty rows, so this is 303 cells and not 100,000 — a real poll of that size would cost minutes to seed and prove nothing more (SC-005, research R-6)

### Implementation

- [X] T012 [US1] In `frontend/src/components/poll/ResultGrid.vue`, move the three state rows out of `<tfoot>` and into `<thead>`, before the row carrying the dates; rename their identifier from `totals-row` to `summary-row` and delete the now-empty footer (FR-001, FR-002)
- [X] T013 [US1] In `frontend/src/components/poll/ResultGrid.vue`, add the fold state — folded on arrival, held in the component and nowhere else — and render the three rows only while unfolded, so the folded counts are absent from the document rather than styled invisible (FR-003, FR-006, FR-007, FR-008, research R-3)
- [X] T014 [US1] In `frontend/src/components/poll/ResultGrid.vue`, add the `summary-toggle` button directly above the table, left aligned, carrying `aria-expanded` and its name from the translations. Deliberately no `aria-controls`: the rows do not exist while folded, and a reference to nothing is worse than an omitted optional attribute (FR-004, FR-005, research R-2, R-4)
- [X] T015 [US1] In `frontend/src/components/poll/ResultGrid.vue`, hide the control and the rows entirely when the poll has no responses (FR-012)

### End to end — the parts jsdom cannot see

- [X] T016 [US1] Re-point `e2e/tests/date-poll-journey.spec.ts`: the test that reads the counts after answering must unfold the summary first, because a person does too. Reaching past the control would be testing a journey nobody takes. The expected numbers are unchanged (FR-021, research R-7)
- [X] T017 [US1] Create `e2e/tests/results-summary.spec.ts` and assert the summary can be unfolded and folded with the keyboard alone, with the control's state readable in both (FR-005, SC-004)
- [X] T018 [US1] Extend `e2e/tests/results-summary.spec.ts`: while folded, the counts are absent from the accessibility tree; while unfolded, they are in it. Measured with the browser's own tree, not with a visibility check — an element can be invisible and still announced (FR-006)
- [X] T019 [US1] Extend `e2e/tests/results-summary.spec.ts`: with 100 candidate days, each summary cell keeps the same horizontal position as its own date cell after the grid is scrolled sideways, and the page body itself does not scroll. This is the requirement the whole placement decision rests on (FR-013, SC-005)
- [X] T020 [US1] Extend `e2e/tests/results-summary.spec.ts`: a participant opening a poll link with no account finds the summary folded there too, and the same single action unfolds it (FR-003, FR-014, SC-001)

**Checkpoint**: US1 is independently testable and shippable.

---

## Phase 4: User Story 2 — An address that looks like a link is one (P2)

**Goal**: all three displayed addresses become links that open in a new tab.

**Independent test**: create a poll, click the address on its card, land on the participant view
in a new tab, switch back and find the admin area untouched.

### Tests that must fail first

- [X] T021 [P] [US2] Extend `frontend/tests/unit/PollList.spec.ts`: `poll-list-link` is an anchor whose destination is that poll's participant address, opening in a new tab, marked so the opened page gets no handle on the opener (FR-015, FR-016, FR-016b)
- [X] T022 [P] [US2] Create `frontend/tests/unit/ShareLink.spec.ts`: both addresses this component renders — the creator's after creating a poll and the participant's after answering — are anchors with the same three properties (FR-015, FR-015a)
- [X] T023 [US2] Extend `frontend/tests/unit/ShareLink.spec.ts`: each anchor's accessible name is the address followed by the hidden new-tab note, and its plain text content is still the bare address and nothing else (FR-016a, FR-017)

### Implementation

- [X] T024 [P] [US2] In `frontend/src/components/admin/PollList.vue`, replace the code element holding the address with an anchor carrying destination, new-tab target, the no-handle relationship and the hidden note (FR-015, FR-016, FR-016a, FR-016b)
- [X] T025 [P] [US2] In `frontend/src/components/poll/ShareLink.vue`, make the same replacement. One edit here covers two of the three addresses, which is why the clarification came out as "all three" rather than "the admin area only" (FR-015, FR-015a)

### End to end

- [X] T026 [US2] Extend `e2e/tests/results-summary.spec.ts` or add to `e2e/tests/admin-journey.spec.ts`: clicking a poll's address opens a second tab showing that poll's participant view, and the admin tab is unchanged when returned to — same scroll position, same open results, same fold state. The destination is exactly the address that was already on screen: a clickable link is a convenience, not a new capability (FR-016, FR-018, SC-006)
- [X] T027 [US2] Extend the suite T026 chose (`e2e/tests/results-summary.spec.ts` or `e2e/tests/admin-journey.spec.ts`): the address is still selectable text and the copy control still yields exactly the address it does today. Six existing tests read these elements with `textContent()`; if any of them needs changing, the text content has drifted and that is a regression (FR-017, SC-007, research R-5)

**Checkpoint**: US2 is independently testable and shippable.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [X] T028 Verify the change set touches no file under `backend/`, adds no call in `frontend/src/api/client.ts`, and introduces no dependency. A presentational feature that reached the backend has misunderstood itself (FR-019, FR-020)
- [X] T029 [P] Update the *Ergebnisse* section of `README.md`: the three numbers are now above the dates and folded on arrival. The paragraph explaining that they need not add up stays — it is more relevant at the top than it was at the bottom
- [X] T030 [P] Add a sentence to the *Ergebnisse* section of `README.md` stating that the counts cover every response, not the fifty on the page in view. Moving a total to the head of a table invites exactly that misreading (FR-008a)
- [X] T031 Run `npm run test:unit` in `frontend/` and `npx playwright test` in `e2e/` against the built container set; report the counts (FR-021, SC-008)
- [X] T032 Run `dotnet test backend/Rundfrage.slnx` — unchanged by this feature, and that is the point: it must still pass without a single edit
- [X] T033 Look at it in a browser against `compose.yaml`. Open a poll with three days and one with a hundred, fold and unfold both, and click an address. Every requirement here is about what a person sees, and the one defect feature 003 shipped was one that only looking would have caught

---

## Dependencies

```text
Setup (T001-T002)          the translations both stories need
   |
   +--> US1 (T003-T020)    MVP
   |
   +--> US2 (T021-T027)    independent; smaller; could ship first
             |
Polish (T028-T033)
```

**Within US1**: T003 must be *observed failing* before T012. T012 before T013 before T014 (each
builds on the previous shape). T016 will fail the moment T013 lands and must be re-pointed in the
same breath, not later — a red suite between two commits is how a re-point gets forgotten.

**Within US2**: T021 and T022 are different files and can run together; T024 and T025 likewise.
T023 needs T022's file to exist.

**Across stories**: nothing. After Phase 1 they share no file.

## Parallel execution examples

**Setup**: T001 and T002 edit the same file and are therefore *not* parallel — they are two keys,
one edit.

**US1**: T004 through T011 all extend one spec file, so they are sequential; T012 through T015
likewise in one component. The end-to-end tasks T017 to T020 build one new file and are sequential
within it, but the whole group can run alongside US2.

**US2**: T021 with T022 (two spec files), then T024 with T025 (two components).

**Polish**: T029 with T030 are the same README section — one edit, not two.

## Implementation strategy

**MVP = Phase 1 + US1.** That is the change that was actually asked for first and the one that
alters how the results read.

**But consider shipping US2 first.** It is four small tasks against three files and delivers a
daily irritation removed. Nothing in US1 depends on it. If the summary work turns out to be
fiddlier than it looks, US2 is already in.

**The riskiest task is T019**, and it is late only because it needs the implementation to exist.
The whole placement decision rests on the summary staying over its own column when a hundred days
scroll sideways. That was measured during planning against a synthetic table (research R-1);
T019 is where it has to be true of the real component. If it is not, the design is wrong and no
amount of test adjustment will fix it.

---

## What actually happened

Three departures from the list, all worth recording.

**The new-tab note was written inside the link, and eleven end-to-end tests said so.** The plan and
the UI contract both promised the text content would stay the bare address, so the six suites that
read these addresses with `textContent()` would keep passing untouched. Putting the hidden note
*inside* the anchor broke that promise the moment it was written: the note became part of the
address, every navigation built from it went nowhere, and five more tests fell with it. The note is
now the link's **description**, referenced from outside — which is what makes the contract true
rather than merely stated. The eleven failures were the contract defending itself, and the fix was
to satisfy it, not to change six tests.

**Two visual defects that only T033 could find**, both invisible to every test in the suite:

- A header cell carries a bottom border and a data cell does not, so each summary row had a rule
  under its label that stopped dead at the first number column. The three rows read as one block;
  they now carry one background and no rules between them.
- At a hundred days the name column narrows until *Vielleicht* wraps below its own mark — and the
  label beside the mark is the only thing that says which of three stacked numbers is which
  (FR-001a). The label no longer wraps.

Neither is a correctness bug and neither would ever have failed a test. They are the reason T033
says "look at it" and is a task rather than a note.

**The grey patch behind the control was not a defect.** It looked like one in the first
screenshots. Measured: the button's own overlay at opacity 0.04, lingering after the click that
opened the summary — a focused button looking focused. Recorded because the next person to
screenshot this will see it too.
