# Multi-role user test findings — 2026-09-17

Run of [user-test-scenarios.md](user-test-scenarios.md) against the local demo (in-memory database)
with persona login, on branch `test/user-scenarios`.

**Summary:** 8 bugs found and fixed, plus all 19 logic gaps acted on in a follow-up pass — 27
changes in total, each with a regression test. The core security control held throughout: no
Sponsor, Candidate, or Hiring Manager response contained a review score, risk flag, or hire
suggestion, and the match score has since joined them behind the same rule.

## How it was run

- **Browser:** separate tabs signed in as different personas at the same time. The project
  lifecycle (X1) was driven through the UI with Sponsor and Ops tabs side by side. A scripted
  crawl loaded all 29 routes as 9 personas (261 page loads), recording redirects, error text,
  and console errors.
- **API:** the same running server, called directly with `X-Dev-Persona`. It covered a
  30-endpoint × 10-persona authorization matrix with a scan for score fields in every
  response, plus the write paths of every cross-role flow.
- **Not run locally:** anything that depends on SQL Server. No local SQL instance was available
  (Docker not running), so those scenarios were listed as unverified rather than reported as
  passing. Most were picked up afterwards by a SQLite harness — see
  [Covered afterwards on SQLite](#covered-afterwards-on-sqlite) and what it still does not reach.

## Results by scenario

| Area | Scenarios | Result |
|---|---|---|
| Candidate | C1–C12 | Pass (found F-01, F-07, G-02, G-03, G-09, G-12) |
| Sponsor | S1–S10 | Pass (found F-04, F-06, G-08, G-10, G-11) |
| Program Ops | O1–O9 | Pass (found F-02, F-03, F-05, G-04, G-14, G-15, G-16). O5 risk signals covered on SQLite |
| Executive | E1–E3 | Pass (found G-07). KPI tiles covered on SQLite |
| Hiring Manager | H1 | Pass (no scores; all non-pipeline routes and APIs blocked) |
| Multi-role / edge | M1–M4 | Pass (found F-08, G-06) |
| X1 Project lifecycle | UI, 4 tabs | Pass (found F-03) |
| X2 Match → assignment | API | Was blocked after OpsApproved (G-01); the flow now runs end to end |
| X3 Direct request | API | Pass (found G-05) |
| X4 One live assignment | API | Pass. App-level check, and the filtered unique index itself on SQLite |
| X5 Work delivery | API | Pass (byte-identical download, other sponsor 403; found G-02, G-11) |
| X6 Reviews and risk | API | Pass after F-06, F-07; the risk view itself now runs on SQLite |
| X7 Community | API + UI | Pass (rate limit 429 after 10, role label can't be spoofed, HTML rendered as text; found G-05, G-12, G-17) |
| X8 Cohort closed | API | Failed (G-04); passes after the fix |
| X9 Concurrency | API | Pass on SQLite — two simultaneous approvals leave one live assignment |
| X10 Status change during assignment | API | Was undefined (G-13); the no-op is now deliberate and documented |

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

Regression tests for the findings and the gaps live in
`tests/LaunchPad.Api.IntegrationTests/UserScenarioRegressionTests.cs`, with the date and file-type
rules unit-tested in `tests/LaunchPad.Application.Tests/` (`AssignmentLifecycleRunnerTests`,
`FileSignaturesTests`) and `src/LaunchPad.Web/src/features/shared/NotFound.test.tsx` for the
frontend. Each API test for F-01 to F-07 was run against the unfixed controllers and failed
before it was accepted.

## Logic gaps — all fixed in a follow-up pass

Originally reported for a product decision. The three that changed behaviour the build guide
described were decided by the user: assignments activate on a **nightly job driven by project
dates** (G-01), Executive **reads everything and changes nothing** (G-07), and candidates and
sponsors **see the match rationale, not the number** (G-09). The guide has been updated for the
last two.

| ID | Severity | Gap | Fix | Commit |
|---|---|---|---|---|
| G-01 | **High** | Nothing moved an assignment from OpsApproved to Active or Active to Completed, so no assignment created in the app could ever be reviewed. | A nightly Function activates approved assignments once the project's start date arrives and completes active ones after its end date, with the date rules unit-tested in `AssignmentLifecycleRunner`. Ops can set either status by hand. Both paths are audited and notify the candidate and sponsor. | `ca438b3` |
| G-02 | **High** | Deliverables accepted any file (an `.exe`, HTML named `fake.pdf`); avatars and community images trusted the declared type. | Extension allow-list for deliverables, and all three paths read the file header and check it against the claimed type before storing. | `890dbe1` |
| G-03 | Medium | Onboarding needed exactly one Active cohort, and new cohorts were created Active. | Cohorts are created Planned; onboarding joins the cohort whose dates cover today, and only a genuine overlap asks Ops. | `a15d0de` |
| G-04 | Medium | Completing a cohort changed nothing. | A completed cohort closes its marketplace, project creation and matching. Unknown cohort ids 404 instead of reporting success. | `a15d0de` |
| G-05 | Medium | Notifications stopped after the first proposal. | Sponsor recommend and reject, direct request, Ops approve and deny, assignment start, and community comments all notify the people they affect. | `d9c30ef` |
| G-06 | Medium | A ProgramOps+Sponsor user could file a sponsor's review on any project. | Sponsor reviews require `ProjectOwnerOnly`, which no role bypasses. | `dc8e80d` |
| G-07 | Medium | Executive could edit and cancel any project and add to-dos. | Reads keep the old rule; writes use `ChangeOwnProject` / `ChangeOwnAssignment` (owner plus Ops). | `dc8e80d` |
| G-08 | Medium | Every sponsor could read every candidate in every cohort. | Sponsors see candidates only from cohorts they have a project in; a sponsor with no profile sees none. Ops, Exec and Hiring Manager are unchanged. | `dc8e80d` |
| G-09 | Low | The numeric match score reached candidates and sponsors. | They get the rationale and the ranking; Ops and Exec keep the number. | `d389e85` |
| G-10 | Low | An unknown skill name silently created a global "Uncategorized" skill. | Names resolve against the taxonomy; unknown ones are rejected by name. | `d389e85` |
| G-11 | Low | Sponsors could upload deliverables; to-dos could be added to dead assignments, with past due dates. | Deliverables come from the candidate (Ops retains access); to-dos need a live assignment and a due date that isn't in the past. | `2e97dd0` |
| G-12 | Low | Anyone could publish an Announcement. | Announcements are Ops-only. | `dc8e80d` |
| G-13 | Low | A No Hire decision left a live assignment untouched, undefined. | Kept, and now written down as deliberate: conversion is decided at the end of the program, and Ops ends an assignment explicitly when that is what they mean. | `2e97dd0` |
| G-14 | Low | Duplicate cohort names were allowed. | A cohort name must be unique within its program. | `a15d0de` |
| G-15 | Low | "Review matches" appeared on drafts; the Ops active-project count included drafts and rejections. | The action is hidden until Ops approves the project; the count is approved, non-cancelled projects only. | `2e97dd0` |
| G-16 | Low | Re-approving an approved match asked for a sponsor recommendation that had already happened. | The message now names the actual state. | `2e97dd0` |
| G-17 | Low | The community API accepted readers the SPA kept out. | The API matches the SPA's route guard: Candidate, Sponsor, ProgramOps. | `dc8e80d` |
| G-18 | Low | The demo seed sent an Ops notification to a sponsor. | Re-subjected as the draft reminder its body always was. | `a15d0de` |
| G-19 | Low | Users without a photo logged a 404 on every page load. | `GET /api/me/avatar` returns 204 for "no photo". | `2e97dd0` |

Two smaller defects were fixed in passing: creating a cohort with no program in the database was
an unhandled 500, and the upload tests were posting bytes that were never valid files.

## Covered afterwards on SQLite

Most of what the in-memory run couldn't reach turned out not to need SQL Server — it needed a
*relational* database. `tests/LaunchPad.Api.IntegrationTests/Sqlite/` runs the same API host on a
throwaway SQLite file with both views created and the production indexes carried over, so these
now run on every `dotnet test`:

| Scenario | What the SQLite run proves |
|---|---|
| X4 | The filtered unique index refuses a second live assignment and allows a Proposed one, and Ops approve returns 409 before the index is reached. |
| X9 | Two simultaneous Ops approvals of the same candidate leave exactly one live assignment. |
| X6 / O5 | `vCandidateRisk` flags a 4.0 → 2.0 score drop as performance risk and three overdue to-dos as engagement risk — read through the real `CandidateRepository`, with the sponsor's copy of the same candidate carrying no score or flag. |
| E1 | `vProjectDeliveryKpi` feeds the Executive tiles and excludes cancelled projects. |

This is a stand-in for SQL Server, not a substitute. It does **not** check the shipped T-SQL:
the views are hand-ported to SQLite in `SqliteSchema`, so a mistake in the migration's view text
would still pass here. Keep the two in step by hand when either changes.

## Still not verifiable locally

- The **T-SQL view definitions** themselves, and SQL Server's own error numbers and locking
  semantics. `SqlServerOnlyBehaviorTests` covers these in the `integration-real-sql` CI job, or
  run `scripts/run-local-full.sh` against a SQL container.
- **Optimistic concurrency.** `rowversion` has no SQLite equivalent, so `SqliteModelCustomizer`
  drops the concurrency token; the X9 invariant above holds through the index and the
  serializable transaction, not through a version check.
- **Temporal-table row history.**
- **`GET /api/ops/risks`** orders by a decimal, which SQLite refuses to translate. Fine on SQL
  Server — the same view data is asserted through `GET /api/candidates/{id}` instead.
- Service Bus, email, SharePoint folder provisioning, and Blob Storage. These run through
  in-process local stand-ins.

## Follow-up left open

- `GetOrCreateByNamesAsync` is gone from `ISkillRepository`; if a bulk import needs to create
  skills from names, it should go through the category-carrying `CreateAsync`.

## Changes made to enable the test run

- `c932d8e` adds persona login for local development. The API honours `X-Dev-Persona` only
  when the environment is Development **and** `Auth:UseDevPersonas` is set, and refuses to
  start if the flag appears anywhere else (`DevPersonaAuthTests`). The SPA picker is compiled
  out of production builds; `dist/` was checked for persona code after `npm run build`.
- Seeded demo users now have fixed object IDs so personas sign in as them.
