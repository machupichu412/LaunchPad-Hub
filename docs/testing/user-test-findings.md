# Multi-role user test findings — 2026-09-17

Run of [user-test-scenarios.md](user-test-scenarios.md) against the local demo (in-memory database)
with persona login, on branch `test/user-scenarios`.

**Summary:** 8 bugs found and fixed, each with a regression test that fails without the fix.
19 logic gaps reported for a product decision. The core security control held: no Sponsor,
Candidate, or Hiring Manager response contained a review score, risk flag, or hire suggestion.

## How it was run

- **Browser:** separate tabs signed in as different personas at the same time. The project
  lifecycle (X1) was driven through the UI with Sponsor and Ops tabs side by side. A scripted
  crawl loaded all 29 routes as 9 personas (261 page loads), recording redirects, error text,
  and console errors.
- **API:** the same running server, called directly with `X-Dev-Persona`. It covered a
  30-endpoint × 10-persona authorization matrix with a scan for score fields in every
  response, plus the write paths of every cross-role flow.
- **Not run locally:** anything that depends on SQL Server. No local SQL instance was available
  (Docker not running), so those scenarios are listed under
  [Not verifiable locally](#not-verifiable-locally) rather than reported as passing.

## Results by scenario

| Area | Scenarios | Result |
|---|---|---|
| Candidate | C1–C12 | Pass after fixes F-01, F-07. Gaps G-02, G-03, G-09, G-12 |
| Sponsor | S1–S10 | Pass after fixes F-04, F-06. Gaps G-08, G-10, G-11 |
| Program Ops | O1–O9 | Pass after fixes F-02, F-03, F-05. Gaps G-04, G-14, G-15, G-16. O5 risk register not verifiable |
| Executive | E1–E3 | Pass. Gap G-07. KPI tiles not verifiable |
| Hiring Manager | H1 | Pass (no scores; all non-pipeline routes and APIs blocked). Gap G-08 |
| Multi-role / edge | M1–M4 | Pass after fix F-08. Gap G-06 |
| X1 Project lifecycle | UI, 4 tabs | Pass (fix F-03 found here) |
| X2 Match → assignment | API | **Blocked after OpsApproved** — G-01. Gap G-05 |
| X3 Direct request | API | Pass. Gap G-05 |
| X4 One live assignment | API | App-level check passes; filtered unique index not verifiable |
| X5 Work delivery | API | Pass (byte-identical download, other sponsor 403). Gap G-02 |
| X6 Reviews and risk | API | Reviews and redaction pass after F-06, F-07; risk view not verifiable |
| X7 Community | API + UI | Pass (rate limit 429 after 10, role label can't be spoofed, HTML rendered as text). Gap G-05 |
| X8 Cohort closed | API | Fail — G-04 |
| X9 Concurrency | — | Not verifiable locally |
| X10 Status change during assignment | API | Undefined behaviour — G-13 |

### What held up

- **Redaction:** `averageScore`, `hasPerformanceRisk`, `hasEngagementRisk`, and
  `suggestedHireOutcome` appear only for Executive and ProgramOps, on every endpoint checked.
  Evaluations for candidates and sponsors carry qualitative fields only.
- **Role gating:** across the 261 route loads, no page opened for a role whose API would refuse
  it. Profile-less sponsors were sent to sponsor onboarding. The only mismatch runs the safe
  way: the UI is stricter than the API on `/community` (G-17). Every Ops-only action returned
  403 for Executive.
- **Ownership:** a second sponsor got 403 on every action against another sponsor's project,
  assignment, reviews, to-dos, and deliverable downloads.
- **Validation and idempotency:** duplicate onboarding (409), bad dates and capacity (400),
  out-of-range ratings (400), forward-only delivery stage, case-insensitive skill de-duplication,
  refusal to delete an in-use skill (409), and repeat review scheduling (no duplicates) all
  behaved correctly.

---

## Fixed bugs

| ID | Severity | Bug | Found in | Fix |
|---|---|---|---|---|
| F-01 | **High** | A candidate could open and rate interest in any cohort's projects by id. The list endpoint filtered by cohort; `open-detail` and `interest` did not. | C12 — `cand-c2` got 200 on a spring-cohort project | `f9c94cb` |
| F-02 | **High** | Ops **Deny** had no status check. Calling it on an OpsApproved or Active assignment silently withdrew the candidate from their project. | O3 — denying Jordan's approved assignment returned 200, and his assignment disappeared | `f9c94cb` |
| F-03 | Medium | A project cancelled while pending stayed in the Ops approval queue and could still be approved. | X1 — cancelled *API Gateway Migration* showed Approve/Reject | `f9c94cb` |
| F-04 | Medium | Creating a project with a nonexistent cohort id was accepted. On SQL Server it fails at the foreign key as a 500. | S2 — cohort 999 returned 201 | `f9c94cb` |
| F-05 | Medium | Cohort status changes wrote no `AuditEvent`, contrary to CLAUDE.md's "every approval, status change". | O1 | `f9c94cb` |
| F-06 | Medium | Submitting the same review twice was not checked. In-memory it created a duplicate row; on SQL Server the unique index turns it into a 500. | S7 | `f9c94cb` |
| F-07 | Medium | If a sponsor submitted a review before Ops scheduled reviews, the scheduled to-do stayed open forever. Resubmitting is refused (F-06), so it could never be completed. | X6 | `b407399` |
| F-08 | Low | An unknown URL rendered an empty page with no message or way back. | Route crawl | `ff28350` |

Regression tests: `tests/LaunchPad.Api.IntegrationTests/UserScenarioRegressionTests.cs` (F-01 to
F-07, 9 cases) and `src/LaunchPad.Web/src/features/shared/NotFound.test.tsx` (F-08). Each API test
was run against the unfixed controllers and failed before it was accepted.

## Logic gaps (need a product decision — not changed)

| ID | Severity | Gap | Evidence | Recommendation |
|---|---|---|---|---|
| G-01 | **High** | **Nothing moves an assignment from OpsApproved to Active, or from Active to Completed.** No controller, repository, or Function sets either status; only seed data is Active. Reviews require Active, and review scheduling only targets Active, so no assignment created through the app can ever be reviewed. | X2: Morgan and Jordan stuck at OpsApproved; `grep "Status = AssignmentStatus.Active"` finds only the seeder and tests | Decide the trigger (start date via a nightly Function, sponsor "kick off", or Ops action) and the completion trigger. |
| G-02 | **High** | Deliverable uploads accept any file: an `.exe` and an HTML file labelled `application/pdf` were both stored and served back. Avatars check the declared type but not the bytes (`image/png` containing text was accepted). | C7, C3 | Allow-list extensions and verify magic bytes. Already listed as deferred in `PERF_AUDIT.md`. |
| G-03 | Medium | Candidate self-onboarding requires **exactly one** Active cohort, but new cohorts are created Active and nothing deactivates the old one. Opening next season's cohort blocks every new candidate. The demo seed has two Active cohorts, so onboarding fails out of the box. | C1: 409 "More than one cohort is active" | Create cohorts as Planned, or let onboarding pick by date or invite. Fix the seed either way. |
| G-04 | Medium | Marking a cohort Completed changes nothing: its projects stay in the marketplace, sponsors can create projects in it, matching runs for it, and scheduling reviews for a completed or nonexistent cohort returns 200. | X8 | Treat Completed as closed for marketplace, project creation, and matching; 404 unknown cohorts. |
| G-05 | Medium | Notifications stop after "You've been proposed". Nobody is notified when a sponsor recommends, Ops approves or denies, or a sponsor directly requests a candidate. Authors aren't notified of comments or likes. | X2, X3, X7: candidate and sponsor inboxes empty after approval | Notify the candidate and sponsor on each assignment decision at minimum. |
| G-06 | Medium | A user who is both ProgramOps and Sponsor can submit Sponsor-on-Candidate reviews on **any** project, because the Ops ownership bypass also applies to reviews. The review then appears in that candidate's evaluations. | M1: `ops-sponsor` reviewed Sam's assignment (200) | Require actual project ownership for review submission regardless of other roles. |
| G-07 | Medium | Executive bypasses project ownership: it can edit and cancel any project and add to-dos to any assignment, while every Ops-only action returns 403. This matches the guide (§5.2) but conflicts with Executive being a dashboard role. | E2 | Confirm whether Executive should write at all; if not, drop it from `OwnsProjectHandler`'s bypass. |
| G-08 | Medium | Every Sponsor (including one with no sponsor profile) and every Hiring Manager can list all candidates in all cohorts, with email and hire outcome, via `/api/candidates` and `/pipeline`. | Authorization matrix | Scope sponsors to their cohort(s) and require a profile; confirm Hiring Manager scope. |
| G-09 | Low | The numeric match score is returned to candidates (dashboard, assignments) and sponsors (eligible-candidate `score`, matches). It isn't a review rating, but the guide says sponsors and candidates "must never receive numeric scores". | Score-field scan | Decide whether match score is exempt; if not, redact in the DTO like review scores. |
| G-10 | Low | Typing an unknown skill name on a project silently adds a new global skill (category "Uncategorized") to the normalized taxonomy every candidate sees. | S2: `NotARealSkillXYZ` appeared in the skill picker | Restrict to existing skills, or queue new names for Ops review. |
| G-11 | Low | Sponsors can upload deliverables onto a candidate's assignment; to-dos can be added to assignments that aren't active; past due dates are accepted. | X5 | Decide who owns deliverables; guard to-dos by assignment status. |
| G-12 | Low | Candidates can publish community posts of type Announcement. | C9 | Limit Announcement to Ops. |
| G-13 | Low | Setting a candidate to No Hire leaves their active assignment untouched. | X10 | Define the cascade (or explicitly none) for each outcome. |
| G-14 | Low | Duplicate cohort names are allowed. | O1 | Unique name per program. |
| G-15 | Low | "Review matches" is offered on Draft and Rejected projects. The Ops dashboard's active project count includes drafts and pending/rejected projects. | Sponsor UI, O7 | Hide the action; count only approved, non-cancelled projects. |
| G-16 | Low | Approving an assignment Ops already approved returns "must be recommended by the sponsor first". | O3 | Say it's already approved. |
| G-17 | Low | Executive, Hiring Manager, and zero-role users are blocked from `/community` in the UI, but the API lets them read and post. | Route crawl vs. API | Align: either allow in UI or gate the API. |
| G-18 | Low | The demo seed sends the Ops notification "New project pending approval" to Sam Sponsor. | Sponsor inbox | Fix the seed recipient. |
| G-19 | Low | Every page load for a user without a photo logs a failed `GET /api/me/avatar` (404) to the console. | Console on all personas | Return 204, or skip the request when no photo exists. |

## Not verifiable locally

The local demo uses EF Core's in-memory provider. These behaviours exist only in SQL Server and
were **not** exercised. Run `scripts/run-local-full.sh` against a SQL container, or rely on
`SqlServerOnlyBehaviorTests` in the `integration-real-sql` CI job:

- The filtered unique index enforcing one live assignment per candidate (X4); the app-level
  check did work.
- Serializable-transaction behaviour under concurrent approvals and requests (X9).
- `vCandidateRisk` (risk register, performance and engagement risk counts, O5, X6) and
  `vProjectDeliveryKpi` (Executive delivery tiles, E1). Both return empty in-memory.
- Temporal-table row history.
- Service Bus, email, SharePoint folder provisioning, and Blob Storage. These run through
  in-process local stand-ins.

## Changes made to enable the test run

- `c932d8e` adds persona login for local development. The API honours `X-Dev-Persona` only
  when the environment is Development **and** `Auth:UseDevPersonas` is set, and refuses to
  start if the flag appears anywhere else (`DevPersonaAuthTests`). The SPA picker is compiled
  out of production builds; `dist/` was checked for persona code after `npm run build`.
- Seeded demo users now have fixed object IDs so personas sign in as them.
