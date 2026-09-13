# Implementation Plan: Admin Shell, Dashboard and Settings

**Branch**: `007-admin-shell-dashboard` | **Date**: 2026-09-12 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/007-admin-shell-dashboard/spec.md`

## Summary

Give the admin area a shape: a persistent shell with a left navigation bar, one area per
capability, a dashboard as the landing page, and settings last. Almost everything that moves
already works — poll creation, results, export, import, maintenance mode, restore — so the bulk of
this feature is relocation plus the routes and tests that make the new places real. Two pieces are
genuinely new: the dashboard's six figures, and the shell itself.

Technically: one `AdminShell.vue` parent route with nested children (`research.md` R-1), a Vuetify
navigation drawer that is permanent on wide screens and temporary on narrow (R-2), German addresses
under `/admin` with the dashboard as the index route (R-5), and one new backend endpoint returning
five aggregate figures in a fixed number of queries (R-3).

**One open risk, and it is the plan's first task, not a footnote.** SC-011 promises all six figures
within two seconds at FR-028c's documented scale — 500 polls at feature 002's per-poll limits. That
worst case is 50 million `DayAnswer` rows, and the yes/maybe/no aggregate has to touch all of them.
Two seconds is almost certainly not achievable, and no index changes that, because the cost is the
row count. Task 1 measures it; R-4 states the three possible outcomes and which response goes with
each. The expected outcome is a spec amendment narrowing SC-011, **not** a cached counter — the
spec and Principle IV both forbid that.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); TypeScript 5.9 for Vue 3.5
**Primary Dependencies**: Vue 3.5.42, Vuetify 4.2, Pinia 4.0.3, Vue Router 5.3.1, vue-i18n 11.4.10,
Vite 8.2.2; ASP.NET Core on .NET 10 with EF Core 10.0.11 (`Microsoft.EntityFrameworkCore.Sqlite`)
**Storage**: SQLite, single file on a mounted volume. **No schema change and no migration** — every
dashboard figure is an aggregate over the existing `Poll`, `PollResponse` and `DayAnswer` tables
**Testing**: Vitest 4.1 + Vue Test Utils 2.5 (frontend unit/component); xUnit (backend unit and
integration); Playwright (end-to-end)
**Logging**: Serilog to stdout, unchanged. No new log statements — this feature adds no operation
worth recording, and Principle IV forbids logging response content
**Target Platform**: Linux server backend, modern browsers
**Project Type**: Web application (Vue frontend + ASP.NET Core backend)
**Performance Goals**: SC-011 — six dashboard figures on screen within 2 s. **Under measurement,
see R-4.** No other criterion in this feature is timing-based
**Constraints**: One new HTTP route (`GET /api/v1/admin/dashboard`); a fixed number of queries
regardless of poll count (FR-028b); no new stored data of any kind (FR-034, Principle IV)
**Scale/Scope**: FR-028c documents 500 polls × 1000 answers × 100 days as the supported scale.
Frontend: ~6 new components, 4 new routes, 1 new store. Backend: 1 endpoint, 1 service, 0 entities.
Nine of ten e2e specs gain navigation steps (R-9)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Checked against `.specify/memory/constitution.md` (v2.0.1).

**Pre-Phase 0**: all gates pass.

- [x] **I. Zero-Signup Participation**: Nothing is added between a link and the answer form. The
      shell, its navigation and its dashboard live behind the operator session (FR-008); the
      participant surface keeps its bare app bar and gains no link into the admin area (FR-009).
      SC-010 asserts the participant step count stays zero, and `zero-signup.spec.ts` plus
      `date-poll-journey.spec.ts` prove it (R-9). The maintenance banner is explicitly the
      operator's and never the participant's (FR-026c) — participants keep their own notice.
- [x] **II. Test-First Development**: Every task in Phase 2 will be ordered test-first. This
      feature makes that unusually testable, because the behaviour already exists: the e2e specs
      are the definition of what must still work after the move, so they are edited to the new
      navigation *first*, watched fail, and then the components moved. The genuinely new behaviour
      — the six figures, the shell's nav semantics — gets failing unit tests before its components.
- [x] **III. Simplicity & YAGNI**: No new project, service, layer or dependency. One new backend
      endpoint and one new frontend store, both justified by a current requirement rather than an
      anticipated one. The navigation lists only the one feature area that exists (FR-004) — no
      placeholder entries for unbuilt features. The shell is a parent route rather than an
      abstraction over "areas": there is no area registry, no plugin point, no configuration.
      **Complexity Tracking is empty and is meant to stay that way.**
- [x] **IV. Data Minimization & Operator-Controlled Storage**: No new data is stored. Every figure
      is an aggregate over rows that already exist (FR-034), and the one tempting shortcut — a
      cached running total — is rejected in R-3 on exactly these grounds. No third-party assets;
      the navigation icons come from the already-bundled `@mdi/font`. No participant name, answer
      or access record appears on the dashboard. Retention is untouched: FR-028c documents a scale,
      not a limit, and adds no rule that refuses a poll.
- [x] **Technology Constraints**: Stays entirely within the pinned stack. Vuetify supplies the
      drawer and list; Vue Router supplies nested routes and `aria-current`; EF Core supplies the
      aggregates. Nothing new is introduced on either side.

**Post-implementation re-check (2026-09-12, T055)**: all five gates pass against the delivered
code. 584 tests green — 232 frontend unit, 110 backend unit, 193 backend integration, 49
end-to-end.

- **I. Zero-Signup Participation** — `components/poll/` is untouched by every task. `zero-signup`
  and `date-poll-journey` pass with no change to any *participant* assertion; only their admin
  setup steps moved, which is what R-9 predicted. A new assertion was added: a participant on
  `/u/:token` sees no shell, no navigation, no sign-out and zero `a[href*="/admin"]`.
- **II. Test-First Development** — every behaviour task was preceded by its failing test. Two
  ordering deviations are recorded honestly below.
- **III. Simplicity & YAGNI** — one new backend endpoint, one new store, six new components, no
  new project/layer/service/dependency. Complexity Tracking stayed empty.
- **IV. Data Minimization** — no schema change, no migration, no new stored field. The cached
  aggregate stayed rejected even after measurement made the correct implementation the slow one.
- **Technology Constraints** — nothing introduced outside the pinned stack.

**Two ordering deviations, stated rather than hidden:**

1. `AdminNav.vue` was written during T008 (the shell imports it) rather than at T022, so T011's
   test was written against existing code. It still did its job: it found two real defects — two
   entries carrying `aria-current` at once, and none carrying it on the answers page.
2. US3 was implemented before the end-to-end migration (T016–T021), because signing in now lands
   on `/admin` — the dashboard — so those specs could not run at all until it existed.

**Post-Phase 1 re-check**: all gates still pass. The design added no entity, no migration, no
dependency and no stored field. Two points are worth recording as *deliberately* held:

- Principle IV was the deciding argument in R-3 against the faster implementation of figure 6.
  The slower correct one was chosen, which is what surfaced the R-4 risk. That is the right order:
  the constitution constrains the design, and the criterion bends to it, not the reverse.
- Principle II shaped R-9: the test ids are kept unchanged so that each diff is a move or a
  rename, never both. A behaviour regression must not be able to hide in renaming noise.

## Project Structure

### Documentation (this feature)

```text
specs/007-admin-shell-dashboard/
├── plan.md              # This file
├── research.md          # Phase 0 — R-1..R-9, including the SC-011 risk
├── data-model.md        # Phase 1 — the figures and how each is derived
├── quickstart.md        # Phase 1 — developer guide
├── contracts/
│   ├── openapi.yaml     # Phase 1 — the one new endpoint
│   └── ui-contract.md   # Phase 1 — shell, navigation, areas, states, a11y
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 output — NOT created by /speckit-plan
```

### Source Code (repository root)

```text
backend/
├── src/Rundfrage.Api/
│   ├── Endpoints/Admin/
│   │   └── DashboardEndpoint.cs          # NEW — GET /admin/dashboard
│   ├── Polls/
│   │   └── DashboardProjection.cs        # NEW — the five aggregate queries
│   └── Program.cs                        # EDIT — register endpoint + projection
└── tests/
    ├── Rundfrage.Api.UnitTests/          # EDIT — projection arithmetic
    └── Rundfrage.Api.IntegrationTests/   # EDIT — endpoint, auth, empty, scale spike

frontend/
├── src/
│   ├── components/
│   │   ├── admin/
│   │   │   ├── AdminShell.vue            # NEW — drawer, app bar, banner, sign-out
│   │   │   ├── AdminNav.vue              # NEW — the three entries
│   │   │   ├── DashboardView.vue         # NEW — the six figures
│   │   │   ├── SettingsView.vue          # NEW — three sections, restore last
│   │   │   ├── PollAnswersView.vue       # NEW — one poll's answers, own address
│   │   │   ├── PollList.vue              # EDIT — summaries only; reveal forms
│   │   │   ├── MaintenanceSwitch.vue     # EDIT — loses the banner (R-6)
│   │   │   ├── ImportPanel.vue           # unchanged, newly revealed
│   │   │   ├── RestorePanel.vue          # unchanged, moves to settings
│   │   │   └── PollForm.vue              # unchanged, newly revealed
│   │   └── poll/                         # UNTOUCHED — Principle I
│   ├── stores/dashboard.ts               # NEW
│   ├── api/client.ts                     # EDIT — fetchDashboard + types
│   ├── locales/de.json                   # EDIT — nav, dashboard, settings labels
│   ├── router.ts                         # EDIT — nested admin routes
│   └── App.vue                           # EDIT — chrome moves into the shell
└── tests/unit/                           # EDIT/NEW — shell, nav, dashboard, settings

e2e/tests/                                # EDIT — 9 of 10 specs gain navigation (R-9)
```

**Structure Decision**: The existing `backend/` + `frontend/` split is unchanged. Within the
frontend, new views live in `components/admin/` beside the components they are assembled from,
rather than in a new `pages/` or `views/` directory — the project has never had one, four files do
not justify inventing one, and Principle III asks for the second concrete use before the
abstraction. `components/poll/` is deliberately untouched: that is the participant surface, and
Principle I is the reason this feature has no business there.

## Phase Ordering

The measurement spike comes first because its outcome can change the spec, and building the
endpoint before knowing the answer risks building it twice.

1. **Spike** — seed FR-028c's worst case, time figure 6, record the number (R-4).
   *Gate: if over budget, amend SC-011 and FR-028c before proceeding, or return figure 6 to the
   user. Do not proceed by making the figure fast in a way the spec forbids.*
2. **Backend** — `DashboardProjection` + endpoint, test-first. Independent of all frontend work.
3. **Shell** — `AdminShell`, `AdminNav`, nested routes, banner lifted from the switch (R-6).
   Delivers User Story 1's navigation.
4. **Areas** — poll list trimmed to summaries with revealed forms; answers page as its own route;
   settings page with its three sections. Completes User Stories 1 and 2.
5. **Dashboard** — `DashboardView` + store, consuming step 2. User Story 3.
6. **End-to-end migration** — nine specs gain navigation, keep their assertions (R-9).
   **This is the test half of steps 3–5, not a phase after them.** An earlier wording made it
   depend on 3, 4 and 5, which contradicted Principle II and this plan's own Constitution Check;
   `tasks.md` always had it right (T016–T021 are failing tests inside US1). Corrected here after
   `/speckit-analyze` flagged it (finding F1).

Steps 2 and 3 are independent and can proceed in parallel. Step 5 depends on 2 and 3.

## Complexity Tracking

> No entries. No constitution gate is violated, no new project/layer/service/dependency is
> introduced, and no abstraction is added ahead of a second concrete use. The one place this plan
> could have grown complexity — a cached aggregate to make figure 6 fast — was rejected in R-3 on
> Principle IV grounds, and the resulting performance question is being answered by measurement
> (R-4) rather than by architecture.
