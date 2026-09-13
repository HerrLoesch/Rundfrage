# Quickstart: Admin Shell, Dashboard and Settings

**Feature**: 007-admin-shell-dashboard

## What this feature is

A rearrangement, plus one new screen. The admin area becomes a shell with a left navigation bar;
date polls get an area of their own; settings and maintenance move to a page that is always last;
and a dashboard becomes the landing page. Almost nothing changes *behaviour* — the value is in
where things are.

Read in this order: [spec.md](./spec.md) for what and why, [research.md](./research.md) for the
nine decisions and the one open risk, [data-model.md](./data-model.md) for the figures,
[contracts/ui-contract.md](./contracts/ui-contract.md) for elements, states and test ids.

## The measurement, and what it changed (resolved)

The spike ran on 2026-09-12 and **SC-011 and FR-028c were amended as a result** (research.md R-4).
The design did not change; the criterion did.

```bash
cd backend
dotnet test tests/Rundfrage.Api.IntegrationTests --filter "Category=DashboardScale"
```

| DayAnswer rows | Read | Disk |
|---------------:|-----:|-----:|
| 1,000,000 | 0.24 s | 218 MiB |
| 2,500,000 | 0.67 s | 547 MiB |
| 5,000,000 | 1.37 s | 1,097 MiB |

Linear, at about **0.27 s and 219 MiB per million**. FR-028c's original worst case of 50,000,000
rows extrapolates to ~14 s against an ~10.7 GB database — a size that contradicts the
constitution's own reason for choosing SQLite. The supported scale is now 500 polls holding up to
2,500,000 day-answers (measured 0.80 s, roughly 2.5× margin under SC-011).

`DashboardScaleTests` keeps both halves: an asserting regression test at the documented scale, and
a reporting theory at three row counts that shows the linearity the bound rests on.

Do **not** resolve a future slowdown with a cached running total. Principle IV forbids the extra
record, and every deletion path — response delete, poll delete, restore, import, the hourly
retention sweep — would have to decrement it correctly. The one that forgot would leave a figure
wrong forever with nothing to detect it.

## Running it

```bash
# Backend
cd backend && dotnet run --project src/Rundfrage.Api

# Frontend
cd frontend && npm run dev

# Checks that must be green (constitution gate 2)
cd frontend && npm run test:unit && npm run typecheck && npm run build
cd backend  && dotnet test
```

The build fails on warnings and type errors (commit `7871dec`), so `typecheck` is not optional.

**End-to-end runs against the container, and needs two things from the environment:**

```bash
set -a && . ./.env && set +a                 # E2E_ADMIN_USER / E2E_ADMIN_PASSWORD
SUBMISSION_LIMIT_PER_HOUR=1000 docker compose up -d --build
cd e2e && npx playwright test
```

The submission limit matters. Its default is 10 per hour per source (002 FR-027a), every browser
in the suite shares one source, and the suite makes well over ten participant submissions — so
without the override, tests fail late in the run with a *participant* symptom that looks alarming
and is not. CI already sets it; the local invocation has to as well.

## Addresses

| Address | Area |
|---------|------|
| `/admin` | Dashboard — the index child, so this is also the "no area named" landing (FR-006) |
| `/admin/terminfindungen` | Poll list |
| `/admin/terminfindungen/:pollId` | One poll's answers |
| `/admin/einstellungen` | Settings |
| `/admin/anmelden` | Sign-in — outside the shell |
| anything else under `/admin` | redirects to `/admin` (FR-007) |

German paths, continuing the existing `/admin/anmelden`. `MapFallbackToFile("index.html")` is
already in `Program.cs`, so every address survives a reload with no backend change.

The operator's existing `/admin` bookmark keeps working and now lands on the dashboard — which is
what FR-006 specifies, not an accident.

## Where things moved

| Control | Was | Is now |
|---------|-----|--------|
| Maintenance toggle | `/admin`, top of page | `/admin/einstellungen`, section 1 |
| Backup download | `/admin`, top of page | `/admin/einstellungen`, section 2 |
| Restore | `/admin`, mid page | `/admin/einstellungen`, section 3, separated |
| Poll import | `/admin`, always open | `/admin/terminfindungen`, behind a reveal |
| Poll create | `/admin`, always open | `/admin/terminfindungen`, behind a reveal |
| Poll answers | inline under the card | `/admin/terminfindungen/:pollId` |
| Export, delete poll | `/admin` | `/admin/terminfindungen` — unmoved within the area |
| Sign-out | `/admin`, top of page | the shell — every area |
| Maintenance banner | inside `MaintenanceSwitch` | the shell — every area |

`App.vue` is now nothing but `<v-app><RouterView/></v-app>`. The two surfaces draw their own chrome
in layout routes: `AdminShell.vue` for the admin area, and `BareShell.vue` — app bar and wordmark,
no navigation — for the participant views and the sign-in form.

Import stays with polls on purpose: it *makes* a poll. Keeping it away from restore puts "add one
poll" and "replace every poll" in different areas, which enforces 005 FR-001 more strongly than 005
itself did.

## Three traps

**1. The banner and the switch move together.** `MaintenanceSwitch` currently renders the banner as
its own child. If you relocate the switch to settings in one commit and lift the banner in another,
the intermediate state has the warning nowhere — FR-026 violated by a commit that passes its own
tests. One task, both changes (R-6).

**2. `aria-current` *is* written by hand, and has to be.** The plan said the opposite; the
implementation proved it wrong and R-2 records the correction. `v-list-item` does not derive
`aria-current` from Vuetify's `active` prop — it comes from the link's own *inclusive* matching,
which marked `/admin` current on every address beneath it (two entries at once, FR-002 violated).
Making it exact then broke FR-014e, because `poll-answers` is a **sibling** route record of
`polls`, not a descendant, and Vue Router derives "active" from record ancestry rather than path
prefixes. Each entry therefore names the route names it owns, and that one list drives both
`active` and an explicit `aria-current`. Assert `aria-current`, never a CSS class.

**2b. A `permanent` drawer still obeys `v-model`.** `<v-navigation-drawer permanent>` with a model
of `false` is closed — the entries render but sit outside the viewport with `tabindex="-2"`.
`AdminNav` computes visibility from the breakpoint instead.

**3. The loading state may not render zeros.** SC-011 allows up to two seconds. Two seconds of `0`
tiles is a dashboard making false claims. Render placeholders until figures arrive, and render
*nothing* numeric when the read failed (FR-032, R-7).

## Why figure 6 is correct without a filter

`DayAnswer` has three `Availability` values and no fourth: an unanswered day stores **no row**
(002 research R-8). So `GROUP BY Availability` cannot count a non-answer. That is what makes
FR-028a — "must equal the sum of every poll's per-day summaries" — true by construction: both are
the same grouped count over the same rows, one filtered to a poll and one not.

There is no "exclude the unanswered" filter that could later be forgotten. Do not add one.

## Tests

Nine of the ten e2e specs touch the admin area and need navigation steps inserted. Only
`timezone.spec.ts` is untouched.

**Keep the existing `data-testid` values.** The migration changes where a test navigates, not what
it asserts. Renaming ids in the same change means every diff mixes a move with a rename, and a real
regression can hide in the noise (R-9).

`zero-signup.spec.ts` and `date-poll-journey.spec.ts` prove the participant path (constitution gate
3). If either needs a change to its *participant* assertions, the implementation is wrong — FR-009
and SC-010 say this feature does not touch that path.

`noLiteralStrings.spec.ts` already enforces FR-035 and will cover the new components automatically.
Add catalogue entries as you add components, not afterwards.

## Spec Kit

```bash
export SPECIFY_FEATURE=007-admin-shell-dashboard
```

The constitution requires this for every Spec Kit command, because branch validation expects an
`NNN-slug` name.
