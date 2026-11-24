# PROJECT_PROGRESS

Single source of truth for where the project stands. Updated after every milestone.
If a session ends, resume from the first module that is not COMPLETE.

Last updated: 2026-11-23

## How we work

- One module at a time, in the order below. A module is COMPLETE only when every gate passed with real execution evidence.
- Gates: 1 Research, 2 Design, 3 Implementation (backend + frontend + DB), 4 Unit tests, 5 Integration tests, 6 End-to-end tests, 7 Code review, 8 Regression.
- Status words: NOT STARTED, IN PROGRESS, INCOMPLETE (work exists but a gate failed or is missing), COMPLETE.
- Integration status words: Implemented, Mock-tested, Real-provider-tested, Blocked by external configuration.

## Technology

| Part | Choice |
|---|---|
| Backend | ASP.NET Core 8 Web API, layered (API, Presentation, Services, Services.Abstraction, Core, Infrastructure, Shared) |
| Data access | EF Core 8 used directly through `IHubDbContext` and `IELearningDbContext` (no repository layer, decided by the owner) |
| Database | PostgreSQL 17. One central Hub database + one database per teacher |
| Errors | Result pattern + `ApiBaseController.HandleResult` + ProblemDetails |
| Frontend | `web/`: React 18 + TypeScript + Vite 4 (the machine has Node 16, so Vite 5 is not possible yet). Arabic default (RTL) + English |
| Tests | xUnit. Integration tests run against real PostgreSQL test databases |

## Module map

| # | Module | Depends on | Status |
|---|---|---|---|
| 0 | Foundation: solution layout, Result pattern, Hub + per-teacher databases | - | COMPLETE (see evidence below) |
| 1 | Accounts, login, two-factor, choosing a teacher, platform admin (register teachers) | 0 | COMPLETE |
| 2 | Teacher management: centers, groups, schedules, students, assistants, audit log | 1 | COMPLETE |
| 3 | Subscription code generator and redemption: 30-day code and free-week code (owner request of 2026-11-22) | 1, 2 | NOT STARTED |
| 4 | Monthly tracking board: attendance, cash payments, custom columns, filters | 2 | NOT STARTED |
| 5 | Exams, homework, quiz after homework, grading with teacher approval, anti-cheat | 2 | NOT STARTED |
| 6 | AI: question generation, essay grading, chatbot (needs AI provider key) | 5 | NOT STARTED |
| 7 | Points, leaderboards, teacher analytics dashboard | 4, 5 | NOT STARTED |
| 8 | Parent weekly WhatsApp report (needs WhatsApp provider) | 4, 5 | NOT STARTED |
| 9 | Recorded videos with watermark, live classes (needs video/live provider) | 2, 3 | NOT STARTED |
| 10 | Online payment (needs Paymob account) | 3 | NOT STARTED |
| 11 | Platform Owner Dashboard (owner request of 2026-11-22): built in parts, each part after the module that produces its data | see below | NOT STARTED (teacher list and on/off already exist from Module 1) |
| X | Cross-cutting: mock server for external providers, performance tests, final QA report, UI PDF | all | NOT STARTED |

## Module 0 — Foundation — COMPLETE

Evidence (executed on 2026-11-22):

- `dotnet build E-Learning.sln`: 0 errors, 0 warnings.
- Hub migration applied to `elearning_hub`: tables `tenants, accounts, memberships, refresh_tokens, account_devices, device_resets, academic_terms, code_batches, codes`.
- Teacher migration applied to `elearning_demo`: tables `users, centers, groups, group_schedules, students, subscription_periods, audit_logs`.
- API started; request with a token for an existing teacher passed the tenant middleware; a token for a missing teacher got `404 Tenant.NotFound`.

Design documents: `docs/01` to `docs/05`. The current database design is `docs/05-design-hub-and-teacher-databases.md`.

## Module 1 — Accounts, login, platform admin — COMPLETE

### Requirements (from docs/01, docs/05 and the features PDF)

- One account per person (phone number, unique on the platform). One login, then choose the teacher.
- Teacher and assistant must use two-factor (authenticator app, TOTP).
- Student account works on one device only.
- Account locks for a while after repeated wrong passwords.
- Platform admin registers teachers; each new teacher gets a new database.
- Requests are routed to the chosen teacher's database; a suspended membership stops working at once.

### Gate status

| Gate | Status | Evidence (all executed on 2026-11-22) |
|---|---|---|
| 1 Research | done | docs/01, docs/05 |
| 2 Design | done | docs/06-module-1-accounts-and-login.md |
| 3 Implementation: backend | done | `dotnet build`: 0 errors, 0 warnings |
| 3 Implementation: frontend | done | `web/` (React + TypeScript). `npm run build`: type check and build pass. Arabic RTL default, English LTR |
| 4 Unit tests | done | backend 66 passed, 0 failed. frontend 15 passed, 0 failed |
| 5 Integration tests | done | 70 passed, 0 failed, against real PostgreSQL |
| 6 End-to-end tests | done | 10 browser tests passed, 0 failed (Playwright, Chromium), on temporary databases |
| 7 Code review | done | fixes listed under "Review fixes" below |
| 8 Regression | done | full run after the review fixes: 66 + 70 + 15 + 10, all passed |

Totals: 161 automated tests executed, 161 passed, 0 failed, 0 skipped. Coverage percentage was not measured.

### Review fixes made in gate 7

- Login now spends the same time on unknown phone numbers as on real accounts (stops finding accounts by timing).
- Phone validation rejects digits followed by other text (found by a test).
- The website renews the session only once at a time. Two renewals at the same moment would have sent a used refresh token and ended every session.
- The two-factor setup screen asks the server for a secret exactly once.
- Added protective response headers, `.gitignore` and `appsettings.Development.example.json`.
- Teacher database connections: no idle connections, capped pool per teacher.

### Not covered (honest list)

- The single-device rule and the lockout are tested through the API, not in the browser.
- No performance or load test was run for this module.
- Test coverage percentage was not measured.
### Backend endpoints (all implemented and tested)

| Method and path | Who | What |
|---|---|---|
| POST /api/auth/login | anyone | phone + password (+ device id for students) |
| POST /api/auth/two-factor/setup | staff, first login | returns the authenticator secret |
| POST /api/auth/two-factor/verify | staff | 6-digit code, returns the session |
| POST /api/auth/refresh | anyone with a refresh token | new session; also chooses or switches teacher |
| POST /api/auth/logout | anyone | cancels the refresh token |
| POST /api/auth/accept-invite | staff | sets the password with the one-time invite code |
| GET /api/auth/tenants/{slug} | anyone | teacher name and logo for the teacher's link |
| GET /api/profile | member of a teacher | own row in that teacher's database |
| GET, POST /api/admin/tenants | platform admin | list and register teachers |
| PUT /api/admin/tenants/{id}/status | platform admin | turn a teacher on or off |
| POST /api/admin/tenants/{id}/teacher-invite | platform admin | new invite code (password reset) |
| GET, POST /api/admin/academic-terms | platform admin | school term dates |

### Where the code is

- Services: `E-Learning.Services/Auth`, `/Admin`, `/Profile`, `/Security` (Totp, TokenService, SecureCode, AccountInvite).
- Controllers: `E-Learning.Presentation/Controllers`.
- Wiring: `E-Learning/Extensions/ServiceCollectionExtensions.cs`, `E-Learning/Tenancy`, `E-Learning/Security`.
- Tests: `tests/ELearning.UnitTests`, `tests/ELearning.IntegrationTests`, `web/src/frontend.test.ts`, `web/e2e`.
- Website: `web/src` (pages, components, i18n, api, auth).

### How to run the tests

`dotnet test E-Learning.sln` (needs local PostgreSQL; the server settings are read from `E-Learning/appsettings.Development.json`, or from the `ELEARNING_TEST_POSTGRES` environment variable). Each run creates its own databases named `eltest_...` and deletes them at the end.

### Security review notes so far

- Verified by tests: same answer for unknown phone and wrong password; lockout after 5 wrong tries; two-factor codes cannot be reused; guessing two-factor codes locks the account even when the password is known; a challenge token is refused as a login token; changed and expired tokens are refused; refresh tokens rotate and reuse ends all sessions; suspended membership and turned-off teacher lose access at once; students and teachers cannot call admin endpoints; connection strings, password hashes and two-factor secrets never appear in responses; two-factor secret, refresh token and invite code are not stored as plain text.
- Bug found by a test and fixed: a phone number followed by other text (for example `01012345678; DROP TABLE ...`) was accepted as a valid phone. No injection was possible (queries are parameterised), but the input is now rejected.
- Known limits: two-factor secrets are encrypted with ASP.NET Data Protection keys kept on the server's disk; production must store these keys in a safe, persistent place. Rate limiting is per IP address and in memory (one server only).

## Module 2 — Teacher management — COMPLETE

Requirements: docs/01 (T-03 to T-10, A-02, A-03, S-04). Design notes: docs/07-module-2-teacher-management.md.

| Gate | Status | Evidence (executed on 2026-11-23) |
|---|---|---|
| 1 Research | done | docs/01 |
| 2 Design | done | docs/07 |
| 3 Implementation: backend | done | `dotnet build`: 0 errors, 0 warnings |
| 3 Implementation: frontend | done | pages in `web/src/pages/teacher` (students, groups, centers, assistants, activity log), Arabic and English; `npm run build` passes |
| 4 Unit tests | done | no new pure logic in this module; backend 66 and frontend 15 unit tests still pass |
| 5 Integration tests | done | 44 new tests; full suite 114 passed, 0 failed, real PostgreSQL |
| 6 End-to-end tests | done | 6 new browser tests; all 16 browser tests passed, 0 failed |
| 7 Code review | done | imports tidied; shared invite dialog reused instead of copied |
| 8 Regression | done | 66 + 114 + 15 + 16 = 211 tests, all passed |

Rules verified by tests: a teacher cannot read, change, suspend or unlock another teacher's student; students get 403 on all staff endpoints; assistants get 403 on assistants and activity log; the parent phone is masked for assistants and kept when an assistant edits a student; every assistant change is logged automatically and a failed change leaves no log row; suspension works on the next request; device changes are limited to 2 per term per account, shared across teachers.

Not covered: no load test; competitor ideas (Excel import, QR attendance cards) are not built; assistants have one fixed permission set (no per-assistant permissions yet).

## How to run the browser tests

- Normal way: `powershell -File scripts\run-e2e.ps1` (starts everything on temporary databases, runs the tests in short sessions, cleans up).
- On this development machine, inside the assistant's tool, any single process tree that ran the browser for more than about 70 seconds was frozen and closed from outside (cause not found; an endpoint protection service is running on the machine and may be related). The tests themselves pass. Workaround that was used for the evidence above: `powershell -File scripts\run-e2e.ps1 -ServersOnly` in one terminal, then in `web`: `npx playwright test e2e/accounts-and-login.spec.ts` and after it `npx playwright test e2e/teacher-management.spec.ts`.

## Next: Module 3 — Subscription codes — NOT STARTED (resume here)
Requested: the platform owner generates two code types (30-day subscription, free week of 7 days), one or many per batch, optional last date to redeem, searchable table, copy and export, statuses Available / Redeemed / Expired / Revoked, who redeemed and when, revoke unused codes, statistics per type, audit log of generate / redeem / revoke, student redemption screen showing type, start and end dates. Tests must include concurrent redemption.

Already in place: hub tables `code_batches` and `codes` (with a concurrency token), secure code generator `SecureCode` (unit tested), teacher table `subscription_periods`.

Open point for the design step: docs/02 described an "activation code" that opens the account and starts the trial. The new request describes a "free week code" for trials and promotions. The design must say whether these are the same code. Stacking rule already agreed in docs/02 (D6): redeeming before the end adds to the end of the current period.

## Module 11 — Platform Owner Dashboard (owner request of 2026-11-22) — NOT STARTED

A separate area for the platform owner, never reachable by teachers. It will be built in parts, because most parts show data that other modules produce:

| Part | Needs | 
|---|---|
| Internal roles (Super, Support, Finance, Technical, Content) with fine permissions, and an audit log of admin actions | Module 1 (done). This part comes first. |
| Teacher management: search, filter, profile, counts, activity | Module 2 |
| Student management across teachers, device and activation status | Modules 2 and 3 |
| Subscription codes | Module 3 |
| Overview numbers and charts, growth analytics | Modules 2, 3 |
| Financial management, refunds, gateway reconciliation | Module 10 (needs Paymob account) |
| AI usage and cost | Module 6 (needs AI provider key) |
| Support tickets | independent, can be built any time after the internal roles |
| Platform configuration, system monitoring | after the providers exist; secrets must never reach the frontend |

Existing today from Module 1: admin login with two-factor, list of teachers, register teacher, turn a teacher on or off, new invite code, academic terms.
## Research

- Competitor research: DONE on 2026-11-22, in `docs/research/competitor-research.md` (about 30 products, feature matrix against the 8 closest, sources). Nothing was tested hands-on: "verified" there means stated in a published source.
- Findings that change our plan (to be decided module by module, not yet implemented):
  - Egyptian competitors offer QR/barcode attendance with student cards, offline attendance, daily cash closing, Excel import (relevant to Modules 2 and 4).
  - Hader sells a student card for 75 EGP per year; our plan is 50 EGP per 30 days. Pricing needs the owner's attention.
  - Egypt's data protection law (151/2020) regulations were issued 1 Nov 2025: licensing, parental consent for children's data, and a permit to send data abroad (this covers sending students' answers to a foreign AI provider). A local lawyer is needed before the pilot.
  - Sending WhatsApp reports from the teacher's own number needs the WhatsApp Business API with opt-in and approved templates (Module 8).

## Blockers and external configuration needed from the owner

| Item | Needed for | Status |
|---|---|---|
| AI provider API key | Module 6 | not requested yet |
| WhatsApp Business provider account | Module 8 | not requested yet |
| Video / live streaming provider account | Module 9 | not requested yet |
| Paymob merchant account | Module 10 | not requested yet |
| Hosting inside Egypt | data residency claim | not configured; do not claim it yet |

## Known issues

- `git` is not installed on this machine, so there is no version history. Installing git is strongly recommended.
- Node is version 16 (old). Upgrading to Node 20+ would allow newer frontend tooling.
- The unused empty projects `E-LearningHub.API`, `.Presentation`, `.Services`, `.Services.Abstraction`, `.Shared` and the orphan folder `E-Learning.Infrastructure` are still in the solution folder.
