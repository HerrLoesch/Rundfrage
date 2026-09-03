# Specification Quality Checklist: SQLite Storage and JSON Export

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-03
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) — see Note 1
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders — see Note 1
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — both resolved in iteration 2
- [x] Requirements are testable and unambiguous — all 43 pass
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

**Note 1 — naming SQLite is not an implementation leak here.** The feature *is* a storage change,
and the constitution's Technology Constraints already pin the choice as of v2.0.0. The
specification therefore references it as a given rather than deciding it, exactly as feature 001
referenced its stack. Every requirement is phrased as an observable outcome — "exactly one
container", "copying the file reproduces the state", "the 1001st concurrent submission is
refused" — none as a technical mechanism. FR-010 makes the distinction explicit: the guarantee is
required, the mechanism is not.

**Note 2 — this specification supersedes requirements in two earlier features.** A storage change
cannot leave "exactly two containers" (001 FR-003, SC-010) standing. Rather than let the specs
quietly contradict each other, the affected requirements are listed in a Superseded Requirements
table with what replaces them. That table is the thing to check first if 001 or 002 ever appear
to fail.

**Note 3 — the user's original idea was deliberately not taken as written.** Storing JSON files as
the system of record loses answers when two people submit at the same instant, which feature 002
already guards against and tests. The Rejected Alternatives section records why, and the readable
output the idea was really after is delivered as an export instead. This is a case where the
specification argues with its own input; that argument belongs in writing.

**Note 4 — the risky half is not the storage, it is the concurrency.** SC-006 and SC-007 exist
because a storage swap is exactly the kind of change that silently weakens a guarantee nobody
re-checks. FR-029 requires those two to be proven against the *new* storage, not inherited from
the old suite.

**Note 5 — clarification outcomes.** The export is on-demand only; nothing is mirrored to disk.
The diagnostic page is removed entirely.

The second answer reaches further than it looks, and the specification says so rather than
letting it be discovered during implementation. The text endpoint from feature 001 was read by
that page and nothing else, so it becomes dead code and goes too (FR-023). With it go six of
feature 001's functional requirements and four of its success criteria — all listed in the
Superseded Requirements table with what replaces them.

That is a real cost, taken deliberately: the walking skeleton proved the chain when there was no
product to prove it, and feature 002 now proves it on every end-to-end run. **FR-024b and SC-014
exist to stop the removal taking assertions with it** — tests that used the diagnostic as a
convenient surface get re-pointed at the product rather than deleted, and the count of product
assertions may not fall.

FR-030 and SC-013 guard the other failure mode of a removal: orphaned routes, components and
stores that nobody notices because nothing references them any more.

**Note 6 — `/speckit-clarify` session 2026-09-03.** Four further questions raised the spec from
33 to 43 requirements and from 14 to 19 success criteria. Two of them found statements that were
already in the spec and not actually true:

- **FR-003 promised something the chosen storage does not deliver.** "Copying that file is
  sufficient" is false while the system runs: freshly committed writes live in companion files,
  so a copy of the main file alone is missing the most recent answers, and a mid-write copy can
  be torn. The damage surfaces only on restore. FR-003 now requires the system to produce a
  consistent copy, and FR-003b names the hand-copy route as unsupported.
- **The spec argued from durability without requiring it.** Rejecting an in-memory store because
  it "loses answers on an unexpected stop" is reasoning that nothing enforced; a configuration
  default would have decided it. FR-012a now requires that a confirmation seen by a participant
  survives a power loss.

The other two recorded decisions that would otherwise have been made by omission: the change in
how easily the data can be read at rest (FR-007a–c — the exposure is not new, the *bar* for it
is), and whether the export is something anyone may build on (FR-020a–c: versioned, additive,
with no promise of permanence).

- All checklist items pass. Validation completed in 3 of a permitted 3 iterations.
- Ready for `/speckit-plan`.
