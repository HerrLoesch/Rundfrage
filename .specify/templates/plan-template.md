# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [C# on .NET LTS + TypeScript/JavaScript for Vue 3 — record exact versions]  
**Primary Dependencies**: [Vue 3, Vuetify, Pinia, Vite; ASP.NET Core Web API — record exact versions]  
**Storage**: [PostgreSQL, self-hosted — record version and schema/migration approach]  
**Testing**: [Vitest + Vue Test Utils, xUnit, Playwright — per constitution]  
**Logging**: [Serilog, structured to stdout — per constitution]  
**Target Platform**: [Linux server backend + modern browsers for the participant flow]
**Project Type**: Web application (Vue frontend + ASP.NET Core backend)  
**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]  
**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]  
**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Check each gate against `.specify/memory/constitution.md` (v1.1.0). Any unchecked box
requires a justified row in Complexity Tracking below.

- [ ] **I. Zero-Signup Participation**: No step is added between a survey link and the
      answer form. Anonymous-only capabilities use link-scoped secrets, not participant
      identity.
- [ ] **II. Test-First Development**: Test tasks precede their implementation tasks; each
      behavior has a test that fails first.
- [ ] **III. Simplicity & YAGNI**: No new project, layer, service, or dependency without a
      Complexity Tracking justification. No speculative abstraction.
- [ ] **IV. Data Minimization & Operator-Controlled Storage**: Only survey-question data is
      collected; no third-party assets or trackers; data stays in the self-hosted database;
      retention outcome defined.
- [ ] **Technology Constraints**: Stays within Vue 3 + Vuetify + Pinia + Vite, ASP.NET Core
      on .NET LTS, self-hosted PostgreSQL, Serilog, Vitest/xUnit/Playwright.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
