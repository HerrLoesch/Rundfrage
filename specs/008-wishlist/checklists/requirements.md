# Specification Quality Checklist: Wunschliste (Wish List)

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-13
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
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

**Validation run 2026-09-13 (iteration 2, after `/speckit-clarify`)** — all items pass.

The three open decisions were answered and written into the spec:

1. **FR-022 — withdrawing an entry**: only the person who made it, through a personal link issued on
   submission (feature 002's pattern). Added FR-022a–FR-022e, two edge cases, SC-014, and the
   Submission entity.
2. **FR-028a — a passed target date**: the list closes for new entries and is marked "geschlossen",
   stays readable, and reopens if the operator moves the target date. Closing is derived from the
   clock, never stored. Added FR-028a–FR-028e, SC-013, two edge cases, US2 AS7/AS8.
3. **FR-048a — the dashboard**: one row per wish list (title, target date, open/closed, entries,
   filled share), which narrowly amends feature 007 FR-034 to permit wish-list titles while keeping
   participant names and poll titles off the dashboard. Added FR-048a–FR-048d, SC-007a, US3
   AS9–AS11.

Ambiguities resolved by documented default rather than by question are recorded in **Assumptions** —
notably the two readings of "% der gewünschten Items" (FR-045a reports both) and a wanted count
lowered below the entries already made (FR-033 refuses it).

One cross-feature consequence is carried forward for `/speckit-plan`: **feature 007's FR-034 must be
amended in the same change** (FR-048b), not silently contradicted.

**Validation run 2026-09-13 (iteration 3, second `/speckit-clarify` pass)** — all items still pass.

Two internal inconsistencies found on re-scan and resolved:

4. **FR-010 — contradictory limits**: 100 items × 50 wanted = 5000 possible places against a
   1000-entry cap, which would have made FR-017 unsatisfiable and the filled share unreachable above
   20 %. The separate entry cap is gone; the sum of wanted counts per list is capped at 1000 and
   checked where the operator works (FR-010a). Scale FR-054 is unaffected (200 × 1000 = 200,000).
5. **FR-023 — rate-limit unit**: counted per submission, not per name, so the multi-item submission
   FR-016 invites costs one of the ten. Withdrawals count against the same budget (FR-023a).
   Added SC-002a.
