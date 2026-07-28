# LaunchPad Platform — Scratch Build Guide
### React + ASP.NET Core + Azure SQL, secured with Microsoft Entra ID

**Version:** 1.0
**Audience:** Engineering team rebuilding the LaunchPad management application as a first-party internal web application (non-Power Platform).
**Scope:** Application architecture, data model, role security, required Azure services, hosting topology, CI/CD, and a phased build sequence.

---

## 1. Scope & Design Assumptions

This guide assumes functional parity with the existing LaunchPad application, plus the headroom the Power Platform build could not reach.

**Carried forward from the current solution:**

| Capability | Requirement |
|---|---|
| Program structure | Program → Cohort → Candidate / Sponsor / Project, many cohorts over time |
| Matching | Rules-based eligibility gates + weighted scoring, top-3 matches surfaced with rationale |
| Approvals | Two-stage: Sponsor recommends → Program Ops approves |
| Ratings | Hidden numeric scores; only derived outcomes (Hire / Talent Plus / No Hire) are user-visible |
| Risk signals | Performance risk (low avg score, downward mid→final trajectory) and Engagement risk (inactivity, stale to-dos) |
| Artifacts | Resumes, portfolios, deliverables, recordings |
| Role views | Executive, Admin / Program Ops, Sponsor / Project Lead, Candidate, Hiring Manager (read-only) |
| Resume parsing | Structured extraction into candidate profile fields |

**Non-functional targets (drive the architecture choices below):**

- ~2,000 named users, <200 concurrent — small by Azure standards, so favor managed PaaS over Kubernetes.
- Internal-only. No anonymous access. Corporate network or Entra-authenticated egress.
- Confidential HR-adjacent data (ratings, hire decisions) — requires field-level authorization and full audit trail.
- Multi-environment: Dev / Test / Prod with independent data.

---

## 2. Target Architecture

```
                        ┌──────────────────────────────┐
                        │      Microsoft Entra ID      │
                        │  App Registrations (SPA+API) │
                        │  App Roles + Security Groups │
                        └───────┬──────────────┬───────┘
                                │ id/access    │ token
                                │ tokens       │ validation
                ┌───────────────▼──────┐   ┌───▼──────────────────────┐
  Browser ─────▶│  Azure Static Web     │──▶│  Azure App Service       │
                │  Apps (React SPA)     │   │  ASP.NET Core Web API    │
                │  Fluent UI v9         │   │  (.NET 9, Linux)         │
                └───────────────────────┘   └───┬──────────────────────┘
                                                │ Managed Identity
             ┌──────────────────────────────────┼──────────────────────────────┐
             │                │                 │              │               │
      ┌──────▼──────┐  ┌──────▼──────┐   ┌──────▼──────┐ ┌─────▼─────┐  ┌──────▼──────┐
      │ Azure SQL   │  │ Blob Storage│   │ Service Bus │ │ Key Vault │  │ Azure OpenAI│
      │ (system of  │  │ (resumes,   │   │ (async jobs)│ │ (secrets, │  │ + AI Search │
      │  record)    │  │ deliverables│   └──────┬──────┘ │  certs)   │  │ (matching,  │
      └─────────────┘  │  recordings)│          │        └───────────┘  │  parsing)   │
                       └─────────────┘   ┌──────▼──────┐                └─────────────┘
                                         │Azure Functions│
                                         │ (matching,    │
                                         │ resume parse, │
                                         │ risk scoring, │
                                         │ digests)      │
                                         └───────────────┘
                                                │
                                    ┌───────────▼───────────┐
                                    │ Application Insights  │
                                    │ + Log Analytics       │
                                    └───────────────────────┘
```

**Why this shape:**

- **SPA + API split** rather than server-rendered MVC. Clean token boundary, and the API is reusable by Functions, Power BI, and any future Teams app.
- **Azure SQL over Cosmos DB.** LaunchPad is inherently relational — cohorts, assignments, ratings, and skills are all joins. Reporting queries (funnel conversion, sponsor risk) are set-based aggregations. Cosmos would force you to denormalize the exact data you need to aggregate.
- **Functions for long-running work.** Matching across a full cohort, resume parsing, and nightly risk recalculation should never run inside a request thread.
- **Static Web Apps for the frontend.** Free/cheap tier, global CDN, native Entra auth integration, and PR preview environments out of the box.

---

## 3. Repository & Solution Layout

Single repo, three deployable units.

```
launchpad/
├── src/
│   ├── LaunchPad.Api/                 # ASP.NET Core Web API — thin controllers
│   │   ├── Controllers/
│   │   ├── Authorization/             # policies, handlers, requirements
│   │   ├── Middleware/                # correlation ID, exception handling
│   │   └── Program.cs
│   ├── LaunchPad.Application/         # use cases, DTOs, validators, interfaces
│   │   ├── Candidates/
│   │   ├── Projects/
│   │   ├── Assignments/
│   │   ├── Matching/
│   │   ├── Reviews/
│   │   └── Reporting/
│   ├── LaunchPad.Domain/              # entities, value objects, domain rules
│   ├── LaunchPad.Infrastructure/      # EF Core, Blob, Service Bus, OpenAI clients
│   ├── LaunchPad.Functions/           # isolated-worker Azure Functions
│   └── LaunchPad.Web/                 # React + TypeScript + Vite
│       ├── src/
│       │   ├── auth/                  # MSAL config, role guards
│       │   ├── features/              # candidate/, sponsor/, ops/, exec/
│       │   ├── components/
│       │   └── api/                   # generated client from OpenAPI
├── tests/
│   ├── LaunchPad.Domain.Tests/
│   ├── LaunchPad.Application.Tests/
│   └── LaunchPad.Api.IntegrationTests/
├── infra/                             # Bicep IaC
│   ├── main.bicep
│   └── modules/
└── .github/workflows/                 # or azure-pipelines/
```

**Layering rule:** `Domain` has zero dependencies. `Application` depends on `Domain` only. `Infrastructure` and `Api` depend inward. This keeps matching logic and scoring rules unit-testable without a database.

---

## 4. Data Model (Azure SQL)

### 4.1 Core tables

```sql
-- Program structure
CREATE TABLE dbo.Program (
    ProgramId       INT IDENTITY PRIMARY KEY,
    Name            NVARCHAR(200)   NOT NULL,
    Description     NVARCHAR(MAX)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1
);

CREATE TABLE dbo.Cohort (
    CohortId        INT IDENTITY PRIMARY KEY,
    ProgramId       INT             NOT NULL REFERENCES dbo.Program(ProgramId),
    Name            NVARCHAR(100)   NOT NULL,      -- e.g. 'LP-2026-Fall'
    StartDate       DATE            NOT NULL,
    EndDate         DATE            NOT NULL,
    Status          TINYINT         NOT NULL       -- 0 Planned, 1 Active, 2 Completed
);

-- People. Identity is the Entra object ID; never a locally-managed password.
CREATE TABLE dbo.AppUser (
    AppUserId       INT IDENTITY PRIMARY KEY,
    EntraObjectId   UNIQUEIDENTIFIER NOT NULL UNIQUE,
    Upn             NVARCHAR(256)   NOT NULL,
    DisplayName     NVARCHAR(200)   NOT NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedUtc      DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_AppUser_EntraObjectId ON dbo.AppUser(EntraObjectId);

CREATE TABLE dbo.Candidate (
    CandidateId       INT IDENTITY PRIMARY KEY,
    AppUserId         INT           NOT NULL REFERENCES dbo.AppUser(AppUserId),
    CohortId          INT           NOT NULL REFERENCES dbo.Cohort(CohortId),
    Location          NVARCHAR(100) NULL,
    Availability      TINYINT       NOT NULL,      -- 0 PartTime, 1 FullTime
    GraduationDate    DATE          NULL,
    LinkedInUrl       NVARCHAR(500) NULL,
    PortfolioUrl      NVARCHAR(500) NULL,
    ResumeBlobPath    NVARCHAR(500) NULL,
    Status            TINYINT       NOT NULL,      -- 0 InProgress, 1 Hire, 2 TalentPlus, 3 NoHire
    RowVersion        ROWVERSION,
    CONSTRAINT UQ_Candidate_User_Cohort UNIQUE (AppUserId, CohortId)
);

CREATE TABLE dbo.Sponsor (
    SponsorId       INT IDENTITY PRIMARY KEY,
    AppUserId       INT             NOT NULL REFERENCES dbo.AppUser(AppUserId),
    Organization    NVARCHAR(200)   NULL,
    Title           NVARCHAR(200)   NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    RemovalReason   NVARCHAR(500)   NULL
);

CREATE TABLE dbo.Project (
    ProjectId           INT IDENTITY PRIMARY KEY,
    CohortId            INT             NOT NULL REFERENCES dbo.Cohort(CohortId),
    SponsorId           INT             NOT NULL REFERENCES dbo.Sponsor(SponsorId),
    Name                NVARCHAR(300)   NOT NULL,
    Description         NVARCHAR(MAX)   NULL,
    AvailabilityNeeded  TINYINT         NOT NULL,
    StartDate           DATE            NULL,
    EndDate             DATE            NULL,
    ApprovalStatus      TINYINT         NOT NULL,  -- 0 Draft, 1 PendingOps, 2 Approved, 3 Rejected
    Status              TINYINT         NOT NULL,  -- 0 Open, 1 InProgress, 2 Completed, 3 Cancelled
    RowVersion          ROWVERSION
);
```

### 4.2 Skills (normalized — this is where SharePoint/Dataverse got painful)

```sql
CREATE TABLE dbo.Skill (
    SkillId     INT IDENTITY PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL UNIQUE,
    Category    NVARCHAR(100) NULL
);

CREATE TABLE dbo.CandidateSkill (
    CandidateId  INT NOT NULL REFERENCES dbo.Candidate(CandidateId) ON DELETE CASCADE,
    SkillId      INT NOT NULL REFERENCES dbo.Skill(SkillId),
    Proficiency  TINYINT NOT NULL DEFAULT 3,       -- 1-5
    Source       TINYINT NOT NULL DEFAULT 0,       -- 0 SelfReported, 1 ResumeParsed, 2 OpsVerified
    PRIMARY KEY (CandidateId, SkillId)
);

CREATE TABLE dbo.ProjectSkill (
    ProjectId   INT NOT NULL REFERENCES dbo.Project(ProjectId) ON DELETE CASCADE,
    SkillId     INT NOT NULL REFERENCES dbo.Skill(SkillId),
    IsRequired  BIT NOT NULL,                      -- required vs preferred
    PRIMARY KEY (ProjectId, SkillId)
);
```

### 4.3 Assignment (the join that carries state)

```sql
CREATE TABLE dbo.Assignment (
    AssignmentId        INT IDENTITY PRIMARY KEY,
    ProjectId           INT       NOT NULL REFERENCES dbo.Project(ProjectId),
    CandidateId         INT       NOT NULL REFERENCES dbo.Candidate(CandidateId),
    MatchScore          DECIMAL(5,2)   NULL,
    MatchRationale      NVARCHAR(MAX)  NULL,
    SponsorApprovedUtc  DATETIME2 NULL,
    SponsorApprovedBy   INT       NULL REFERENCES dbo.AppUser(AppUserId),
    OpsApprovedUtc      DATETIME2 NULL,
    OpsApprovedBy       INT       NULL REFERENCES dbo.AppUser(AppUserId),
    Status              TINYINT   NOT NULL,   -- 0 Proposed,1 SponsorApproved,2 OpsApproved,3 Active,4 Completed,5 Withdrawn
    StartDate           DATE      NULL,
    EndDate             DATE      NULL,
    RowVersion          ROWVERSION
);
CREATE UNIQUE INDEX UX_Assignment_Active
    ON dbo.Assignment(CandidateId)
    WHERE Status IN (2,3);   -- one active assignment per candidate
```

### 4.4 Reviews & derived scoring

Ratings live in one table so criteria can change over time without schema churn.

```sql
CREATE TABLE dbo.Review (
    ReviewId        INT IDENTITY PRIMARY KEY,
    AssignmentId    INT           NOT NULL REFERENCES dbo.Assignment(AssignmentId),
    ReviewType      TINYINT       NOT NULL,   -- 0 SponsorOnCandidate, 1 CandidateOnSponsor, 2 ProjectEval
    Checkpoint      TINYINT       NOT NULL,   -- 0 Midpoint, 1 Final
    SubmittedBy     INT           NOT NULL REFERENCES dbo.AppUser(AppUserId),
    SubmittedUtc    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    Commitment      TINYINT       NULL,       -- 1-5
    Availability    TINYINT       NULL,
    Guidance        TINYINT       NULL,
    OutputQuality   TINYINT       NULL,
    Comments        NVARCHAR(MAX) NULL,
    OverallScore    AS (CAST((ISNULL(Commitment,0)+ISNULL(Availability,0)
                            + ISNULL(Guidance,0)+ISNULL(OutputQuality,0)) AS DECIMAL(5,2))
                        / NULLIF(
                            (CASE WHEN Commitment    IS NULL THEN 0 ELSE 1 END)
                          + (CASE WHEN Availability  IS NULL THEN 0 ELSE 1 END)
                          + (CASE WHEN Guidance      IS NULL THEN 0 ELSE 1 END)
                          + (CASE WHEN OutputQuality IS NULL THEN 0 ELSE 1 END), 0)
                       ) PERSISTED
);
CREATE UNIQUE INDEX UX_Review_Once
    ON dbo.Review(AssignmentId, ReviewType, Checkpoint, SubmittedBy);
```

Risk signals are **computed**, not stored as a free-text status. Expose them as a view so the API and Power BI share one definition:

```sql
CREATE VIEW dbo.vCandidateRisk AS
WITH scores AS (
    SELECT a.CandidateId,
           AVG(CASE WHEN r.Checkpoint = 0 THEN r.OverallScore END) AS MidScore,
           AVG(CASE WHEN r.Checkpoint = 1 THEN r.OverallScore END) AS FinalScore,
           AVG(r.OverallScore) AS AvgScore
    FROM dbo.Assignment a
    JOIN dbo.Review r ON r.AssignmentId = a.AssignmentId AND r.ReviewType = 0
    GROUP BY a.CandidateId
),
activity AS (
    SELECT a.CandidateId,
           MAX(t.CompletedUtc) AS LastCompletionUtc,
           SUM(CASE WHEN t.Status <> 2 AND t.DueDate < CAST(SYSUTCDATETIME() AS DATE)
                    THEN 1 ELSE 0 END) AS StaleTodoCount
    FROM dbo.Assignment a
    LEFT JOIN dbo.ProjectTodo t ON t.AssignmentId = a.AssignmentId
    GROUP BY a.CandidateId
)
SELECT c.CandidateId,
       s.AvgScore, s.MidScore, s.FinalScore,
       CAST(CASE WHEN s.AvgScore < 3.0
                  OR (s.FinalScore IS NOT NULL AND s.MidScore IS NOT NULL
                      AND s.FinalScore < s.MidScore - 0.5)
            THEN 1 ELSE 0 END AS BIT) AS HasPerformanceRisk,
       CAST(CASE WHEN ISNULL(act.StaleTodoCount,0) >= 3
                  OR act.LastCompletionUtc < DATEADD(DAY,-14,SYSUTCDATETIME())
            THEN 1 ELSE 0 END AS BIT) AS HasEngagementRisk,
       ISNULL(act.StaleTodoCount,0) AS StaleTodoCount
FROM dbo.Candidate c
LEFT JOIN scores   s   ON s.CandidateId = c.CandidateId
LEFT JOIN activity act ON act.CandidateId = c.CandidateId;
```

### 4.5 Audit

Every state transition on Candidate, Assignment, Project, and Review gets an immutable row. Use SQL **temporal tables** for the cheapest correct implementation:

```sql
ALTER TABLE dbo.Assignment ADD
    ValidFrom DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL DEFAULT SYSUTCDATETIME(),
    ValidTo   DATETIME2 GENERATED ALWAYS AS ROW END   HIDDEN NOT NULL DEFAULT '9999-12-31 23:59:59.9999999',
    PERIOD FOR SYSTEM_TIME (ValidFrom, ValidTo);

ALTER TABLE dbo.Assignment
    SET (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.AssignmentHistory));
```

Plus an application-level `AuditEvent` table for *who did what and why* (temporal tables capture the row, not the actor's intent).

---

## 5. Backend — ASP.NET Core

### 5.1 Program.cs wiring

```csharp
var builder = WebApplication.CreateBuilder(args);

// --- Authentication: validate Entra-issued access tokens ---
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

// --- Authorization: policies, not scattered role strings ---
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.ViewTalentPipeline, p =>
        p.RequireRole(Roles.Executive, Roles.ProgramOps, Roles.Sponsor, Roles.HiringManager));

    options.AddPolicy(Policies.ViewHiddenScores, p =>
        p.RequireRole(Roles.Executive, Roles.ProgramOps));

    options.AddPolicy(Policies.ApproveMatch, p =>
        p.RequireRole(Roles.ProgramOps));

    options.AddPolicy(Policies.ManageOwnProfile, p =>
        p.Requirements.Add(new OwnsCandidateProfileRequirement()));

    options.AddPolicy(Policies.ManageOwnProject, p =>
        p.Requirements.Add(new OwnsProjectRequirement()));

    // Fail closed: every endpoint requires auth unless explicitly opted out.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
});

builder.Services.AddScoped<IAuthorizationHandler, OwnsCandidateProfileHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OwnsProjectHandler>();

// --- Data: managed identity, no connection string secrets ---
builder.Services.AddDbContext<LaunchPadDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Sql"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

`appsettings.json` connection string with **no secret**:

```
Server=tcp:launchpad-prod.database.windows.net,1433;
Database=launchpad;
Authentication=Active Directory Default;
Encrypt=True;
```

### 5.2 Resource-based authorization (the part that matters)

Role checks alone are insufficient. A Sponsor is allowed to see *their own* projects, not every project. Implement with `IAuthorizationHandler`:

```csharp
public sealed class OwnsProjectHandler
    : AuthorizationHandler<OwnsProjectRequirement, Project>
{
    private readonly ICurrentUser _user;
    public OwnsProjectHandler(ICurrentUser user) => _user = user;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext ctx,
        OwnsProjectRequirement requirement,
        Project project)
    {
        // Ops and Exec bypass ownership.
        if (ctx.User.IsInRole(Roles.ProgramOps) || ctx.User.IsInRole(Roles.Executive))
        {
            ctx.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (ctx.User.IsInRole(Roles.Sponsor)
            && project.Sponsor.AppUser.EntraObjectId == _user.EntraObjectId)
        {
            ctx.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
```

Called from the controller:

```csharp
[HttpGet("{id:int}")]
public async Task<ActionResult<ProjectDto>> Get(int id)
{
    var project = await _projects.GetWithSponsorAsync(id);
    if (project is null) return NotFound();

    var auth = await _authorization.AuthorizeAsync(User, project, Policies.ManageOwnProject);
    if (!auth.Succeeded) return Forbid();

    return Ok(_mapper.ToDto(project, User));   // mapper strips hidden fields
}
```

### 5.3 Field-level redaction for hidden ratings

This is the single most important security control in the app — sponsors and candidates must never receive numeric scores, **including in the JSON payload**. Do not rely on the frontend to hide them.

```csharp
public CandidateDto ToDto(Candidate c, ClaimsPrincipal user)
{
    var dto = new CandidateDto
    {
        CandidateId    = c.CandidateId,
        DisplayName    = c.AppUser.DisplayName,
        Location       = c.Location,
        Availability   = c.Availability,
        GraduationDate = c.GraduationDate,
        Skills         = c.Skills.Select(s => s.Skill.Name).ToArray(),
        Outcome        = c.Status.ToOutcomeLabel()      // Hire / Talent Plus / In Progress
    };

    // Numeric scores and risk flags are additive, never default.
    if (user.IsInRole(Roles.Executive) || user.IsInRole(Roles.ProgramOps))
    {
        dto.AverageScore       = c.Risk?.AvgScore;
        dto.HasPerformanceRisk = c.Risk?.HasPerformanceRisk;
        dto.HasEngagementRisk  = c.Risk?.HasEngagementRisk;
    }
    return dto;
}
```

Back it with an integration test per role that asserts the serialized response contains no `averageScore` key.

### 5.4 Async work → Service Bus → Functions

| Job | Trigger | Why it's async |
|---|---|---|
| Cohort-wide matching | Ops clicks "Run matching" → queue message | Seconds-to-minutes across N×M candidates/projects |
| Resume parsing | Blob upload event | Calls Azure OpenAI; latency unpredictable |
| Nightly risk recalculation | Timer (CRON) | Full-table scan, off-peak |
| Notification digests | Timer | Batched, not per-event |
| Sponsor auto-flag | Service Bus on review submit | Evaluates rolling threshold across projects |

---

## 6. Role Security with Microsoft Entra ID

### 6.1 Two app registrations

| Registration | Purpose | Key config |
|---|---|---|
| `LaunchPad-API` | Protected resource | Exposes scope `api://<api-client-id>/access_as_user`; defines **app roles**; `accessTokenAcceptedVersion: 2` |
| `LaunchPad-SPA` | Public client | SPA redirect URIs, PKCE, requests the API scope. No client secret. |

### 6.2 Define app roles on the API registration

App roles are declared in the API's manifest and land in the token as `roles` claims:

```json
"appRoles": [
  {
    "id": "6b1d3f2a-0000-4000-8000-000000000001",
    "displayName": "Executive",
    "value": "LaunchPad.Executive",
    "description": "Read-only access to all program data and dashboards.",
    "allowedMemberTypes": ["User"],
    "isEnabled": true
  },
  {
    "id": "6b1d3f2a-0000-4000-8000-000000000002",
    "displayName": "Program Ops",
    "value": "LaunchPad.ProgramOps",
    "description": "Full administrative access; approves matches and manages cohorts.",
    "allowedMemberTypes": ["User"],
    "isEnabled": true
  },
  {
    "id": "6b1d3f2a-0000-4000-8000-000000000003",
    "displayName": "Sponsor",
    "value": "LaunchPad.Sponsor",
    "description": "Manages own projects; views talent pipeline without scores.",
    "allowedMemberTypes": ["User"],
    "isEnabled": true
  },
  {
    "id": "6b1d3f2a-0000-4000-8000-000000000004",
    "displayName": "Candidate",
    "value": "LaunchPad.Candidate",
    "description": "Manages own profile and assigned project.",
    "allowedMemberTypes": ["User"],
    "isEnabled": true
  },
  {
    "id": "6b1d3f2a-0000-4000-8000-000000000005",
    "displayName": "Hiring Manager",
    "value": "LaunchPad.HiringManager",
    "description": "Read-only talent pipeline.",
    "allowedMemberTypes": ["User"],
    "isEnabled": true
  }
]
```

### 6.3 Assign roles to *groups*, never to individuals

Create five Entra security groups and assign each to the corresponding app role via **Enterprise Applications → LaunchPad-API → Users and groups**.

| Entra group | App role | Membership managed by |
|---|---|---|
| `SG-LaunchPad-Executive` | `LaunchPad.Executive` | Program Strategy Lead |
| `SG-LaunchPad-ProgramOps` | `LaunchPad.ProgramOps` | Program Manager |
| `SG-LaunchPad-Sponsor` | `LaunchPad.Sponsor` | Dynamic or Ops-managed |
| `SG-LaunchPad-Candidate` | `LaunchPad.Candidate` | Automated at cohort onboarding |
| `SG-LaunchPad-HiringManager` | `LaunchPad.HiringManager` | Ops-managed |

Group-based assignment requires Entra ID P1. Two operational benefits: onboarding a cohort becomes one group-membership bulk operation, and offboarding is guaranteed to revoke access everywhere.

> **Token bloat guard:** use app-role assignment (`roles` claim), not raw group IDs (`groups` claim). Group claims can overflow past ~150 memberships and force a Graph lookup on every request. App roles emit exactly the five values you care about.

### 6.4 Enable user assignment required

On the Enterprise Application, set **Assignment required = Yes**. Without this, any authenticated tenant user gets a token with zero roles — and if any endpoint is missing a policy, they're in. Combined with the `FallbackPolicy` in §5.1, this gives you defense in depth.

### 6.5 Conditional Access

Scope a CA policy to the LaunchPad Enterprise App:

- Require MFA.
- Require compliant or Entra-hybrid-joined device.
- Block legacy authentication.
- Optional: named-location restriction for the Executive and Program Ops roles, given they can see hidden scores.

### 6.6 The role → row mapping problem

The token tells you *what role* someone has. It does not tell you *which candidate or sponsor record* they are. Bridge it once per request:

```csharp
public sealed class CurrentUser : ICurrentUser
{
    public Guid EntraObjectId { get; }
    public string[] Roles { get; }

    public CurrentUser(IHttpContextAccessor accessor)
    {
        var principal = accessor.HttpContext!.User;
        EntraObjectId = Guid.Parse(principal.FindFirstValue("oid")!);
        Roles = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
    }
}
```

Then just-in-time provision the `AppUser` row on first authenticated request (upsert on `EntraObjectId`), so you never maintain a separate user list.

### 6.7 Optional hardening — SQL Row-Level Security

For genuine defense in depth, push candidate-scoped filtering into the database with a security predicate keyed on `SESSION_CONTEXT`:

```sql
CREATE FUNCTION dbo.fn_CandidateAccessPredicate(@CandidateId INT)
RETURNS TABLE WITH SCHEMABINDING AS
RETURN SELECT 1 AS ok
WHERE CAST(SESSION_CONTEXT(N'Role') AS NVARCHAR(50)) IN (N'ProgramOps', N'Executive')
   OR @CandidateId = CAST(SESSION_CONTEXT(N'CandidateId') AS INT);

CREATE SECURITY POLICY dbo.CandidateFilter
ADD FILTER PREDICATE dbo.fn_CandidateAccessPredicate(CandidateId) ON dbo.Candidate
WITH (STATE = ON);
```

Set `SESSION_CONTEXT` in an EF Core connection interceptor. Treat this as a second net, not the primary control — application-layer authorization stays authoritative because it produces better error semantics.

---

## 7. Frontend — React + TypeScript

### 7.1 Stack

| Concern | Choice |
|---|---|
| Build | Vite |
| Language | TypeScript, `strict: true` |
| UI | Fluent UI React v9 (matches internal Microsoft look and gives you accessibility for free) |
| Auth | `@azure/msal-react` + `@azure/msal-browser` |
| Server state | TanStack Query |
| Routing | React Router v6 |
| API client | Generated from the API's OpenAPI doc (`nswag` or `openapi-typescript-codegen`) — keeps DTOs in sync |
| Charts | Recharts, or embed Power BI for exec dashboards |

### 7.2 MSAL configuration

```ts
export const msalConfig: Configuration = {
  auth: {
    clientId: import.meta.env.VITE_SPA_CLIENT_ID,
    authority: `https://login.microsoftonline.com/${import.meta.env.VITE_TENANT_ID}`,
    redirectUri: window.location.origin,
  },
  cache: { cacheLocation: 'sessionStorage', storeAuthStateInCookie: false },
};

export const apiRequest = {
  scopes: [`api://${import.meta.env.VITE_API_CLIENT_ID}/access_as_user`],
};
```

Attach the token in a single interceptor so no call site can forget:

```ts
export async function authedFetch(input: RequestInfo, init: RequestInit = {}) {
  const account = msalInstance.getActiveAccount();
  if (!account) throw new Error('No active account');

  const result = await msalInstance.acquireTokenSilent({ ...apiRequest, account });

  return fetch(input, {
    ...init,
    headers: {
      ...init.headers,
      Authorization: `Bearer ${result.accessToken}`,
      'Content-Type': 'application/json',
    },
  });
}
```

### 7.3 Role-aware routing

```tsx
export function useRoles(): AppRole[] {
  const { accounts } = useMsal();
  return (accounts[0]?.idTokenClaims?.roles as AppRole[]) ?? [];
}

export function RequireRole({ allow, children }: {
  allow: AppRole[]; children: React.ReactNode;
}) {
  const roles = useRoles();
  if (!roles.some(r => allow.includes(r))) return <Navigate to="/unauthorized" replace />;
  return <>{children}</>;
}
```

```tsx
<Routes>
  <Route path="/" element={<RoleAwareHome />} />
  <Route path="/pipeline" element={
    <RequireRole allow={['LaunchPad.Executive','LaunchPad.ProgramOps',
                         'LaunchPad.Sponsor','LaunchPad.HiringManager']}>
      <TalentPipeline />
    </RequireRole>} />
  <Route path="/ops/approvals" element={
    <RequireRole allow={['LaunchPad.ProgramOps']}>
      <ApprovalQueue />
    </RequireRole>} />
  <Route path="/exec" element={
    <RequireRole allow={['LaunchPad.Executive','LaunchPad.ProgramOps']}>
      <ExecutiveDashboard />
    </RequireRole>} />
</Routes>
```

> Client-side guards are **navigation UX only**. They shape the menu; they are not security. The API must independently reject every unauthorized request.

---

## 8. Required Azure Services

### 8.1 Required for v1

| Service | SKU (Prod) | Purpose | Notes |
|---|---|---|---|
| **Azure App Service** | Linux, P0v3 | Hosts the ASP.NET Core API | Enable system-assigned managed identity, Always On, health check on `/healthz` |
| **Azure Static Web Apps** | Standard | Hosts the React SPA | Standard tier is required for private endpoints and custom auth; free tier for Dev |
| **Azure SQL Database** | General Purpose, Serverless (2–8 vCore, auto-pause off in Prod) | System of record | Zone-redundant in Prod; Entra-only authentication |
| **Microsoft Entra ID** | P1 | Identity, app roles, group assignment, Conditional Access | P1 required for group-based app-role assignment |
| **Azure Key Vault** | Standard | Certificates, OpenAI keys, any third-party secrets | Access via managed identity + RBAC, not access policies |
| **Azure Blob Storage** | StorageV2, Hot + Cool lifecycle | Resumes, deliverables, recordings | Private container; serve via short-lived user-delegation SAS |
| **Application Insights** | — | Distributed tracing, dependency and failure telemetry | Workspace-based |
| **Log Analytics Workspace** | Pay-as-you-go | Central log sink, KQL, alerting | 90-day retention minimum for audit |

### 8.2 Required for the async and AI capabilities

| Service | SKU | Purpose |
|---|---|---|
| **Azure Functions** | Flex Consumption or EP1 | Matching engine, resume parsing, nightly risk scoring, digests |
| **Azure Service Bus** | Standard | Reliable job queue with dead-lettering; decouples API from long jobs |
| **Azure OpenAI Service** | Standard, GPT-4o-mini or GPT-4o | Resume parsing to structured JSON, match rationale generation |
| **Azure AI Search** | Basic | Semantic skill matching and internal artifact retrieval (RAG) |

### 8.3 Recommended / phase 2

| Service | Purpose |
|---|---|
| **Azure API Management** (Developer → Basic v2) | Central auth enforcement, throttling, request logging if you expose the API beyond the SPA |
| **Azure Front Door** (Standard) | WAF, global entry point, TLS termination if you need one hostname over SPA + API |
| **Azure Cache for Redis** (Basic C0) | Cache skill taxonomy, cohort metadata, exec dashboard aggregates |
| **Power BI Embedded / Fabric** | Executive dashboards — cheaper to build than hand-rolled charts, and Ops already reads Power BI |
| **Azure Monitor Alerts + Action Groups** | Paging on 5xx spikes, SQL DTU saturation, dead-letter depth |
| **Microsoft Purview** | Data classification and retention labeling for HR-adjacent records |

### 8.4 Rough monthly cost — production

| Item | Estimate (USD) |
|---|---|
| App Service P0v3 | ~$75 |
| Azure SQL GP Serverless (avg 3 vCore) | ~$180 |
| Static Web Apps Standard | ~$9 |
| Functions Flex Consumption | ~$25 |
| Service Bus Standard | ~$10 |
| Blob Storage (250 GB + egress) | ~$10 |
| App Insights + Log Analytics (~15 GB) | ~$40 |
| Azure AI Search Basic | ~$75 |
| Azure OpenAI (low volume) | ~$30 |
| Key Vault | ~$3 |
| **Total** | **≈ $455/month** |

Dev and Test environments run roughly $80–120/month each on Basic/Free tiers with auto-pause enabled on SQL.

---

## 9. Hosting & Environment Topology

### 9.1 Three environments, three resource groups, one subscription

```
Subscription: LaunchPad-Internal
├── rg-launchpad-dev     (Free/Basic SKUs, SQL serverless auto-pause ON)
├── rg-launchpad-test    (mirrors Prod SKUs one tier down)
└── rg-launchpad-prod    (production SKUs, zone-redundant SQL)
```

Each environment gets its **own Entra app registrations** (separate client IDs and redirect URIs). Never share a registration across environments — a Dev token must not be valid in Prod.

Naming convention: `<type>-launchpad-<env>-<region>` → `app-launchpad-prod-eastus2`.

### 9.2 Network topology

**Simplest secure posture (recommended for this app size):**

```
Internet ──▶ Front Door (WAF) ──▶ Static Web App (SPA)
                              └──▶ App Service (API)
                                       │  VNet integration (outbound)
                                       ▼
                             ┌─────────────────────────┐
                             │  VNet: vnet-launchpad   │
                             │  ├── snet-app  (deleg.) │
                             │  └── snet-pe            │
                             │        Private Endpoints│
                             │        ├── Azure SQL    │
                             │        ├── Key Vault    │
                             │        ├── Blob Storage │
                             │        └── Service Bus  │
                             └─────────────────────────┘
```

- **Public network access = Disabled** on SQL, Key Vault, Storage, and Service Bus. All backend traffic traverses private endpoints.
- App Service uses **regional VNet integration** for outbound; inbound stays public but is fronted by Front Door WAF and requires a valid Entra token.
- Private DNS zones (`privatelink.database.windows.net`, `privatelink.vaultcore.azure.net`, etc.) linked to the VNet.

**Managed identity everywhere.** Grant the App Service identity:

| Target | Role |
|---|---|
| Azure SQL | `db_datareader`, `db_datawriter`, plus `EXECUTE` on procs — created via `CREATE USER [app-launchpad-prod] FROM EXTERNAL PROVIDER` |
| Key Vault | Key Vault Secrets User |
| Blob Storage | Storage Blob Data Contributor |
| Service Bus | Azure Service Bus Data Sender |
| Azure OpenAI | Cognitive Services OpenAI User |

Zero connection-string secrets in configuration. This is the single biggest security win over the Power Platform build, where connector credentials are inherently shared.

### 9.3 Deployment slots

Prod App Service runs a `staging` slot. Deploy → warm up → smoke test → **swap**. Slot-sticky settings keep environment variables from following the swap.

### 9.4 SQL operations

- **Backups:** PITR at 7 days (default), long-term retention weekly for 12 weeks / monthly for 12 months.
- **Auth:** Entra-only. Disable SQL authentication entirely.
- **Migrations:** EF Core migrations executed by a dedicated deployment identity, not the runtime app identity. Runtime app identity has no DDL rights.
- **Failover:** Prod = zone-redundant. Add a geo-replica only if the program takes an RTO commitment; for an internal program tool, zone redundancy plus PITR is usually right-sized.

### 9.5 Internal compliance gates

Before Prod goes live, expect to complete:

1. **Service Tree registration** — the app needs a service ID with a named owner accountable for ongoing compliance reviews. Register once the permanent environment and long-term owner are settled, not before.
2. **Security review / threat model** — STRIDE pass on the auth boundary, hidden-ratings redaction, and blob SAS issuance.
3. **Privacy review** — candidate PII, resumes, and evaluative ratings are in scope. Define retention (recommend: purge candidate artifacts 24 months after cohort close).
4. **Accessibility** — Fluent UI gets you most of the way; still budget for an audit pass.

---

## 10. CI/CD

```yaml
# .github/workflows/deploy.yml  (abridged)
name: Build & Deploy

on:
  push: { branches: [main] }
  pull_request: { branches: [main] }

permissions:
  id-token: write     # OIDC federated credential — no stored Azure secrets
  contents: read

jobs:
  build-api:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build --collect:"XPlat Code Coverage"
      - run: dotnet publish src/LaunchPad.Api -c Release -o ./publish
      - uses: actions/upload-artifact@v4
        with: { name: api, path: ./publish }

  build-web:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '22', cache: 'npm' }
      - run: npm ci --prefix src/LaunchPad.Web
      - run: npm run lint --prefix src/LaunchPad.Web
      - run: npm run test --prefix src/LaunchPad.Web
      - run: npm run build --prefix src/LaunchPad.Web

  deploy-prod:
    needs: [build-api, build-web]
    if: github.ref == 'refs/heads/main'
    environment: production          # required reviewers gate here
    runs-on: ubuntu-latest
    steps:
      - uses: azure/login@v2
        with:
          client-id:       ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id:       ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}
      - uses: azure/arm-deploy@v2         # infra first, idempotent
        with:
          resourceGroupName: rg-launchpad-prod
          template: infra/main.bicep
          parameters: infra/params.prod.json
      - run: dotnet ef database update --connection "$SQL_CONN"
      - uses: azure/webapps-deploy@v3
        with:
          app-name: app-launchpad-prod
          slot-name: staging
      - run: ./scripts/smoke-test.sh https://app-launchpad-prod-staging.azurewebsites.net
      - run: az webapp deployment slot swap -g rg-launchpad-prod -n app-launchpad-prod --slot staging
```

**Quality gates to enforce on PR:** build + unit tests green, ESLint/`dotnet format` clean, CodeQL, Dependabot, and a per-role authorization integration test suite that must pass before merge.

---

## 11. Observability & Operations

| Signal | Implementation |
|---|---|
| Distributed tracing | App Insights auto-instrumentation across API → SQL → Service Bus → Functions, correlated by `operation_Id` |
| Structured logging | Serilog → App Insights, always including `EntraObjectId`, `Role`, `CorrelationId` |
| Audit trail | SQL temporal tables + `AuditEvent` rows for every approval, status change, and score recalculation |
| Health | `/healthz` (liveness) and `/healthz/ready` (SQL + Service Bus reachability) |
| Alerts | 5xx rate > 1% over 5 min; p95 latency > 2s; Service Bus dead-letter count > 0; SQL CPU > 80% for 15 min |
| Dashboards | Azure Workbook for platform health; Power BI for program KPIs |

---

## 12. Phased Build Sequence

| Phase | Duration | Deliverable | Exit criteria |
|---|---|---|---|
| **0 — Foundation** | 1 week | Bicep IaC for all three environments; both Entra app registrations with app roles; the five security groups; CI/CD pipeline deploying a "hello world" API and SPA | An authenticated user can hit a role-protected endpoint and see their roles echoed back |
| **1 — Data & identity** | 2 weeks | Full EF Core model + migrations; JIT `AppUser` provisioning; authorization policies and handlers; per-role integration test suite | Every table exists; each role's access is proven by a passing test |
| **2 — Core CRUD** | 3 weeks | Candidate profile, Sponsor, Project, Cohort management; blob upload with SAS; Fluent UI shell with role-aware navigation | Ops can run a cohort end to end manually |
| **3 — Matching & approvals** | 3 weeks | Matching engine in `LaunchPad.Application` (pure, unit-tested); Service Bus + Function orchestration; top-3 matches with rationale; two-stage approval workflow | A project posts, matches generate, sponsor recommends, ops approves |
| **4 — Reviews & risk** | 2 weeks | Midpoint and final review forms; hidden scoring with field-level redaction; `vCandidateRisk` surfaced to Ops/Exec; sponsor auto-flag rules | Redaction tests pass for every role; risk view matches hand-calculated fixtures |
| **5 — Dashboards & AI** | 2 weeks | Executive dashboard (funnel: recommended → approved → hired, with the delta visible); Azure OpenAI resume parsing; AI Search semantic matching | Exec dashboard reproduces current reporting; parsed resumes populate skills with `Source = ResumeParsed` |
| **6 — Hardening & launch** | 2 weeks | Private endpoints, Conditional Access, WAF, load test, security + privacy review, Service Tree registration, runbooks | All compliance gates closed; Prod swap executed |

**Total: ~15 weeks** for a team of 3–4 engineers.

---

## 13. Migrating Off the Current Build

Run both in parallel for one cohort rather than cutting over cold.

1. **Export first.** Pull current data to CSV/JSON before touching anything. Snapshot both records and file attachments.
2. **Map identities.** Resolve every existing user to an Entra object ID. This is the step that reliably takes longer than expected — start it in Phase 0.
3. **Normalize skills.** The current multi-select fields become `Skill` + `CandidateSkill` rows. Expect to dedupe free-text variants ("Power BI", "PowerBI", "power-bi"); build the canonical taxonomy manually once and map into it.
4. **Rehydrate reviews.** Preserve original `SubmittedUtc` values so trajectory analysis (mid → final) stays valid.
5. **Migrate files** to Blob with `AzCopy`, then rewrite `ResumeBlobPath` and deliverable references.
6. **Reconcile.** Row counts, aggregate score checksums, and a spot-check of 10 candidates against the current UI before declaring parity.
7. **Freeze, cut over, keep the old app read-only** for one cohort as a fallback.

---

## 14. Key Decisions Summary

| Decision | Choice | Rationale |
|---|---|---|
| Database | Azure SQL | Relational domain; set-based reporting; temporal tables give audit for free |
| API hosting | App Service (not AKS) | Managed platform; slots; the workload doesn't justify orchestration overhead |
| Frontend hosting | Static Web Apps | CDN + PR previews + native Entra integration at near-zero cost |
| Role model | Entra app roles assigned to groups | Roles ride in the token; membership managed by Ops without a code change |
| Authorization | Policy-based + resource handlers | Role alone can't express "own project"; handlers can |
| Hidden ratings | Server-side redaction in the mapper | Client-side hiding is not a control |
| Async work | Service Bus + Functions | Matching and parsing must not block requests |
| Secrets | Managed identity + Key Vault | No connection strings in config; the main security upgrade over connector-based auth |
| IaC | Bicep | Environment parity; reviewable infra changes |

---

*Prepared for the LaunchPad platform re-architecture. Section 6 (Entra role security) and Section 9 (hosting topology) are the two areas to socialize with your security reviewer earliest — they gate the Service Tree registration and Prod approval.*
