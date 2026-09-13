<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:

- **Active plan**: `specs/008-wishlist/plan.md`
- Specification: `specs/008-wishlist/spec.md`
- Design decisions and rejected alternatives (R-1 … R-14): `specs/008-wishlist/research.md`
- The three new tables and everything derived rather than stored: `specs/008-wishlist/data-model.md`
- UI contract (areas, participant surfaces, states, test ids): `specs/008-wishlist/contracts/ui-contract.md`
- The new endpoints: `specs/008-wishlist/contracts/openapi.yaml`
- Developer guide: `specs/008-wishlist/quickstart.md`

Two cross-feature consequences this plan carries:

- Feature 007 FR-034 is amended by 008 FR-048b (wish-list titles allowed on the dashboard;
  participant names and poll titles still not).
- The restore preview must also count wish lists, or it understates what a restore destroys
  (`research.md` R-10).

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
<!-- SPECKIT END -->
