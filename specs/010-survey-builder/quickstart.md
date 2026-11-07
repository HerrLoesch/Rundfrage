# Quickstart: Individuelle Formulare

**Feature**: 010-survey-builder | **Date**: 2026-09-22

For a developer picking this feature up. Read [research.md](./research.md) R-1, R-3 and R-13
first; they are the three decisions the rest of the code assumes.

## Run it

```bash
export SPECIFY_FEATURE=010-survey-builder     # every Spec Kit command needs this (constitution)

# Backend
dotnet run --project backend/src/Rundfrage.Api            # http://localhost:5000
# Frontend
cd frontend && npm run dev                                 # proxies /api to the backend
```

The operator account comes from `ADMIN_USER` and `ADMIN_PASSWORD_HASH`; there is no default and
the application refuses to start without them.

## Walk the feature by hand

1. Sign in, open **Formulare** in the navigation (fifth entry, before Einstellungen).
2. Create one titled "Anmeldung Sommerfest". The builder opens on an empty canvas; no link is
   offered yet (FR-011).
3. Add a Text field "Name" (max length 100), mark it required. Add an Email field "E-Mail",
   required. Add a Boolean field "Kommst du?", required. Add a Whole-number field "Personen",
   optional.
4. Drag "E-Mail" above "Name". The canvas reorders immediately.
5. Copy the link that now appears — it looks like `http://localhost:5173/f/<22 chars>`.
6. Open it in a **private window**. No sign-in. Try submitting with the boolean field untouched:
   blocked, field named. Answer everything required, submit: confirmation shown.
7. Back in the builder, the response count is 1. Open the responses panel and delete that one
   response — the form and its fields are unaffected.
8. Add a second, real response, then download both **CSV** and **JSON** — same field order in
   both, same values, typed correctly in the JSON.
9. Delete the "Personen" field. Its already-collected values are gone from any future export;
   nothing else about the two responses changes.
10. Delete the form entirely. The confirmation states the response count first. Afterward its link
    404s exactly like an unknown one.

## Where things live

| Concern | Path |
|---|---|
| The four new tables | `backend/src/Rundfrage.Api/Data/Entities/Form.cs`, `FormField.cs`, `FormResponse.cs`, `FormFieldValue.cs` |
| Schema + cascade | `Data/RundfrageDbContext.cs` (`OnModelCreating`) |
| Migration | `Data/Migrations/…_AddForms.cs` |
| Form service (validation + CRUD) | `backend/src/Rundfrage.Api/Forms/FormService.cs` |
| Field-value validation (the shared rule table) | `backend/src/Rundfrage.Api/Forms/FormFieldValidation.cs` |
| CSV writer (hand-rolled, no library) | `backend/src/Rundfrage.Api/Forms/FormCsvExport.cs` |
| JSON export (mirrors `Polls/PollExport.cs`) | `backend/src/Rundfrage.Api/Forms/FormExport.cs` |
| Operator endpoints | `Endpoints/Admin/FormAdminEndpoints.cs` |
| Participant endpoints | `Endpoints/Public/FormEndpoints.cs` |
| Restore preview | `Data/RestoreService.cs` (`ReadFormCountsAsync`, tolerant read pattern) |
| Admin area (list) | `frontend/src/components/admin/FormsView.vue` |
| Builder | `frontend/src/components/admin/FormBuilderView.vue` |
| Participant surface | `frontend/src/components/form/FormFillView.vue` |
| Shared validation rules (TypeScript side) | `frontend/src/composables/useFormFieldValidation.ts` |
| Stores | `frontend/src/stores/forms.ts`, `formFill.ts` |
| Shared validation fixture | `frontend/tests/fixtures/form-field-validation.json` (consumed by both an xUnit and a Vitest test — R-7) |
| Strings | `frontend/src/locales/de.json` → `form`, `nav.forms` |

## The five things this plan carries that are easy to get wrong

1. **A field's type is immutable once created.** `FormFieldUpdate` has no `type` property; there
   is no endpoint path that changes it. Changing a question's kind of answer means deleting the
   field and adding a new one (spec clarification, 2026-09-22).
2. **`FormFieldValue` rows represent presence, not a three-state value.** A boolean field's
   "unanswered" state is a *missing row*, never a stored `null`/third value. Render it as a
   `v-radio-group` with nothing pre-selected — a `v-checkbox` cannot represent "unanswered" and
   will silently defeat FR-015 (R-13).
3. **Deleting a field must delete its `FormFieldValue` rows; deleting a response must delete only
   its own.** Both are plain cascade deletes once the foreign keys are right — but get the
   direction backwards (cascade from `FormResponse` when you meant `FormField`, or the reverse)
   and one of FR-009 or FR-042a silently breaks while its test still might not catch a narrow
   enough case. Write both deletion tests before the cascade configuration (Principle II).
4. **This feature adds no `CreatorId` and no `OwnerScope` method.** It is genuinely simpler than
   Polls/WishLists here — do not add an owner column "for consistency" or "for later." That is the
   anticipatory abstraction Principle III forbids, and there is no second caller to justify it
   (R-3).
5. **The public form-fetch route refuses a zero-field form exactly like an unknown token.** Do not
   give "form exists but has no fields yet" a distinguishable response — that would be a fourth
   state where the contract only allows the existing not-found/found split (FR-011, FR-021).

## Tests

```bash
dotnet test backend/Rundfrage.slnx
cd frontend && npm run test:unit && npx playwright test
```

Test-first is mandatory (Principle II). The order that matters most here:

- `FormFieldValidationTests` (unit, both languages against the shared fixture) — the rule table
  everything else assumes; write it before any endpoint calls it.
- `FormFieldCascadeTests` (integration) — deleting a field removes only its values; deleting a
  response removes only its values; deleting a form removes everything. Three separate assertions,
  because R-1's schema makes it easy to get one direction right and the other wrong.
- `FormSubmissionValidationTests` (integration) — every required-missing and every malformed-value
  case from the shared fixture, asserted against the real `POST /f/{formToken}/responses`, and
  asserted to have stored nothing on refusal (FR-018).
- `FormFieldLimitTests` — the 50-field cap (FR-011a), including the concurrent-add case, the same
  shape `CreatorLimitTests` used for the 100-Ersteller cap in feature 009.
- `RestoreTests` (extended) — the new form/response counts in the preview, and the tolerant read
  against a pre-010 backup file that lacks the `Forms` table entirely.

Raise `SUBMISSION_LIMIT_PER_HOUR` in the end-to-end environment for the form-journey spec, for the
same reason it is already raised for the poll and wish-list journeys: one machine legitimately
exceeds the production number during a full run. No new environment variable is needed — this
feature reuses the existing policy (R-9).
