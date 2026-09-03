# Phase 0 Research: Day Summary Above the Date, and Links That Are Links

**Feature**: 004-day-summary-and-links | **Date**: 2026-09-03

Seven decisions. Three were measured rather than recalled, because each one had a plausible
answer that turns out to be wrong.

---

## R-1: The summary rows live inside the table header

**Decision**: the three summary rows become three `<tr>` inside the existing `<thead>`, placed
before the row that carries the dates.

**Rationale**: FR-013 says each day's summary must stay over its own column when the grid is
scrolled sideways. Inside the same table that is not something to implement — it is what a table
already does. Measured across 100 columns, before and after scrolling 2400 px:

```text
                col 0      col 37     col 99
unscrolled   yes 80        3114       8198
             date 80       3114       8198
after 2400px yes -2320      714       5798
             date -2320     714       5798
page body scrolls sideways: no
```

Identical to the pixel at every column, and the page body itself never scrolls — which is also
half of SC-005.

**Alternatives considered**:

- *A `<tbody>` before the `<thead>`* — would allow one element to wrap all three rows and give
  `aria-controls` a single target. It is invalid HTML: the content model puts `thead` first.
  Chromium parses it without reordering (measured), which is precisely the kind of tolerance that
  makes an invalid document survive until something else does not tolerate it.
- *A separate summary block above the table* — then the summary no longer scrolls with the
  columns, and at 100 days the counts would sit over the wrong dates. This is the alternative that
  fails the requirement outright.

---

## R-2: A disclosure control, not an expansion panel

**Decision**: the fold-out is a button carrying `aria-expanded`, not the component library's
accordion.

**Rationale**: the accordion component wraps its content in its own elements. Table rows cannot
be wrapped — a `<thead>` may contain `<tr>` and nothing else — so using it would force the
summary out of the table, which R-1 shows breaks FR-013. What an accordion provides that matters
here is the *semantics*: one control, a state that is announced, content that disappears when
folded. Those are `aria-expanded` and a conditional render, and they are available without the
component.

**On `aria-controls`**: measured that the attribute accepts a list of ids (`"a b c"`), so the
three rows could be named individually. It is deliberately **not** used: R-3 removes the rows from
the document when folded, which would leave the attribute pointing at nothing. A dangling
reference is worse than an absent optional one, and `aria-expanded` is the part assistive
technology acts on.

**Alternatives considered**:

- *Keep the rows in the document and use `aria-controls`* — trades R-3's guarantee for an
  attribute of limited support. The wrong way round.

---

## R-3: Folded means absent, not merely invisible

**Decision**: the three rows are removed from the document while folded, rather than hidden with
a style.

**Rationale**: FR-006 requires that folded counts are neither reachable by keyboard nor announced.
Both approaches satisfy it — measured, `display: none` does remove a row from the accessibility
tree:

```text
unfolded        "Vielleicht" in the tree = true    "Dienstag" = true
display:none    "Vielleicht" in the tree = false   "Dienstag" = true
```

Absence is chosen anyway, for two reasons. It cannot be undone by accident: a style can be
overridden by a later rule, a print stylesheet or a browser setting, and then hidden content is
suddenly announced. And with 100 days, folded means 303 cells are never built rather than built
and then painted over.

**Consequence**: unfolding builds those 303 cells. That is the real cost behind SC-005, and it is
small because the grid pages at fifty responses — the summary spans every day but the grid never
holds more than one page of rows.

---

## R-4: The control sits directly above the grid

**Decision**: the button is placed immediately above the table, aligned left, inside the results
card's body — not in the card's header beside the response count.

**Rationale**: a disclosure is understood by proximity. Its content appears directly beneath it,
so it belongs directly above that content. The card header already carries the heading and the
response count; a third thing there would be a control at one corner of the card revealing rows at
the opposite corner.

**Alternatives considered**:

- *In the card header, beside the count chip* — tidier as layout, worse as a disclosure. The
  distance between control and content is exactly what a reader has to bridge to understand what
  the control does.

---

## R-5: The links

**Decision**: each displayed address becomes an anchor that opens in a new tab, marked so the
opened page gets no handle on the tab that opened it, with a visually hidden note that a new tab
is what will happen.

**Rationale**: FR-016 (the admin area is left standing), FR-016a (the context switch is announced,
not merely performed) and FR-016b (no handle on the opener). The hidden note is appended to the
link's own text so the visible text stays the bare address — FR-017 keeps that address selectable
and copyable, because these links are pasted into chat windows far more often than they are
clicked.

**Consequence for existing tests**: six tests read the address with `textContent()`. An anchor has
the same text content as the code element it replaces, so all six keep passing unchanged. This
change is additive to what is already asserted.

---

## R-6: Where each requirement is verified

**Decision**: structure and state in component tests; keyboard, the new tab, and the participant
surface end to end; the size case in a component test with synthetic data.

**Rationale**: the split follows what each level can actually see.

| Requirement | Verified in | Why not elsewhere |
|---|---|---|
| Folded on arrival, both surfaces (FR-003) | Component + e2e | The component test pins the default; only the browser proves it on the real participant page |
| Three rows above the date (FR-001, FR-002) | Component | Position in the document, no browser needed |
| Labels aligned with the name column (FR-001a) | Component | Structural: same column index |
| Not announced while folded (FR-006) | e2e | Needs a real accessibility tree |
| Keyboard operation (FR-005) | e2e | jsdom has no focus model worth trusting |
| Alignment while scrolling (FR-013) | e2e | Needs layout; jsdom computes none |
| New tab, admin tab untouched (FR-016, SC-006) | e2e | Two tabs is a browser fact |
| Unfolding at the limits (SC-005) | Component | 100 days × one page of 50 rows can be handed in as props; a real poll of that size would cost minutes to seed and prove nothing extra |

**Note on jsdom**: the vertical-centring defect in feature 003 was invisible to component tests
because jsdom computes no layout. Everything in the table above that depends on position, focus
or the accessibility tree is therefore end-to-end on purpose, not by preference.

---

## R-7: The two tests that must move with the feature

**Decision**: `frontend/tests/unit/ResultGrid.spec.ts` and `e2e/tests/date-poll-journey.spec.ts`
are re-pointed at the new position and the new default, never relaxed (FR-021).

**Rationale**: both assert the counts where they are today.

- The component test looks for the totals rows. It must now assert that they are **absent** until
  unfolded, and that after unfolding they precede the date row.
- The end-to-end journey reads the counts after answers are recorded — and will fail, because the
  rows no longer exist on arrival. It must unfold first. That is a real change in what a person
  does, and the test should show it rather than reach past it.

**Consequence**: a re-pointed test that still fails is a defect in the feature, not in the test.
The counts it checks (`Ja: 2`, `Nein: 1` and so on) do not change.

---

## R-8: Wording lives in the translations

**Decision**: the control's label, its two state names, and the new-tab note are translation keys,
like every other user-facing string.

**Rationale**: 002 FR-029 and the scanner that enforces it, which since feature 003 also checks
`alt`. A literal here would be caught, which is the point of having the scanner — but writing it
correctly the first time is cheaper than being caught.
