# Tasks: Individuelle Formulare (Custom Form Builder)

**Input**: Design documents from `/specs/010-survey-builder/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Mandatory (Constitution Principle II: Test-First Development). Every behaviour task is
preceded by the test task that defines it, and that test must be observed failing before the
implementation task begins.

**Organization**: Tasks are grouped by user story (spec.md) so each can be implemented and
tested independently. User Stories 1 and 2 are both P1 — either can be built first, but neither
form-building nor form-answering alone is a usable feature, so the two together are this
feature's MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no ordering dependency)
- **[Story]**: US1 / US2 / US3 / US4, mapping to spec.md's four user stories
- Every task names an exact file path

## Path Conventions

Web application, per plan.md's Project Structure: `backend/src/Rundfrage.Api/`,
`backend/tests/Rundfrage.Api.{Unit,Integration}Tests/`, `frontend/src/`, `frontend/tests/unit/`,
`e2e/tests/`.

---

## Phase 1: Setup

**Purpose**: Confirm the feature needs no new tooling before any code is written.

- [X] T001 Confirm no new dependency is required on either side (research R-5, R-6): review
      `backend/src/Rundfrage.Api/Rundfrage.Api.csproj` and `frontend/package.json` and record that
      neither needs a change for this feature — CSV export and drag-and-drop are both hand-rolled.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The schema, the DI wiring and the route-group skeletons every user story needs.
Nothing in Phase 3 onward can start until this phase is done.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 [P] Create the `FieldType` enum (`Text`, `Integer`, `Decimal`, `Boolean`, `Email`,
      `Phone`, `PostalCode`) in `backend/src/Rundfrage.Api/Data/Entities/FieldType.cs`
      (data-model.md §2, research R-2)
- [X] T003 [P] Create the `Form` entity in `backend/src/Rundfrage.Api/Data/Entities/Form.cs`
      (`Id`, `Title`, `FormToken`, `CreatedAtUtc` — data-model.md §1)
- [X] T004 [P] Create the `FormField` entity in
      `backend/src/Rundfrage.Api/Data/Entities/FormField.cs` (`Id`, `FormId`, `Type`, `Label`,
      `Required`, `MaxLength`, `MinLength`, `DisplayOrder` — data-model.md §2)
- [X] T005 [P] Create the `FormResponse` entity in
      `backend/src/Rundfrage.Api/Data/Entities/FormResponse.cs` (`Id`, `FormId`,
      `SubmittedAtUtc` — data-model.md §3)
- [X] T006 [P] Create the `FormFieldValue` entity in
      `backend/src/Rundfrage.Api/Data/Entities/FormFieldValue.cs` (`Id`, `ResponseId`, `FieldId`,
      `Value` — data-model.md §4)
- [X] T007 Configure `RundfrageDbContext` in `backend/src/Rundfrage.Api/Data/RundfrageDbContext.cs`:
      add the four `DbSet<T>` properties; in `OnModelCreating`, configure all four explicit
      cascades (`Form`→`FormField`, `Form`→`FormResponse`, `FormField`→`FormFieldValue`,
      `FormResponse`→`FormFieldValue`), the unique index on `Form.FormToken`, the
      `(FormId, DisplayOrder)` index on `FormField`, and the unique `(ResponseId, FieldId)` index
      on `FormFieldValue` (data-model.md §7). Depends on T002–T006.
- [X] T008 Generate and apply the `AddForms` EF Core migration in
      `backend/src/Rundfrage.Api/Data/Migrations/` (no data migration needed — new tables only).
      Depends on T007.
- [X] T009 [P] Create the `FormService` skeleton (constructor injecting `RundfrageDbContext`, no
      methods yet) in `backend/src/Rundfrage.Api/Forms/FormService.cs`. Depends on T007.
- [X] T010 [P] Register `FormService` in DI, and map the empty `admin.MapGroup("/forms")` and
      `api.MapGroup("/f")` route groups (no handlers yet) in `backend/src/Rundfrage.Api/Program.cs`
      (research R-8 — `/admin/forms` inherits the admin group's session requirement and
      maintenance-mode exemption; `/f` sits beside `/u` and `/w` so `MaintenanceMiddleware` gates
      it). Depends on T009.
- [X] T011 [P] Create the `forms.ts` Pinia store skeleton (state refs only: `forms`, `current`,
      loading/error state) in `frontend/src/stores/forms.ts`
- [X] T012 [P] Create the `formFill.ts` Pinia store skeleton in `frontend/src/stores/formFill.ts`
- [X] T013 [P] Add the `/admin/formulare`, `/admin/formulare/:formId` (in `AdminShell`) and
      `/f/:formToken` (in `BareShell`) route definitions, each lazy-loaded against a placeholder
      component, in `frontend/src/router.ts` (contracts/ui-contract.md §2)
- [X] T014 [P] Add the "Formulare" navigation entry — 5th position, after "Ersteller", before
      "Einstellungen" — in `frontend/src/components/admin/AdminNav.vue` (contracts/ui-contract.md §1)

**Checkpoint**: Schema, routing skeleton and stores exist. User story work can begin.

---

## Phase 3: User Story 1 - Build a form by dragging fields into place (Priority: P1) 🎯 MVP (half)

**Goal**: The operator can create a form, add/edit/remove/reorder typed fields on a drag-and-drop
canvas, and get its participant link once at least one field exists.

**Independent Test**: Build one form with at least four fields covering at least three different
types using only drag-and-drop and the add/remove controls, reorder two of them, and confirm the
published field order matches the order left in the builder.

### Tests for User Story 1 ⚠️

> Write these first; confirm each fails before starting the matching implementation task.

- [X] T015 [P] [US1] Unit tests for form/field structural validation — title required/≤200 chars,
      label required/≤200 chars, `maxLength` required and bounded 1..5000 for Text fields,
      `minLength ≤ maxLength`, the 50-field cap — in
      `backend/tests/Rundfrage.Api.UnitTests/FormValidationTests.cs`
- [X] T016 [P] [US1] Integration tests for `POST /admin/forms`, `GET /admin/forms/{formId}` and
      `GET /admin/forms` (the list — asserting `fieldCount`, `responseCount` and `formToken` per
      row, FR-040) in `backend/tests/Rundfrage.Api.IntegrationTests/FormAdminTests.cs`
- [X] T017 [P] [US1] Integration tests for field add/edit/remove/reorder, including the 50-field
      cap, its concurrent-add case, editing a field's label/required/length limits on a form that
      already has responses (FR-008), and a `PATCH .../fields/{fieldId}` request that includes a
      `type` value being ignored rather than applied (quickstart.md "easy to break" #1), in
      `backend/tests/Rundfrage.Api.IntegrationTests/FormFieldTests.cs`
- [X] T018 [P] [US1] Component tests for `FormBuilderView.vue` covering add/edit/remove field,
      drag reorder, the move-up/move-down button alternative, and the two link states — "kein
      Feld — Link nicht erreichbar" (`form-builder-no-link`) while the field list is empty, and
      the copyable link (`form-builder-link`) once the first field is added (FR-011) — in
      `frontend/tests/unit/FormBuilderView.spec.ts`
- [X] T019 [P] [US1] Component tests for `FormsView.vue` covering the creation dialog and opening
      a form into the builder, in `frontend/tests/unit/FormsView.spec.ts`
- [X] T020 [P] [US1] Component tests for the "Formulare" nav entry and its two admin routes, in
      `frontend/tests/unit/AdminNav.spec.ts` and `frontend/tests/unit/adminRoutes.spec.ts`

### Implementation for User Story 1

- [X] T021 [US1] Implement `FormService.CreateAsync` and the static `Validate*` methods for title,
      label and length limits in `backend/src/Rundfrage.Api/Forms/FormService.cs`. Depends on T009;
      make T015 pass.
- [X] T022 [US1] Implement `POST /admin/forms` and `GET /admin/forms/{formId}` in
      `backend/src/Rundfrage.Api/Endpoints/Admin/FormAdminEndpoints.cs`. Depends on T021, T010;
      make T016 pass.
- [X] T023 [US1] Implement `FormService` field methods — `AddFieldAsync` (incl. the 50-field cap),
      `UpdateFieldAsync` (label/required/lengths; never type), `RemoveFieldAsync` (cascades to
      `FormFieldValue`), `ReorderFieldsAsync` (dense renumber, research R-4) — in
      `backend/src/Rundfrage.Api/Forms/FormService.cs`. Depends on T021.
- [X] T024 [US1] Implement `POST .../fields`, `PATCH .../fields/{fieldId}`,
      `DELETE .../fields/{fieldId}` and `PUT .../fields/order` in
      `backend/src/Rundfrage.Api/Endpoints/Admin/FormAdminEndpoints.cs`. Depends on T023, T022;
      make T017 pass.
- [X] T025 [US1] Implement `GET /admin/forms` — list with `fieldCount`, `responseCount`,
      `formToken` per form — in `backend/src/Rundfrage.Api/Endpoints/Admin/FormAdminEndpoints.cs`.
      Depends on T022; make T016 pass.
- [X] T026 [P] [US1] Implement `FormsView.vue`: form list, "Formular erstellen" dialog (title
      only), opening a row into the builder, in `frontend/src/components/admin/FormsView.vue`.
      Depends on T011; make T019 pass.
- [X] T027 [P] [US1] Implement `FormBuilderView.vue`: title header, field canvas, add-field control
      offering all seven types, field cards (label input, required toggle, length inputs for
      Text), remove-field with confirmation, drag reorder plus move-up/move-down buttons, and the
      "kein Feld — Link nicht erreichbar" / published-link states, in
      `frontend/src/components/admin/FormBuilderView.vue`. Depends on T011; make T018 pass.
- [X] T028 [P] [US1] Wire `forms.ts` store actions: load list, create, load detail, add/update/
      remove/reorder field, in `frontend/src/stores/forms.ts`. Depends on T011.
- [X] T029 [P] [US1] Add `form.*` and `nav.forms` strings for everything in this story to
      `frontend/src/locales/de.json` (contracts/ui-contract.md §6)

**Checkpoint**: A form can be built end to end and its link produced. Not yet answerable — that's
User Story 2.

---

## Phase 4: User Story 2 - A participant answers and cannot submit something incomplete (Priority: P1) 🎯 MVP (half)

**Goal**: A participant opens a form link with no account, is blocked from submitting while any
required field is empty or malformed (both client- and server-side), and a valid submission is
recorded with no participant identity attached.

**Independent Test**: Open a published form with a mix of required and optional fields with no
account; attempt to submit with a required field empty and with a required field holding a value
of the wrong shape; confirm both are blocked with the offending field identified; then submit a
valid response and confirm it is recorded.

### Tests for User Story 2 ⚠️

- [X] T030 [P] [US2] Create the shared validation fixture
      `frontend/tests/fixtures/form-field-validation.json` — valid and invalid cases for all seven
      field types (data-model.md §6, research R-7)
- [X] T031 [P] [US2] Unit tests for `FormFieldValidation`, consuming the shared fixture, in
      `backend/tests/Rundfrage.Api.UnitTests/FormFieldValidationTests.cs`. Depends on T030.
- [X] T032 [P] [US2] Integration tests for `POST /f/{formToken}/responses` covering every fixture
      case and asserting nothing is stored on refusal, in
      `backend/tests/Rundfrage.Api.IntegrationTests/FormSubmissionTests.cs`. Depends on T030.
- [X] T033 [P] [US2] Integration tests proving `GET /f/{formToken}` produces the identical
      `NeutralNotFound` response for a zero-field form, a malformed token, an unknown token and a
      deleted form's former token — all four indistinguishable from one another (FR-021) — in
      `backend/tests/Rundfrage.Api.IntegrationTests/FormZeroFieldTests.cs`
- [X] T034 [P] [US2] Integration tests confirming `/f/**` is refused under maintenance mode while
      `/admin/forms/**` is unaffected, in
      `backend/tests/Rundfrage.Api.IntegrationTests/FormMaintenanceTests.cs`
- [X] T035 [P] [US2] Integration test confirming form submissions share the existing participant
      rate-limit policy (research R-9) rather than a new one, in
      `backend/tests/Rundfrage.Api.IntegrationTests/FormRateLimitTests.cs`
- [X] T036 [P] [US2] Component tests for `FormFillView.vue`, consuming the shared fixture,
      including the boolean field's no-default radio group, in
      `frontend/tests/unit/FormFillView.spec.ts`. Depends on T030.

### Implementation for User Story 2

- [X] T037 [P] [US2] Implement `FormFieldValidation.cs` — the seven type rules (data-model.md §6)
      — in `backend/src/Rundfrage.Api/Forms/FormFieldValidation.cs`. Make T031 pass.
- [X] T038 [US2] Implement `GET /f/{formToken}` (public definition fetch; a zero-field form
      produces the same response as an unknown token) in
      `backend/src/Rundfrage.Api/Endpoints/Public/FormEndpoints.cs`. Depends on T010; make T033
      pass.
- [X] T039 [US2] Implement `POST /f/{formToken}/responses` — validate every value via
      `FormFieldValidation`, store one `FormResponse` and its `FormFieldValue` rows in one
      transaction, refuse (storing nothing) and name every field at fault otherwise — in
      `backend/src/Rundfrage.Api/Endpoints/Public/FormEndpoints.cs`. Depends on T037; make T032
      pass.
- [X] T040 [US2] Apply `RateLimiting.SubmissionPolicy` to the submission route in
      `backend/src/Rundfrage.Api/Endpoints/Public/FormEndpoints.cs`. Depends on T039; make T035
      pass.
- [X] T041 [P] [US2] Implement `useFormFieldValidation.ts` — the TypeScript half of the seven
      rules — in `frontend/src/composables/useFormFieldValidation.ts`. Depends on T030; make T036
      pass.
- [X] T042 [US2] Implement `FormFillView.vue`: one control per field type (a `v-radio-group` with
      nothing pre-selected for Boolean — never a `v-checkbox`), text-based required markers,
      inline per-field errors, client-side submit gating, a terminal confirmation state, and
      unavailable/maintenance notices, in `frontend/src/components/form/FormFillView.vue`. Depends
      on T041.
- [X] T043 [P] [US2] Wire `formFill.ts` store: load definition, hold in-progress answers, submit,
      surface field errors, in `frontend/src/stores/formFill.ts`. Depends on T012.
- [X] T044 [P] [US2] Add `form.fill.*` and the new `error.*` catalogue entries to
      `frontend/src/locales/de.json`

**Checkpoint**: User Stories 1 and 2 together are the feature's MVP — a form can be built and
answered, correctly, end to end.

---

## Phase 5: User Story 3 - Take the answers out as CSV or JSON (Priority: P2)

**Goal**: The operator downloads a form's responses as CSV or JSON, correctly ordered and typed,
even with zero responses or after the fields have been reordered.

**Independent Test**: Collect several responses on one form covering every field type, download
both formats, and confirm each file names every field and reproduces every response's values with
the correct type and in field order.

### Tests for User Story 3 ⚠️

- [X] T045 [P] [US3] Unit tests for the CSV writer's RFC 4180 quoting (comma, quote, newline, and
      plain values) in `backend/tests/Rundfrage.Api.UnitTests/FormCsvExportTests.cs`
- [X] T046 [P] [US3] Integration tests for both export formats: header/property order, type
      fidelity (numbers as numbers, booleans as booleans in JSON / `TRUE`/`FALSE` in CSV), a
      zero-response empty file, export order after a field reorder, duplicate-label
      disambiguation, and — a response submitted before a new field was added exports that
      field's value as absent (empty CSV cell / omitted or `null` JSON property), never a guessed
      default (FR-038) — in `backend/tests/Rundfrage.Api.IntegrationTests/FormExportTests.cs`
- [X] T047 [P] [US3] Component tests for the responses panel and the two export buttons, extending
      `frontend/tests/unit/FormBuilderView.spec.ts`

### Implementation for User Story 3

- [X] T048 [P] [US3] Implement `FormCsvExport.cs` (the RFC 4180 writer and the label-disambiguation
      rule) in `backend/src/Rundfrage.Api/Forms/FormCsvExport.cs`. Make T045 pass.
- [X] T049 [P] [US3] Implement `FormExport.cs` — a versioned JSON document mirroring
      `Polls/PollExport.cs`'s shape, sharing the label-disambiguation rule — in
      `backend/src/Rundfrage.Api/Forms/FormExport.cs`
- [X] T050 [US3] Implement `GET /admin/forms/{formId}/responses` in
      `backend/src/Rundfrage.Api/Endpoints/Admin/FormAdminEndpoints.cs`. Depends on T022.
- [X] T051 [US3] Implement `GET /admin/forms/{formId}/export/csv` and
      `GET /admin/forms/{formId}/export/json` in
      `backend/src/Rundfrage.Api/Endpoints/Admin/FormAdminEndpoints.cs`. Depends on T048, T049,
      T050; make T046 pass.
- [X] T052 [P] [US3] Add `formExportCsvUrl(formId)`/`formExportJsonUrl(formId)` URL builders
      (navigated to directly, not fetched — matching `exportUrl`/`backupUrl`) to
      `frontend/src/api/client.ts`. Depends on T051.
- [X] T053 [US3] Add the responses panel and the two export buttons to `FormBuilderView.vue` in
      `frontend/src/components/admin/FormBuilderView.vue`. Depends on T052; make T047 pass.
- [X] T054 [P] [US3] Wire `forms.ts` store: load a form's responses, in
      `frontend/src/stores/forms.ts`. Depends on T050.

**Checkpoint**: Responses collected in User Story 2 can be taken out correctly in either format.

---

## Phase 6: User Story 4 - Manage the set of forms (Priority: P3)

**Goal**: The operator sees every form with its counts, renames one, edits fields on a form that
already has responses, deletes a single response, and deletes a whole form — each destroying
exactly and only what it should.

**Independent Test**: Build two forms, collect a response on each, edit one form's fields, delete
the other, and confirm the deleted form's link and responses are gone while the edited form's
existing responses and new field set are both correct.

### Tests for User Story 4 ⚠️

- [X] T055 [P] [US4] Integration tests for renaming and deleting a whole form — cascading to its
      fields, responses and values, its participant link 404ing afterward — extending
      `backend/tests/Rundfrage.Api.IntegrationTests/FormAdminTests.cs`
- [X] T056 [P] [US4] Integration tests proving the two delete-cascade directions against a form
      that already has multiple responses: removing one field leaves every response's other values
      intact; deleting one response leaves every other response and every field untouched, in
      `backend/tests/Rundfrage.Api.IntegrationTests/FormFieldCascadeTests.cs`
- [X] T057 [P] [US4] Integration tests for deleting a single response, in
      `backend/tests/Rundfrage.Api.IntegrationTests/FormResponseDeletionTests.cs`
- [X] T058 [P] [US4] Integration tests distinguishing "no forms exist" from "the stored data cannot
      be read", extending `backend/tests/Rundfrage.Api.IntegrationTests/FormAdminTests.cs`
- [X] T059 [P] [US4] Integration tests for the restore preview's new form/response counts, including
      a tolerant read against a pre-010 backup file that lacks the `Forms` table entirely, **and**
      a full backup→restore round trip proving a form with fields and responses is byte-for-byte
      the same afterward and its participant link still answers (FR-046) — not only that the
      preview counts it correctly — extending
      `backend/tests/Rundfrage.Api.IntegrationTests/RestoreTests.cs`
- [X] T060 [P] [US4] Component tests for rename, delete-with-response-count-confirmation, and the
      empty state, extending `frontend/tests/unit/FormsView.spec.ts`
- [X] T061 [P] [US4] Component tests for the per-response delete action and its confirmation,
      extending `frontend/tests/unit/FormBuilderView.spec.ts`

### Implementation for User Story 4

- [X] T062 [US4] Implement `FormService.RenameAsync` and `DeleteAsync` (cascades to fields,
      responses and values) in `backend/src/Rundfrage.Api/Forms/FormService.cs`. Depends on T021;
      make T055 pass.
- [X] T063 [US4] Implement `PATCH /admin/forms/{formId}` and `DELETE /admin/forms/{formId}` in
      `backend/src/Rundfrage.Api/Endpoints/Admin/FormAdminEndpoints.cs`. Depends on T062.
- [X] T064 [US4] Implement `FormService.DeleteResponseAsync` in
      `backend/src/Rundfrage.Api/Forms/FormService.cs`. Depends on T021; make T057 pass.
- [X] T065 [US4] Implement `DELETE /admin/forms/{formId}/responses/{responseId}` in
      `backend/src/Rundfrage.Api/Endpoints/Admin/FormAdminEndpoints.cs`. Depends on T064.
- [X] T066 [US4] Verify and, if needed, correct the field- and form-deletion queries in
      `backend/src/Rundfrage.Api/Forms/FormService.cs` until both directions in T056 pass — the
      "easy to break" checkpoint quickstart.md calls out by name (#3).
- [X] T067 [P] [US4] Implement `ReadFormCountsAsync` (the tolerant `sqlite_master`-check pattern
      `ReadCreatorCountAsync` already uses) and wire `FormsInBackup`/`ResponsesInBackup`/
      `FormsLost`/`ResponsesLost` into `RestorePreview`/`Counts` in
      `backend/src/Rundfrage.Api/Data/RestoreService.cs` (research R-11). Make T059 pass.
- [X] T068 [US4] Add rename, delete (reusing `DeleteConfirm.vue`, stating the response count) and
      the empty state to `FormsView.vue` in `frontend/src/components/admin/FormsView.vue`. Depends
      on T063; make T060 pass.
- [X] T069 [US4] Add per-response delete (with confirmation) to `FormBuilderView.vue` in
      `frontend/src/components/admin/FormBuilderView.vue`. Depends on T065; make T061 pass.
- [X] T070 [P] [US4] Wire `forms.ts` store: rename, delete form, delete response, in
      `frontend/src/stores/forms.ts`. Depends on T063, T065.

**Checkpoint**: All four user stories are independently functional. The full set of forms is
manageable end to end.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T071 [P] Extend `AdminAuthorizationTests.cs` with every `/admin/forms/**` route requiring an
      operator session, in `backend/tests/Rundfrage.Api.IntegrationTests/AdminAuthorizationTests.cs`
- [X] T072 [P] Write `e2e/tests/form-journey.spec.ts` — the quickstart.md walk as a Playwright spec
      (create, add/reorder fields, answer, export both formats, delete a response, delete the
      form). Depends on Phases 3–6 being complete.
- [X] T073 Raise `SUBMISSION_LIMIT_PER_HOUR` in the end-to-end environment configuration for the
      form-journey run, matching the existing poll and wish-list journeys (research R-9 — no new
      environment variable is introduced by this feature)
- [X] T074 Walk quickstart.md by hand once, end to end, and fix anything it reveals
- [X] T075 [P] `FormScaleTests` — seed the documented scale (200 forms, 200,000 responses
      installation-wide, one form at the 50-field ceiling, research.md/plan.md SC-009) and assert
      the forms area and a form's response list each load within two seconds, mirroring feature
      009's `CreatorScaleTests` precedent, in
      `backend/tests/Rundfrage.Api.IntegrationTests/FormScaleTests.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies.
- **Foundational (Phase 2)**: Depends on Setup. Blocks every user story.
- **User Story 1 (Phase 3)**: Depends on Foundational only.
- **User Story 2 (Phase 4)**: Depends on Foundational only — not on User Story 1's implementation
  tasks, though in practice a form built in US1 is what US2's tests answer. The two stories'
  backend routes (`/admin/forms/**` vs `/f/**`) and frontend surfaces (`FormBuilderView.vue` vs
  `FormFillView.vue`) touch disjoint files, so they can proceed in parallel.
- **User Story 3 (Phase 5)**: Depends on Foundational, and on User Story 1's `GET /admin/forms/{formId}`
  existing (T022) to attach the export routes beside it — but not on User Story 2.
- **User Story 4 (Phase 6)**: Depends on Foundational, and on User Story 1's `FormService`/
  `FormAdminEndpoints.cs` skeleton existing (T021, T022) to extend — but is independently testable
  once T017's field-removal cascade (US1) and User Story 3's response-listing (T050) exist,
  since T056's cascade tests exercise both.
- **Polish (Phase 7)**: Depends on all four user stories.

### Within Each User Story

- Tests are written and observed failing before their matching implementation task (Constitution
  Principle II).
- Entities before services; services before endpoints; endpoints before the frontend that calls
  them; store wiring can proceed alongside the view that uses it since both depend only on the
  endpoint contract, not on each other.

### Parallel Opportunities

- All Foundational entity-creation tasks (T002–T006) run in parallel — four different files.
- Once Foundational is complete, **User Story 1 and User Story 2 can be staffed and built in
  parallel** — they are both P1, and they touch no common file (the builder vs. the fill-in
  surface, `/admin/forms` vs. `/f`).
- Within US1: all six test tasks (T015–T020) in parallel; T026–T029 (frontend view, store,
  strings) in parallel once their shared dependency (T011) is done.
- Within US2: T030's fixture is a single dependency five other tasks (T031–T033, T036, T041) fan
  out from in parallel once it exists.
- Within US3: T048 and T049 (CSV and JSON writers) are independent files and run in parallel.
- Within US4: all five backend test tasks (T055–T059) in parallel; T067 (restore) in parallel with
  the frontend tasks T068–T070.

---

## Parallel Example: User Story 2 (after T030 lands)

```bash
Task: "Unit tests for FormFieldValidation in backend/tests/Rundfrage.Api.UnitTests/FormFieldValidationTests.cs"
Task: "Integration tests for POST /f/{formToken}/responses in backend/tests/Rundfrage.Api.IntegrationTests/FormSubmissionTests.cs"
Task: "Integration tests for zero-field GET /f/{formToken} in backend/tests/Rundfrage.Api.IntegrationTests/FormZeroFieldTests.cs"
Task: "Component tests for FormFillView.vue in frontend/tests/unit/FormFillView.spec.ts"
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2)

1. Complete Phase 1: Setup.
2. Complete Phase 2: Foundational (blocks everything).
3. Complete Phase 3 (US1) and Phase 4 (US2) — in parallel if staffed, otherwise US1 then US2.
4. **STOP and VALIDATE**: build a form, answer it from a second browser with no account, confirm
   required-field gating both client- and server-side. This is the MVP — a form can be built and
   correctly answered — even though nothing can yet be exported or renamed.
5. Deploy/demo if ready.

### Incremental Delivery

1. Setup + Foundational → foundation ready.
2. US1 + US2 → MVP: build a form, answer it correctly. Deploy/demo.
3. US3 → responses can be exported. Deploy/demo.
4. US4 → the set of forms is fully manageable (rename, delete, per-response delete, restore
   preview). Deploy/demo.
5. Polish → authorization sweep, end-to-end coverage, quickstart validation.

### Parallel Team Strategy

With multiple developers, once Foundational is done:

- Developer A: User Story 1 (the builder)
- Developer B: User Story 2 (the participant surface and shared validation fixture)
- Developer C: starts User Story 3 once T022 (US1) lands, or User Story 4 once T021/T022 and T050
  (US3) land

---

## Notes

- [P] tasks touch different files with no ordering dependency on each other.
- [Story] labels map every phase-3-onward task to spec.md's four user stories for traceability.
- The two delete-cascade directions (T056/T066) are this feature's single highest-risk point —
  see quickstart.md's "easy to break" #3. Do not consider User Story 4 done until T056 passes
  against a form that has real, multi-response data, not an empty fixture.
- Commit after each task or logical group; stop at any checkpoint to validate a story
  independently before continuing.
