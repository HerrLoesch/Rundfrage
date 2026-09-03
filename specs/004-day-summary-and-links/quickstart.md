# Quickstart: Day Summary and Followable Addresses

**Feature**: 004-day-summary-and-links | **Date**: 2026-09-03

What this feature must make true, as the reader and the operator will meet it. Nothing about
starting, configuring or backing up the system changes — the 003 quickstart stands unaltered.

---

## Seeing it

```bash
docker compose up -d --build
```

Sign in, create a poll with a few days, answer it from the participant link a couple of times,
then open the results.

**On arrival**: the dates are the first thing under the heading. No counts anywhere — not at the
top, not at the bottom. Above the table sits one control.

**One click, or Enter on the focused control**: three rows appear *above* the dates — *Ja*,
*Vielleicht*, *Nein*, each with its mark on the left in the same column as the participant names,
and one number per day.

**Click again**: they are gone, and the grid is exactly as it was.

The same is true on the participant page. There is one behaviour, not one per audience — that was
the first thing clarified, and FR-003 records it.

---

## The three numbers

They mean what they always meant, and it is worth restating because moving them to the top invites
a new misreading:

- **They cover every response**, not the fifty on the page in front of you (FR-008a). A poll with
  1000 answers shows all 1000 in the summary while the grid shows the first fifty.
- **They need not add up.** A day someone left blank is counted in none of the three (FR-009,
  inherited from 002 FR-033). *Ja 4, Vielleicht 1, Nein 2* under seven responses is not a bug.
- **Zero is shown as zero.** A day nobody chose reads `0 0 0`, not an empty space (FR-011) — the
  difference between "nobody could" and "nobody was asked" has to stay visible.

A poll nobody has answered shows neither the control nor the counts, just the existing message
(FR-012). A control that unfolds to zeros in every column is furniture.

---

## Addresses

Every address the system shows is now a link:

| Where | Points at |
|---|---|
| Each card in the poll list | That poll's participant view |
| The confirmation after creating a poll | The same |
| The confirmation after answering | Your own answer, ready to revise |

Each opens **in a new tab**, so the page you were on is still there when you come back — the admin
list keeps its scroll position, its open results and its unfolded summary. A screen reader is told
that a new tab will open before it opens (FR-016a).

The address itself is still plain selectable text, and the copy button still works (FR-017). These
links are pasted into chat windows far more often than they are clicked, and that has to keep
being the easy path.

---

## Verifying it

```bash
cd frontend && npm run test:unit     # structure, default state, the size case
cd e2e && npx playwright test        # layout, focus, the accessibility tree, the second tab
```

**What lives where, and why** — the split is not a preference:

| Checked | Where | Because |
|---|---|---|
| Folded on arrival; three rows above the date; labels aligned | Component | Document structure; no browser needed |
| Unfolding at 100 days within 1 s | Component | The grid pages at fifty rows, so this is 303 cells handed in as props — a real poll of that size would cost minutes to seed and prove nothing more |
| Operable by keyboard alone | End to end | jsdom has no focus model worth trusting |
| Counts absent from the reading order while folded | End to end | Needs a real accessibility tree |
| Each summary stays over its day while scrolling | End to end | jsdom computes no layout |
| New tab opens; the admin tab is untouched | End to end | Two tabs is a browser fact |

The last three are the reason this feature is not "just a component change". Feature 003 shipped a
logo four pixels off centre with every component test green, because jsdom computes no layout at
all. Anything positional here is checked in a browser on purpose.

---

## Two tests that had to move

Neither was deleted and neither was relaxed (FR-021):

- `frontend/tests/unit/ResultGrid.spec.ts` looked for the totals rows. It now asserts they are
  **absent** until unfolded, and that they precede the dates afterwards.
- `e2e/tests/date-poll-journey.spec.ts` reads the counts after recording answers. It has to unfold
  first — because a person does too. A test that reached past the control would be testing a
  journey nobody takes.

If a re-pointed test still fails, the feature is wrong, not the test.
