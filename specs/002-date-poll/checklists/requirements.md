# Specification Quality Checklist: Date Poll (Terminfindung)

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-02
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — all three resolved in iteration 2
- [x] Requirements are testable and unambiguous — all 76 pass
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

**Note 1 — this is where Principle I stops being free.** Feature 001 satisfied Zero-Signup
Participation trivially, because it had no participant-facing flow. Here the principle drives
concrete requirements that would otherwise have been designed differently: FR-022 (the display
name is a label, never an identity), FR-026 and FR-028 (revision via a per-response token rather
than a login), and FR-031. It also forces an uncomfortable acceptance recorded in the
Assumptions and the Edge Cases: **repeat answering cannot be prevented**, because every honest
mechanism for preventing it requires identifying the participant. That is a product consequence,
not an oversight.

**Note 2 — the not-found response is a security requirement, not a UX one.** FR-027, FR-040 and
SC-012 require unknown, malformed, expired and deleted links to be indistinguishable. Without
that, the link space becomes probe-able: an attacker could learn which tokens once existed. This
is why it is stated four times rather than once.

**Note 3 — scope grew deliberately.** The project owner chose to include participant voting and
creator authentication rather than deferring either. The specification therefore spans two very
different audiences — an authenticated creator and an anonymous participant — with opposite
access rules. The five user stories are ordered so that each remains independently demonstrable
despite that split.

**Note 4 — one requirement is deliberately absent.** Nothing here lets a creator edit a poll
after creation, and Out of Scope says so explicitly. Editing candidate days after answers exist
raises a question this specification does not answer (what happens to answers for a removed
day?), and inventing a rule for it would have been speculation.

**Note 5 — clarification outcomes (iteration 2).** The three answers raised the specification
from 49 to 54 requirements and from 12 to 15 success criteria, and are recorded with their
reasoning in a new "Resolved Decisions" section.

One answer created a contradiction that had to be resolved rather than merely recorded. Making
the grid visible to participants (FR-036) collided with User Story 3's fourth acceptance
scenario, which refused *unauthenticated* access to results. Both cannot be true. The scenario
was rewritten around the actual distinction: **holding the participant link grants sight of the
grid; holding a creator session grants the admin area.** Two further consequences were written
in rather than left implicit — FR-036a (the page must disclose that names are public *before*
the name is entered) and FR-036b (the grid is readable without answering first).

The single-account decision also simplified FR-018: with one operator there is no ownership
filter, so the admin area simply lists every poll.

**Note 6 — `/speckit-clarify` session 2026-09-02.** Five further questions raised the spec from
54 to 69 requirements and from 15 to 26 success criteria. Three of the five targeted requirements
that were written but not quantified — FR-015 ("an explicit limit" with no number), FR-006 ("a
bounded period"), FR-005 ("slowed or temporarily blocked"). Those had passed the "testable and
unambiguous" item in iteration 2, which was too generous: a requirement naming no value cannot
be tested. They now carry concrete numbers.

Two questions surfaced genuine gaps rather than vague wording:

- **Nothing limited answer submission.** With FR-015a refusing the 1001st response, flooding a
  poll was a working denial of service against every real participant. Now FR-027a–c.
- **No way existed to delete a single response.** FR-037 deleted whole polls only, so one
  unwanted answer could be removed only by destroying everyone else's. Now FR-037a–b.

The generous limits chosen for FR-015 forced FR-036c and SC-016: 1000 responses across 100
candidate days is 100,000 cells, and the specification now states that the grid must survive its
own maximum rather than leaving that to be discovered during implementation.

**Note 7 — second `/speckit-clarify` pass, same day.** Three further questions (8 in total for
2026-09-02) raised the spec from 69 to 76 requirements and from 26 to 32 success criteria. This
pass also corrected three statements that earlier clarifications had silently invalidated: Key
Entities still said the creator account's "shape depends on the resolution of FR-045" after
FR-045 had been resolved, and two edge cases still described limits as "an explicit limit"
after the numbers were fixed.

What the three questions found:

- **FR-011 claimed to exclude time zones, and could not.** It read "no time zones beyond the day
  itself", but FR-014 compares days to "now" and FR-039 adds 30 days to one — both require a
  zone. Undefined, every date test at a day boundary would have been irreproducible. Now
  FR-011a/b fix Europe/Berlin, and the Assumptions entry that claimed time zones were out of
  scope was corrected rather than left contradicting the requirement.
- **A fourth answer state existed that the totals ignored.** FR-024 created *no answer*; FR-033
  counted only three states. The project owner chose to keep it uncounted, so FR-033a now states
  plainly that the totals need not sum to the response count and that the row count is what
  reveals how many people answered. FR-024a and an extension to FR-053 give the empty cell a
  defined appearance, distinct from *has no time* without relying on colour.
- **FR-040 was intermittently untrue.** It required expired links to be not-found, but nothing
  said when expiry took effect. With only a periodic job, a poll would be past its deadline and
  still answerable until the job ran. FR-039b now checks expiry on access, and FR-039c keeps the
  actual erasure that Principle IV requires.

- All checklist items pass. Ready for `/speckit-plan`.
