<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:

- **Active plan**: `specs/010-survey-builder/plan.md`
- Specification: `specs/010-survey-builder/spec.md`
- Design decisions and rejected alternatives (R-1 … R-14): `specs/010-survey-builder/research.md`
- The four new tables, the validation rule table and the migration:
  `specs/010-survey-builder/data-model.md`
- UI contract (areas, routes, the seven per-type controls, states, test ids):
  `specs/010-survey-builder/contracts/ui-contract.md`
- The new endpoints: `specs/010-survey-builder/contracts/openapi.yaml`
- Developer guide, including the five rules that are easy to break:
  `specs/010-survey-builder/quickstart.md`

Five things this plan carries that are easy to get wrong:

- **A field's type never changes after creation.** No endpoint, no request shape, writes a new
  `Type` onto an existing `FormField`; changing the kind of answer a question collects means
  deleting the field and adding a new one (spec clarification 2026-09-22, `quickstart.md` #1).
- **"Unanswered" is the absence of a `FormFieldValue` row, not a stored value.** A required boolean
  field must render as a radio group with nothing pre-selected — a checkbox cannot represent
  "neither yet" and will silently defeat the requiredness check (`research.md` R-13).
- **The two delete-cascades run in opposite directions and both must be exactly right.** Deleting a
  `FormField` removes only the values collected for that field, across every response; deleting a
  `FormResponse` removes only the values collected in that response, across every field. A test for
  one does not exercise the other (`quickstart.md` #3).
- **This feature adds no `CreatorId` and no `OwnerScope` method.** It is deliberately operator-only
  (spec FR-032); do not add an owner column "for consistency" with Polls/WishLists — there is no
  second caller for it here (`research.md` R-3).
- **The public form-fetch route treats a zero-field form exactly like an unknown token.** Do not
  give "exists but has no fields yet" a distinguishable response — the contract only has
  found/not-found (FR-011, FR-021).

Cross-feature consequences still in force from features 008 and 009:

- Feature 007 FR-034 is amended by 008 FR-048b (wish-list titles allowed on the dashboard;
  participant names and poll titles still not) and extended by 009 FR-042 (Ersteller names, same
  grounds). Feature 010 does not extend this further — forms do not appear on the dashboard at all
  (`research.md`, data-model.md §8): the spec makes no dashboard requirement for this content type,
  and adding one is a decision left to a future feature, not implied by this one.
- Feature 009's `OwnerScope` and closed capability list (FR-028c) remain exactly as 009 left them;
  feature 010 does not touch either, per its own FR-032.

- Application-wide layout and type system (not feature-scoped): `specs/design-system.md`.
  Read it before changing any component's spacing, widths or headings — and note the finding it
  records: Vuetify's precompiled CSS ships no typography scale, so `text-h4` and friends only work
  because `frontend/src/styles/app.css` supplies them.

- Governing constitution (v2.0.1): `.specify/memory/constitution.md`

Completed:

- Feature 001 (platform scaffold): `specs/001-platform-scaffold/plan.md`
- Feature 002 (date poll): `specs/002-date-poll/plan.md`
- Feature 003 (SQLite and export): `specs/003-sqlite-and-export/plan.md`
- Feature 004 (day summary and links): `specs/004-day-summary-and-links/plan.md`
- Feature 005 (import and maintenance mode): `specs/005-import-and-maintenance-mode/plan.md`
- Feature 006 (highlight best days): `specs/006-highlight-best-days/plan.md`
- Feature 007 (admin shell and dashboard): `specs/007-admin-shell-dashboard/plan.md`
- Feature 008 (wish lists): `specs/008-wishlist/plan.md`
- Feature 009 (creator links): `specs/009-creator-links/plan.md`
<!-- SPECKIT END -->
