# Implementation Plan: Individuelle Formulare (Custom Form Builder)

**Branch**: `010-survey-builder` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/010-survey-builder/spec.md`

## Summary

A third survey type. Rundfrage today asks "which day works" (date poll) or "which item do you
want" (wish list); this feature lets the operator ask anything of their own choosing — one field
at a time, typed, on a drag-and-drop canvas — and share it as one link. A participant fills in
every required field or cannot submit; the operator later takes the answers out as CSV or JSON.

Technically this is **four new tables, one migration, two hand-written export formats, and no
owner concept at all**. That last point is the plan's main simplification relative to feature 009:
the spec's clarification session settled that this feature is operator-only, so there is no
`CreatorId`, no `OwnerScope` extension, and no isolation surface to get wrong. What is genuinely
new is the field-type system, the two-sided (client and server) validation it drives, and export.

**Three things in this plan deserve attention before anyone writes code.**

First, **a field's type never changes after creation** (spec clarification, 2026-09-22). The
schema, the admin `PATCH` endpoint and the builder UI all enforce this the same way: there is
simply no code path that writes a new `Type` onto an existing `FormField`. Reinterpreting values
already collected under a different type has no defined meaning, so the simplest correct answer is
that the question never arises.

Second, **"unanswered" is the absence of a row, not a stored value** (research R-13). This is what
lets a required boolean field have an effect without a bespoke tri-state column: every field type,
boolean included, is "unanswered" exactly when `FormFieldValue` has no matching row. The one place
this must be gotten right is the participant control for a boolean field — `v-radio-group`, never
`v-checkbox`, because a checkbox cannot represent "neither yet."

Third, **the two delete-cascades run in opposite directions and both must be exactly right**:
deleting a `FormField` must remove only the values collected *for that field*, across every
response; deleting a `FormResponse` must remove only the values collected *in that response*,
across every field. They share nothing but the `FormFieldValue` table, and a test for one does not
exercise the other (quickstart.md, "easy to get wrong" #3).

**No open risk gates this design.** Unlike feature 009, there is no isolation boundary to prove and
no scale regression to guard against — this feature's documented scale (200 forms, 200,000
responses, FR-047a) is a smaller order of magnitude than the totals 007 and 008 already carry, and
every query here is a straightforward indexed lookup.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); TypeScript 5.9.3 for Vue 3.5.42
**Primary Dependencies**: Vue 3.5.42, Vuetify 4.2.0, Pinia 4.0.3, Vue Router 5.3.1, vue-i18n
11.4.10, Vite 8.2.2; ASP.NET Core on .NET 10 with EF Core 10.0.11
(`Microsoft.EntityFrameworkCore.Sqlite`), Serilog.AspNetCore 10.0.0. **No new dependency on either
side** — no CSV library (research R-5) and no drag-and-drop library (research R-6).
**Storage**: SQLite, single file on a mounted volume. **One migration** (`AddForms`): four new
tables (`Form`, `FormField`, `FormResponse`, `FormFieldValue`), all relationships required (not
nullable) so EF Core's own cascade default already matches what this feature needs (data-model.md
§7) — still configured explicitly, for clarity, not correctness. No data migration: new tables,
not new columns on an existing one.
**Testing**: Vitest 4.1.11 + Vue Test Utils 2.5.0 (frontend unit/component); xUnit 2.9.3 (backend
unit and integration, via the existing `ApiFactory`/`SqliteFixture` pattern); Playwright 1.62
(end-to-end)
**Logging**: Serilog to stdout. No response content, no participant identity, ever logged
(Principle IV) — a form's responses hold nothing more sensitive than a wish list's items, but the
same rule applies uniformly.
**Target Platform**: Linux server backend, modern browsers
**Performance Goals**: SC-009 — at the documented scale of 200 forms and 200,000 responses
installation-wide, with any single form holding up to 50 fields, the forms area and a form's
response list appear within two seconds
**Constraints**: Client and server type-checks never disagree (FR-030, research R-7); a rejected
submission stores nothing (FR-018, SC-003); deleting a field or a response destroys exactly and
only what it should (FR-009, FR-042a); a field's type is immutable post-creation (FR-008); the
public form-fetch route treats a zero-field form exactly like an unknown token (FR-011, FR-021)
**Scale/Scope**: Documented, not enforced: 200 forms and 200,000 responses installation-wide
(FR-047a). Enforced: at most 50 fields per form (FR-011a) — the only new enforced maximum.
Submission rate limit: the existing 10/hour participant policy, reused unchanged (FR-022, research
R-9). Backend: 4 entities, 1 migration, **11 paths carrying 15 operations**
(contracts/openapi.yaml), 1 registered service type (`FormService`; export and CSV writers are
static, mirroring `PollExport`, and are not registered). Frontend: 3 routes, ~6 components,
2 stores, 1 navigation entry

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against `.specify/memory/constitution.md` (v2.0.1). **Pre-Phase-0: pass.
Post-Phase-1: pass.** Complexity Tracking below is empty.

- [x] **I. Zero-Signup Participation**: Opening `/f/{formToken}` requires no account, no sign-in,
      no password — FR-012 states this and the participant surface's only interactive step is the
      form itself. The operator, per the constitution's own carve-out ("creators MAY be asked to
      authenticate"), builds forms behind the existing session; nothing here asks a participant to
      identify themselves (FR-019).
- [x] **II. Test-First Development**: `tasks.md` will order every behaviour test-first;
      quickstart.md names the five suites to write before the code they describe, and the two
      cascade directions (easy-to-break #3) are called out specifically because a single shared
      test would not catch both getting it wrong.
- [x] **III. Simplicity & YAGNI**: No new project, service boundary, or dependency on either side
      (research R-5, R-6). No owner column, no `OwnerScope` extension — the one column feature 009
      would suggest "for consistency" is exactly the anticipatory abstraction this principle
      rejects, since this feature has no second caller for it (research R-3). Client/server
      validation is duplicated rather than bridged by new shared-code infrastructure, because a
      shared test fixture closes the actual risk (disagreement) without adding a build step
      (research R-7).
- [x] **IV. Data Minimization & Operator-Controlled Storage**: A `FormResponse` holds a submission
      timestamp and per-field values — no IP address, no user agent, no session, no name
      (data-model.md §3, FR-019). Deletion (of a field's values, of one response, or of a whole
      form) is a real `DELETE`, not a hide flag (FR-009, FR-042a, FR-042). Retention is explicit:
      no automatic expiry, deletion is the only way out (FR-043), the same choice feature 008 made
      for wish lists. No third-party asset, script or dependency is introduced.
- [x] **Technology Constraints**: Vue 3 + Vuetify + Pinia + Vite, ASP.NET Core on .NET 10, SQLite
      on a mounted volume, Serilog, Vitest/xUnit/Playwright. Nothing outside the pinned list.

## Project Structure

### Documentation (this feature)

```text
specs/010-survey-builder/
├── plan.md              # This file
├── research.md          # Phase 0 — R-1 … R-14 (+ the label-collision note under R-14)
├── data-model.md         # Phase 1 — the four tables, the validation rule table, the migration
├── quickstart.md        # Phase 1 — developer guide, and the five rules that are easy to break
├── contracts/
│   ├── openapi.yaml     # Phase 1 — the new endpoints
│   └── ui-contract.md   # Phase 1 — areas, routes, states, test ids
├── checklists/
│   └── requirements.md  # from /speckit-specify
└── tasks.md             # Phase 2 output — NOT created by /speckit-plan
```

### Source code (repository root)

New files are marked **new**; everything else is an existing file this feature edits — and in most
cases does not need to, unlike feature 009, since there is no owner column to thread through
`Poll`/`WishList` and no admin surface beyond this feature's own.

```text
backend/src/Rundfrage.Api/
├── Data/
│   ├── Entities/
│   │   ├── Form.cs                          # new
│   │   ├── FormField.cs                     # new
│   │   ├── FormResponse.cs                  # new
│   │   └── FormFieldValue.cs                # new
│   ├── Migrations/…_AddForms.cs             # new — four tables, no data migration
│   ├── RundfrageDbContext.cs                # + four DbSets, indexes, explicit cascades (§7)
│   └── RestoreService.cs                    # + form/response counts in the preview (FR-047)
├── Forms/                                   # new folder — a sibling of Polls/, Wishes/, Creators/
│   ├── FormService.cs                       # new — create, rename, delete; field CRUD + reorder
│   ├── FormFieldValidation.cs               # new — the seven type rules (research R-7)
│   ├── FormCsvExport.cs                     # new — RFC 4180 writer, no library (research R-5)
│   └── FormExport.cs                        # new — JSON export, mirrors Polls/PollExport.cs
├── Endpoints/
│   ├── Admin/FormAdminEndpoints.cs          # new — /admin/forms/**
│   └── Public/FormEndpoints.cs              # new — /f/{formToken}/**
└── Program.cs                               # + DI, + the /f group beside /u and /w

frontend/src/
├── components/
│   ├── admin/AdminNav.vue                   # + the "Formulare" entry
│   ├── admin/FormsView.vue                  # new — the forms area
│   ├── admin/FormBuilderView.vue            # new — canvas, fields, responses, export
│   └── form/FormFillView.vue                # new — the participant surface
├── composables/useFormFieldValidation.ts    # new — the TypeScript half of research R-7
├── stores/forms.ts                          # new — the operator's forms and fields
├── stores/formFill.ts                       # new — one participant's in-progress answers
├── api/client.ts                            # + the form calls, + two export URL builders
├── router.ts                                # + /admin/formulare(/:formId), + /f/:formToken
└── locales/de.json                          # + form.*, + nav.forms

backend/tests/
├── Rundfrage.Api.IntegrationTests/
│   ├── FormAdminTests.cs                    # new — create, rename, delete, list
│   ├── FormFieldTests.cs                    # new — add, edit, remove, reorder, the 50 cap
│   ├── FormFieldCascadeTests.cs             # new — the two delete directions (easy-to-break #3)
│   ├── FormSubmissionTests.cs               # new — every validation case from the shared fixture
│   ├── FormExportTests.cs                   # new — CSV and JSON, order, typing, duplicate labels
│   ├── FormResponseDeletionTests.cs         # new — one response, nothing else affected
│   ├── FormMaintenanceTests.cs              # new — /f is gated, /admin/forms is not
│   ├── FormRateLimitTests.cs                # new — the existing submission policy, unchanged
│   ├── FormZeroFieldTests.cs                # new — public route treats it as unknown (FR-011)
│   ├── AdminAuthorizationTests.cs           # + the /admin/forms routes
│   └── RestoreTests.cs                      # + form/response counts, + a pre-010 backup file
└── Rundfrage.Api.UnitTests/
    └── FormFieldValidationTests.cs          # new — the shared fixture, no DB

frontend/tests/unit/
├── FormsView.spec.ts                        # new
├── FormBuilderView.spec.ts                  # new — including drag and the button alternative
├── FormFillView.spec.ts                     # new — including the shared-fixture validation cases
├── AdminNav.spec.ts                         # + the "Formulare" entry, settings still last
└── adminRoutes.spec.ts                      # + /admin/formulare

frontend/tests/fixtures/
└── form-field-validation.json                # new — consumed by both sides (research R-7)

e2e/tests/
└── form-journey.spec.ts                     # new — the quickstart.md walk, as a Playwright spec
```

**Structure Decision**: the existing web-application layout is kept exactly as it is. The backend
gains one folder, `Forms/`, as a **sibling** of `Polls/`, `Wishes/` and `Creators/` — the same
reasoning `Program.cs` already records for feature 008 and repeats for feature 009: features share
mechanisms (the token, the write transaction, the shell) and no behaviour. Ownership is a
mechanism `Forms/` deliberately does **not** pick up, so unlike feature 009 this sibling touches
neither `Poll.cs` nor `WishList.cs` nor `OwnerScope.cs` at all.

The frontend gains one folder, `components/form/`, beside `poll/`, `wish/` and `creator/`, holding
only the participant-facing `FormFillView.vue`; the two operator-facing views join `components/admin/`
directly, the same place `FormsView`'s siblings (`PollList.vue`, `WishListsView.vue`,
`CreatorsView.vue`) already live.

## Phase 2 approach (what `/speckit-tasks` will expand)

Ordered so that each stage is independently testable and the riskiest thing — the two cascade
directions — is proved early rather than assumed from the schema.

1. **Schema** — `Form`, `FormField`, `FormResponse`, `FormFieldValue`, the migration, and
   `FormFieldCascadeTests` proving both delete directions against real data before any endpoint
   exists.
2. **Validation rule table** — `FormFieldValidation.cs`, the TypeScript composable, and the shared
   fixture they both consume; `FormFieldValidationTests` first, since every later test that submits
   a value depends on this being right.
3. **Operator: building a form** — create, rename, delete; field add/edit/remove/reorder; the
   50-field cap tested before it is written.
4. **The participant surface, backend** — the `/f` group, the zero-field-form refusal, submission
   validation end to end, the existing rate-limit policy applied.
5. **Export** — CSV first (the hand-written writer, quoting edge cases), then JSON (mirroring
   `PollExport`'s shape), then the duplicate-label disambiguation rule (research R-14 addendum).
6. **Frontend: the builder** — `FormsView`, `FormBuilderView`, drag-and-drop plus the button
   alternative (FR-049), the field cards.
7. **Frontend: the participant surface** — `FormFillView`, the seven per-type controls, the shared
   client-side validation, the boolean field's radio-group (never a checkbox).
8. **Restore preview and backup** — the form/response counts (FR-047), and a test against a
   pre-010 backup file to prove the tolerant read (research R-11).
9. **End-to-end** — the quickstart.md walk as a Playwright spec.

Stages 1–3 deliver User Story 1 and half of User Story 4; stage 4 completes User Story 2; stage 5
delivers User Story 3; the remainder of stage 3 (rename, delete) plus stage 8 complete User Story
4.

## Complexity Tracking

> Fill ONLY if Constitution Check has violations that must be justified.

**No violations.** No new project, service, layer or dependency is introduced. This feature is, if
anything, simpler than the one before it: feature 009 added a table, two nullable columns, and an
access-filter abstraction; this feature adds four tables and no abstraction beyond the ordinary
service/endpoint split every existing module already uses.

One decision that *looks* like added complexity and is recorded here so a reviewer does not have
to rediscover the reasoning:

| Looks like | Why it is not | Where it is argued |
|---|---|---|
| Duplicated validation logic (TypeScript and C#, not shared) | The two are kept from disagreeing by a shared test fixture, not by a shared runtime — a WASM or generated-parser bridge for seven regex-shaped rules would be the actual unjustified complexity | research R-7 |
