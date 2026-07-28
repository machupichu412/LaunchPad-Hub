# CLAUDE.md

Guidance for Claude Code (and any other Claude instance) working in this repository.

## What this is

LaunchPad is being rebuilt from scratch as a first-party internal web application — **React SPA + ASP.NET Core API + Azure SQL, secured with Microsoft Entra ID** — replacing an existing Power Platform (Power Apps / Dataverse) implementation. Functional parity with the current app is the floor; the rebuild exists to reach headroom the Power Platform build couldn't (deeper role security, real async processing, AI-assisted matching/parsing).

~2,000 named users, <200 concurrent, internal-only, confidential HR-adjacent data (ratings, hire decisions). These constraints (small scale, high sensitivity) are why the architecture favors managed PaaS + policy-based authorization over anything heavier.

The authoritative spec for all of this is **`launchpad-build-guide.md`** in the repo root — when in doubt, that document wins over this summary. This file exists to keep the guide's decisions front-of-mind during day-to-day coding, not to replace it.

## Repo layout

```
launchpad/
├── src/
│   ├── LaunchPad.Api/            # ASP.NET Core Web API — thin controllers only
│   ├── LaunchPad.Application/    # use cases, DTOs, validators, interfaces
│   │   ├── Candidates/ Projects/ Assignments/ Matching/ Reviews/ Reporting/
│   ├── LaunchPad.Domain/         # entities, value objects, domain rules
│   ├── LaunchPad.Infrastructure/ # EF Core, Blob, Service Bus, OpenAI clients
│   ├── LaunchPad.Functions/      # isolated-worker Azure Functions
│   └── LaunchPad.Web/            # React + TypeScript + Vite
│       └── src/ auth/ features/{candidate,sponsor,ops,exec}/ components/ api/
├── tests/
│   ├── LaunchPad.Domain.Tests/
│   ├── LaunchPad.Application.Tests/
│   └── LaunchPad.Api.IntegrationTests/
├── infra/                        # Bicep IaC (main.bicep + modules/)
└── .github/workflows/
```

**Layering rule (do not violate):** `Domain` has zero dependencies. `Application` depends only on `Domain`. `Infrastructure` and `Api` depend inward. Matching logic and scoring rules must stay unit-testable without a database — if a change to `Application` code requires spinning up EF Core or SQL to test it, that's a layering violation.

## Data model essentials

- **Program → Cohort → Candidate / Sponsor / Project.** Many cohorts over time; a candidate/sponsor/project always belongs to one cohort.
- **Identity:** `AppUser.EntraObjectId` is the only identity anchor. Never build a locally-managed password or a parallel user list — JIT-provision the `AppUser` row on first authenticated request (upsert on `EntraObjectId`).
- **Skills are normalized:** `Skill` + `CandidateSkill` (with `Proficiency`, `Source`: SelfReported/ResumeParsed/OpsVerified) + `ProjectSkill` (with `IsRequired`). Do not go back to free-text/multi-select skill fields — that's exactly the pain this rebuild is fixing.
- **`Assignment` is the stateful join** between Project and Candidate: `Status` (Proposed → SponsorApproved → OpsApproved → Active → Completed/Withdrawn), `MatchScore`, `MatchRationale`. A unique filtered index enforces one active assignment (`Status IN (2,3)`) per candidate — don't bypass this with raw inserts.
- **Ratings live in one flexible `Review` table** (`ReviewType`, `Checkpoint`: Midpoint/Final) with a computed, persisted `OverallScore`. Don't add new rating columns elsewhere — extend this table so scoring criteria can change without schema churn.
- **Risk signals (`vCandidateRisk`) are a computed view, not a stored status.** Performance risk = low average score or a downward mid→final trajectory. Engagement risk = stale to-dos or inactivity. The API and Power BI must read the same view — never duplicate this logic in application code.
- **Audit:** SQL temporal tables (`SYSTEM_VERSIONING`) capture row history automatically; a separate application-level `AuditEvent` table captures actor intent (who did what and why) for every approval, status change, and score recalculation. Both are required — temporal tables alone don't tell you *why*.

## The one security control that matters most

**Hidden numeric ratings must never reach a Sponsor or Candidate — not in the UI, not in the JSON payload.** Redaction happens server-side in the DTO mapper (additive: only populate `AverageScore`/risk flags for Executive/ProgramOps roles), never client-side. Every endpoint returning a `CandidateDto` needs an integration test per role asserting the serialized response contains no score field for unauthorized roles. If you're ever tempted to filter scores out in the React layer instead of the mapper, stop — that's not a control.

## Authorization model

- **Roles ride in the Entra token** (`roles` claim from app-role assignment), not raw Entra groups — group claims overflow past ~150 memberships and force a Graph lookup per request. Five roles: Executive, ProgramOps, Sponsor, Candidate, HiringManager.
- **Groups are assigned to app roles**, never individuals assigned directly — onboarding/offboarding a cohort should be a group-membership operation.
- **Role alone is not enough.** "Sponsor can manage their own project" needs resource-based authorization (`IAuthorizationHandler` + `OwnsProjectRequirement`/`OwnsCandidateProfileRequirement`), not just `[Authorize(Roles = "Sponsor")]`.
- **Fail closed:** global `FallbackPolicy` requires an authenticated user by default; "Assignment required = Yes" on the Enterprise App means zero-role tokens can't sneak through a policy that was accidentally left off an endpoint.
- **Client-side route guards (`RequireRole` in React) are navigation UX only.** They shape the menu. The API independently re-checks every request — never treat a frontend guard as a security boundary.
- SQL Row-Level Security (`SESSION_CONTEXT`-based predicate) is available as defense-in-depth but is a second net; application-layer authorization stays the authoritative control because it gives better error semantics (403 vs. a silently empty result set).

## Async work

Anything that runs longer than a request should be a Service Bus message consumed by an Azure Function, not inline in a controller: cohort-wide matching, resume parsing (Azure OpenAI), nightly risk recalculation, notification digests, sponsor auto-flag evaluation. If you find yourself adding a `Task.Run` or a long loop inside an API controller for one of these, move it to `LaunchPad.Functions` instead.

## Conventions

- **Secrets:** managed identity + Key Vault everywhere. No connection strings or API keys in `appsettings.json` — this is the single biggest security upgrade over the old connector-based Power Platform auth, don't regress it.
- **Migrations** run under a dedicated deployment identity with DDL rights; the runtime app identity gets `db_datareader`/`db_datawriter`/`EXECUTE` only, never schema-modify rights.
- **Environments** (Dev/Test/Prod) each have their own Entra app registrations. Never reuse a registration or client ID across environments.
- **IaC** is Bicep (`infra/main.bicep` + `infra/modules/`) — infra changes go through the same PR review as code.
- **CI/CD:** GitHub Actions, OIDC federated credentials (no stored Azure secrets), infra deploys before app deploy, deploy-to-staging-slot → smoke test → slot swap for Prod. PR gates: build + tests green, lint clean, CodeQL, and the per-role authorization integration suite.
- **Frontend:** Fluent UI v9, TanStack Query for server state, MSAL for auth with a single `authedFetch` interceptor attaching the bearer token (no call site should acquire tokens itself), API client generated from the OpenAPI doc so DTOs can't drift from the backend.
- **Observability:** every log line and trace should carry `EntraObjectId`, `Role`, and `CorrelationId`. Health checks live at `/healthz` (liveness) and `/healthz/ready` (SQL + Service Bus reachability).

## Build sequence (context on what "done" looks like at each stage)

The guide phases the build: Foundation (IaC + auth skeleton) → Data & identity → Core CRUD → Matching & approvals → Reviews & risk → Dashboards & AI → Hardening & launch, ~15 weeks total. When picking up work, check which phase is active — e.g. don't wire up Azure OpenAI resume parsing (Phase 5) before the core Assignment/approval workflow (Phase 3) exists, and don't skip the per-role integration test suite that's supposed to land in Phase 1.

## Migration from the current Power Platform build

This is a parallel-run migration (both systems live for one cohort), not a cutover. The trickiest step in practice is identity mapping — resolving every existing Dataverse/Power Apps user to an Entra object ID — so start that early. Skills need de-duplication of free-text variants (e.g. "Power BI" / "PowerBI" / "power-bi") into the new normalized `Skill` taxonomy before import. Preserve original `SubmittedUtc` timestamps on migrated reviews so mid→final trajectory analysis stays valid.

## Things not to do

- Don't add a field to `Projects`/`Assignment` to track completion/status redundantly — status is already modeled explicitly on `Assignment.Status`; derive views from it rather than duplicating state.
- Don't denormalize the relational model to fit a document store — Azure SQL was chosen specifically because cohorts/assignments/ratings/skills are joins and reporting is set-based aggregation.
- Don't ship a numeric score or risk flag to a DTO by default — the redaction pattern is additive (opt-in per role), never subtractive (opt-out/strip).
- Don't put long-running work on the request thread — matching, parsing, and recalculation belong in Functions.
