<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:

- **Active plan**: `specs/009-creator-links/plan.md`
- Specification: `specs/009-creator-links/spec.md`
- Design decisions and rejected alternatives (R-1 … R-14): `specs/009-creator-links/research.md`
- The one new table, the two nullable columns and the access filter:
  `specs/009-creator-links/data-model.md`
- UI contract (areas, the single-address creator surface, states, test ids):
  `specs/009-creator-links/contracts/ui-contract.md`
- The new endpoints: `specs/009-creator-links/contracts/openapi.yaml`
- Developer guide, including the five rules that are easy to break:
  `specs/009-creator-links/quickstart.md`

Five things this plan carries that are easy to get wrong:

- **Creator handlers never receive `RundfrageDbContext`.** Every read goes through `OwnerScope`,
  built the way `RetentionService.LivePolls()` is built, so isolation is structural (`research.md`
  R-1, FR-033).
- **Creator routes must not be mounted under `/api/v1/admin`.** That prefix is exempt from
  `MaintenanceMiddleware`; mounting there would let an Ersteller write into a database about to be
  replaced by a restore (`research.md` R-6, FR-050).
- **The `Creator` → content cascade must be configured explicitly.** EF Core's default for an
  optional relationship is `ClientSetNull`, which would hand a deleted Ersteller's polls and wish
  lists to the operator instead of destroying them (`research.md` R-4, FR-020c).
- **The creator surface has exactly one address**, deliberately departing from 007 FR-014a, because
  here the address carries the credential (`research.md` R-9, FR-028d). Do not give it child routes.
- The restore preview must also count Ersteller, or it understates what a restore destroys — the
  same defect 008 recorded for wish lists (FR-052).

Cross-feature consequences still in force from feature 008:

- Feature 007 FR-034 is amended by 008 FR-048b (wish-list titles allowed on the dashboard;
  participant names and poll titles still not). 009 FR-042 extends that narrowly to Ersteller names,
  which are operator-written text on the same grounds.

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
<!-- SPECKIT END -->
