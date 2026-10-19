# Multi-role user test scenarios

Manual and scripted scenarios covering every feature for each LaunchPad role, including flows
that need several roles at once. Results from the first run are in
[user-test-findings.md](user-test-findings.md).

## How to run

1. Start the API with the `LocalDemo` profile (it sets `Auth__UseDevPersonas=true`) and the web
   app with `VITE_DEV_PERSONAS=true VITE_MOCK_MODE=false`. In the Claude desktop app, use the
   `launchpad-api` and `launchpad-web-personas` entries in `.claude/launch.json`.
2. Open one browser tab per persona. Each tab shows the persona picker, and the choice is kept
   per tab (sessionStorage), so a Sponsor tab and an Ops tab can run side by side.
3. Use **Switch persona** in the header to change who a tab is signed in as.
4. For API-level checks, send `X-Dev-Persona: <key>` to `http://localhost:5254`. The API only
   honours this header in Development with `Auth:UseDevPersonas` set.

The in-memory database resets on every API restart.

### Personas

| Key | Roles | Starting state |
|---|---|---|
| `ops` | ProgramOps | — |
| `exec` | Executive | — |
| `hm` | HiringManager | — |
| `sponsor` | Sponsor | Sam Sponsor: owns *Internal Dashboard Revamp* (active assignment for Casey), *Design System Refresh*, *Mentorship Program Refresh* (draft) |
| `sponsor2` | Sponsor | Priya Shah: owns *Customer Insights AI Copilot*, *API Gateway Migration* (pending), *Cloud Cost Optimization* (fall cohort) |
| `sponsor-new` | Sponsor | No sponsor profile |
| `cand1` | Candidate | Jordan Rivera, spring cohort: proposed for *Design System Refresh* |
| `cand2` | Candidate | Casey Kim, spring cohort: active on *Internal Dashboard Revamp* |
| `cand3` | Candidate | Morgan Lee, spring cohort |
| `cand-c2` | Candidate | Alex Torres, fall cohort |
| `cand-new` | Candidate | No candidate profile |
| `ops-sponsor` | ProgramOps + Sponsor | No sponsor profile |
| `norole` | *(none)* | Authenticated, no app roles |

### Checks that apply to every scenario

- **Redaction:** no response to a Sponsor, Candidate, or Hiring Manager contains
  `averageScore`, `overallScore`, `hasPerformanceRisk`, `hasEngagementRisk`, or
  `suggestedHireOutcome`. Inspect the network panel, not just the screen.
- **Authorization:** hidden navigation is not a control. Anything a role can't reach in the UI
  is also tried as a direct URL and as a direct API call.
- **Console:** no uncaught errors or React warnings.

---

## Candidate

| ID | Scenario | Personas | Steps | Expected |
|---|---|---|---|---|
| C1 | Onboarding | `cand-new` | Open app → onboarding → location, availability, skills → submit. Retry submit. | Profile created in the single active cohort; second submit refused (409). If zero or several cohorts are active, a clear message to contact Program Ops. |
| C2 | Dashboard and journey trail | `cand1`, `cand2` | Open `/dashboard`. | Current assignment, task progress, community activity. No review or risk numbers. |
| C3 | Profile and photo | `cand1` | Edit bio, school, GPA (try 9.5), links. Upload, crop, delete a photo. Upload a non-image. | Valid edits saved; GPA out of range rejected; non-image rejected; photo removal falls back to initials. |
| C4 | Marketplace | `cand1` | Browse `/marketplace`, search, open a project, rate interest 1–5, re-rate. | Only Open + Approved projects in the candidate's own cohort. Re-rating updates, doesn't duplicate. |
| C5 | Assignments | `cand1`, `cand2` | Open `/assignments`. | Shows the live assignment with sponsor and project details. |
| C6 | Tasks | `cand2` | Toggle a sponsor to-do complete and back. | Status persists; sponsor sees the change. |
| C7 | Deliverables | `cand2` | Upload a PDF linked to a to-do; upload an oversized file; upload an `.exe`; download own file. | PDF accepted and downloadable byte-for-byte. Oversize rejected. Executable content rejected. |
| C8 | Evaluations | `cand2` | Open `/evaluations` after a sponsor review. | Strengths, growth areas, recommendation visible. **No numeric ratings in UI or JSON.** |
| C9 | Community | `cand1` | Post (each type), attach image, comment, like, filter by hashtag; post 11 times in a minute; post a body containing HTML. | Posts render; rate limit returns 429 after 10; HTML shown as text; role label can't be spoofed via `activeRole`. |
| C10 | Notifications | `cand1` | Open bell, mark one read, mark all read. | Unread count updates. |
| C11 | Authorization negatives | `cand1` | Visit `/ops/*`, `/exec`, `/projects`, `/pipeline`. Call `/api/candidates`, `/api/ops/risks`, `/api/projects/{id}/matches`. | UI redirects to Not authorized; API returns 403. |
| C12 | Cohort isolation | `cand-c2` | Open `/marketplace`. Call `open-detail` and `interest` for a spring-cohort project id. | Only fall-cohort projects; other cohort's ids return 404. |

## Sponsor

| ID | Scenario | Personas | Steps | Expected |
|---|---|---|---|---|
| S1 | Onboarding | `sponsor-new` | Complete sponsor profile; submit twice. | Profile created; second submit 409. Sponsor-only pages blocked until then. |
| S2 | Create and submit project | `sponsor` | Create a project (name, description, skills, max candidates); try end before start, max 0, unknown cohort; edit; submit. | Draft → Pending review. Invalid input rejected with field errors. |
| S3 | Review matches | `sponsor` | `/projects/{id}/matches` → recommend one, reject another; recommend twice. | Proposed → SponsorApproved / Withdrawn; second recommend 400; siblings withdrawn only once the project is full. |
| S4 | Request a specific candidate | `sponsor` | From eligible candidates, request one; request again; request for a full project. | Creates a SponsorApproved assignment in the Ops queue; duplicates and full projects refused. |
| S5 | Manage to-dos | `sponsor` | Add to-dos with priority and due date; view candidate's progress. | Candidate sees them; past due dates flagged. |
| S6 | My Candidates | `sponsor` | Open `/candidates`, open a candidate profile. | Only candidates on own projects. **No scores or risk flags in JSON.** |
| S7 | Submit reviews | `sponsor` | Submit midpoint then final for an active assignment; resubmit; submit for a non-active assignment; out-of-range ratings. | Accepted once per checkpoint (409 on repeat); non-active 400; ratings outside 1–5 rejected; linked to-do auto-completes. |
| S8 | Delivery stage and cancel | `sponsor` | Advance delivery stage; try moving it backward; cancel a project with a reason. | Forward only; cancel withdraws open assignments and leaves the Ops approval queue. |
| S9 | Ownership negatives | `sponsor2` | Against Sam's project and assignment: edit, cancel, submit, recommend, add to-do, list reviews, download a deliverable, submit a review. UI and API. | All 403. |
| S10 | Search and pipeline | `sponsor`, `sponsor-new` | Global search; `/pipeline`. | Results limited to what the role may see. |

## Program Ops

| ID | Scenario | Personas | Steps | Expected |
|---|---|---|---|---|
| O1 | Cohorts | `ops` | Create a cohort (bad dates, duplicate name); change status; schedule midpoint reviews twice. | Validation on dates; status change audited; scheduling is idempotent and skips reviews already submitted. |
| O2 | Project approvals | `ops` | Approve one pending project; reject one with and without a reason; try to approve a cancelled project. | Reason required; sponsor notified; cancelled projects not in the queue and not approvable. |
| O3 | Matching queue | `ops` | Run matching; approve and deny sponsor-recommended assignments; deny one that is already approved. | Approve respects one-live-assignment and project capacity; deny only from the queue state. |
| O4 | Talent pipeline | `ops` | Filter by cohort; open a candidate; set status Hire / Talent Plus / No Hire. | Scores and risk flags visible; status change audited. |
| O5 | Risks | `ops` | `/ops/risks`. | Risk register matches `vCandidateRisk` (SQL only). |
| O6 | Skills | `ops` | Add a skill; add the same name in different case; delete an unused skill; delete one in use. | Case-insensitive de-dupe; in-use delete refused (409). |
| O7 | Dashboard | `ops` | `/ops/dashboard`. | Counts agree with the underlying lists. |
| O8 | Projects catalog | `ops` | `/ops/projects`, open, edit, change delivery stage. | Edits saved and audited. |
| O9 | Help center | `ops` | Search guides; open a topic by URL. | Filter works; unknown topic handled. |

## Executive

| ID | Scenario | Personas | Steps | Expected |
|---|---|---|---|---|
| E1 | Executive dashboard | `exec` | `/exec`, switch cohorts. | Funnel, KPIs, and university breakdown per cohort. |
| E2 | Read-mostly access | `exec` | Visit ops routes; call approve, deny, cohort status, skills delete. | Ops-only actions return 403. |
| E3 | Pipeline with scores | `exec` | `/pipeline`. | Scores and risk flags visible. |

## Hiring Manager

| ID | Scenario | Personas | Steps | Expected |
|---|---|---|---|---|
| H1 | Pipeline only | `hm` | `/pipeline`, candidate detail; try ops, exec, sponsor, candidate routes and APIs. | Pipeline visible **without scores**; everything else blocked. |

## Multi-role and edge cases

| ID | Scenario | Personas | Steps | Expected |
|---|---|---|---|---|
| M1 | Role switcher | `ops-sponsor` | Switch between Program Ops and Sponsor in the header. | Menu and home page follow the active role. |
| M2 | No roles | `norole` | Open any page; call any API. | Not authorized page; role-gated APIs 403. |
| M3 | Persona switch | any | Switch persona in a tab with data loaded. | Full reload; nothing from the previous identity remains on screen. |
| M4 | Unknown URL | any | Visit `/does-not-exist`. | "Page not found" with a link home. |

## Cross-role flows (run in parallel tabs)

| ID | Flow | Tabs | Steps | Expected |
|---|---|---|---|---|
| X1 | Project lifecycle | `sponsor`, `ops`, `cand1`, `cand-c2` | Sponsor creates and submits → Ops rejects with reason → Sponsor sees reason and notification, edits, resubmits → Ops approves → `cand1` sees it in the marketplace; `cand-c2` does not. | Every hop visible in the other tab after refresh; notifications at each decision. |
| X2 | Match to assignment | `cand1`, `sponsor`, `ops` | Candidate rates interest → Ops runs matching → Sponsor recommends → Ops approves. | Candidate's assignment moves Proposed → OpsApproved → Active; candidate notified at each step. |
| X3 | Direct request | `sponsor`, `ops`, `cand3` | Sponsor requests Morgan → Ops denies → Sponsor re-requests → Ops approves. | Deny returns the spot; re-request allowed. |
| X4 | One live assignment | `sponsor`, `sponsor2`, `ops` | Both sponsors get the same candidate to SponsorApproved; Ops approves both. | Second approve 409. *SQL only for the filtered unique index.* |
| X5 | Work delivery | `sponsor`, `cand2` | Sponsor adds to-do → candidate completes it and uploads a deliverable → sponsor downloads. | Same bytes; `sponsor2` can't download. |
| X6 | Reviews and risk | `ops`, `sponsor`, `cand2`, `exec` | Ops schedules midpoint → sponsor submits a low review, then a lower final → Ops risks and Exec dashboard show performance risk → candidate and sponsor payloads stay redacted. | *Risk view is SQL only.* |
| X7 | Community | `cand1`, `sponsor`, `cand2` | Candidate posts → sponsor likes → Casey comments. | Counts update; author notified. |
| X8 | Cohort closed mid-flow | `ops`, `cand-c2`, `sponsor2` | Ops marks fall cohort Completed. | Marketplace, interest, project creation, and matching for that cohort stop. |
| X9 | Concurrent decisions | two `ops` tabs, `sponsor`, `sponsor2` | Approve and deny the same assignment at once; two sponsors request one candidate at once. | Exactly one decision wins. *SQL only.* |
| X10 | Status change during assignment | `ops`, `sponsor`, `cand2` | Ops sets Casey to No Hire while her assignment is active. | Defined outcome for the live assignment. |
