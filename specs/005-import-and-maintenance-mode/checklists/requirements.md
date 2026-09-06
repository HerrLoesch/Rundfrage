# Specification Quality Checklist: Importing Exported Data, and a Maintenance Mode

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-04
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

All three clarifications were answered in session 2026-09-04 and are recorded in the spec.
The answers changed the feature's shape rather than merely filling gaps:

- **Both imports are in scope**, and they were separated by *purpose* rather than by format. That
  separation is what let the second question resolve without touching the export: restoration is
  the backup's job and the backup already carries its tokens, so 003 FR-015 stands and the JSON
  export stays tokenless.
- **Skipping replaced the all-or-nothing model** for JSON import. FR-003, FR-004 and FR-012 now
  carry the weight that a single atomicity rule used to: an item may be left out, but never
  quietly. FR-013 keeps atomicity where it still belongs — a poll is created whole or not at all.
- **A whole-storage restore was added** (User Story 3, FR-016 to FR-024). It is the heaviest and
  riskiest slice: it replaces everything, and FR-020 and FR-021 exist specifically so that a failed
  restore cannot cost the operator the data they already had.

Two requirements are load-bearing in a way that is easy to lose during planning and should be
checked again at the Constitution Check gate:

- **FR-030** — maintenance state must live outside the data a restore replaces. Without it,
  restoring a backup taken before maintenance was switched on reopens the participant side in the
  middle of the maintenance window.
- **FR-031** — maintenance mode must not report the application as unhealthy. The health check
  added in `fix: make a production update safe under Coolify` is what a deployment uses to decide
  whether to roll back; a maintenance mode that failed it would have the orchestrator undo the
  deployment while the operator was working.

One deliberate wording choice: FR-031 and SC-011 name the container runtime. That is operational
vocabulary rather than an implementation detail — the constitution pins a containerised
deployment, and stating the requirement without it would obscure what it protects against.

### Clarification session 2026-09-04 (`/speckit-clarify`)

Five further questions were asked and integrated. Three closed real gaps, one closed a
contradiction this spec had introduced, and one recorded an accepted risk:

- **FR-005a / SC-007a** — an uploaded file must not outlive the import. The gap was asymmetric:
  003 FR-021 already said nothing is kept after a backup is *downloaded*, and nothing said the same
  for the upload direction, where the file is every link in the system.
- **FR-024** turned from a warning into a refusal. A restore is now impossible while maintenance
  mode is off, which removes the "participant answers during a restore" case by construction rather
  than leaving it to be handled.
- **FR-016a** resolved a contradiction of this spec's own making: FR-012 has a JSON import *skip* a
  poll past its retention date, while the restore path said nothing about the same condition. A
  restore now takes the backup as it is and reports what is already expired — filtering would make
  the restored state deliberately unequal to the backup, which FR-016 and SC-006 forbid.
- **FR-003a / SC-001a** — one request, not a job. The summary is that request's answer and is not
  retained. This keeps a second body of persistent state out of the design (Principle III).
- **No upload size limit**, on either path, decided deliberately and recorded in Assumptions with
  its accepted consequence: an oversized upload can exhaust the volume, but cannot cost data,
  because FR-020 and FR-021 hold the existing data until a restore has succeeded. It is written
  down as a decision so that it is not "corrected" later as though it were an oversight.

Checklist passes in full. Ready for `/speckit-plan`.
