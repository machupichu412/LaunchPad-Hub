# Performance, Reliability & Production-Readiness Audit

Audit date: 2026-09-16 · Branch: `perf/production-readiness` (off `main` @ `76061a9`)

LaunchPad is a .NET 9 + React 19 internal app sized for ~2,000 named users and <200
concurrent. This is the first performance and operations review of the codebase.

**Headline:** the app is stronger than expected on *correctness* — filtered unique
indexes, serializable transactions guarding duplicate submission, structured logging
with correlation IDs, health checks wired into App Service — and weak on the *basics of
serving traffic*: no response compression, no global error handler, no request timeouts,
a 1.71 MB single-chunk SPA, and one deployed-environment bug that silently writes user
uploads to ephemeral App Service local disk.

Scope of the implementation pass: the high-impact / low-risk tier. Everything else is
recorded below under [Deferred](#deferred-work) so it stays visible.

Baseline measurements taken before any change:

| Metric | Baseline |
|---|---|
| Backend tests | 339 passing (208 integration / 130 application / 1 domain) |
| Frontend bundle | `index-xXqmTNR8.js` **1,713.97 kB** (469.20 kB gzip), single chunk |
| Frontend CSS | 0.72 kB |
| Frontend tests | none (no runner installed) |
| `GET /api/candidates` | 2N+1 queries for N candidates |

---

## A. Database

| # | Item | Status | Evidence | Proposed change | Impact | Risk | Effort |
|---|---|---|---|---|---|---|---|
| 1 | Indexes | Partial | 13 explicit `HasIndex` incl. filtered unique `UX_Assignment_Active` (`AssignmentConfiguration.cs:33-36`), `UX_Review_Once` (`ReviewConfiguration.cs:28-30`), keyset `(CreatedUtc, CommunityPostId)` (`CommunityConfigurations.cs:28-29`). Missing on hot filter/sort columns: `Assignment.Status` (`OpsDashboardRepository.cs:20-27`, `AssignmentRepository.cs:141-145`), `Candidate.Status` (`ReportingRepository.cs:18`), `Project.Status`/`ApprovalStatus` (`ProjectRepository.cs:36`), `Review.ReviewType`+`Checkpoint` (`ReviewRepository.cs:26-27`), `Notification.CreatedUtc` (`NotificationRepository.cs:18`), `Deliverable.SubmittedUtc` (`AssignmentRepository.cs:95`) | Add 8 indexes via migration; generate only, do not apply | H | L | S |
| 2 | N+1 queries | Partial | `CandidatesController.cs:139-150` `ToDtosAsync` issues `GetRiskAsync` + `ComputeSuggestedHireOutcomeAsync` per candidate, over a whole cohort, from `:114` and `:124`. Elsewhere batching is deliberate and correct (`CohortsController.cs:118-120`, `CommunityRepository.cs:101-105`) | Batch both lookups; 2N+1 → 3 queries | H | L | M |
| 3 | Expensive queries | Partial | **No `AsNoTracking` anywhere in `src/`** — every read tracks. `SearchController.cs:62-91` loads entire project/candidate sets with all `Include`s then filters in C# (`:138`). `ProjectsController.cs:463-541` materializes a whole cohort and TF-IDF ranks in-process per request. `ReportingRepository.cs:20-23` pulls all risk rows to count two booleans client-side | `AsNoTracking()` on read paths now; query pushdown deferred | H | M | M |
| 4 | Query caching | Missing | No `IMemoryCache`, `IDistributedCache`, `AddResponseCaching`, or `AddOutputCache` in `src/`. Static lookups such as `SkillRepository.GetAllAsync:49` re-query per request | Deferred — needs TTL/invalidation design | M | M | M |
| 5 | Connection pooling | Partial | `InfrastructureServiceCollectionExtensions.cs:34-37` uses `AddDbContext` (not `AddDbContextPool`) with bare `EnableRetryOnFailure()` and **no `CommandTimeout`**. ADO.NET pooling is on by default via the connection string | Add explicit `CommandTimeout` + retry parameters | M | L | S |
| 6 | Pagination | Partial | Community is paginated well — keyset cursor, max page size 50, fetch N+1 to avoid `COUNT` (`CommunityController.cs:30,61`; `CommunityRepository.cs:27-42`). Notifications cap via `RecentTake` (`NotificationsController.cs:37`). Everything else is unbounded | Deferred — API contract change | H | H | L |

## B. API / Backend

| # | Item | Status | Evidence | Proposed change | Impact | Risk | Effort |
|---|---|---|---|---|---|---|---|
| 7 | Rate limiting | Partial | `Program.cs:80-93` fixed-window 10/min per user, applied **only** to Community writes via `[EnableRateLimiting]`. No global limiter | Add a global per-user limiter with `Retry-After` | M | L | S |
| 8 | API limits | Partial | Per-endpoint form limits exist — 110 MB (`AssignmentsController.cs:145`), 8 MB (`CommunityController.cs:97`), 2 MB avatar with a spoof-proof stream cap (`MeController.cs:62-71`). No global Kestrel body cap. Max page size only for Community | Add global `MaxRequestBodySize` from config | M | L | S |
| 9 | Response caching / ETag | Missing | No `[ResponseCache]`, ETag, or `Cache-Control` anywhere — including binary endpoints returning `File(...)` (`AppUsersController.cs:39`, `CandidatesController.cs:111`, `CommunityController.cs:210`) | Deferred | M | L | M |
| 10 | Compression | **Missing** | No `AddResponseCompression` in `Program.cs` | Add brotli + gzip for JSON | H | L | S |
| 11 | Unbounded lists | Missing | ~25 actions across 11 controllers return uncapped collections: `CandidatesController.cs:114,124`; `ProjectsController.cs:107,120,197,207,440,461,643`; `AssignmentsController.cs:57,71,128,220`; `ReviewsController.cs:128`; `CohortsController.cs:37`; `SkillsController.cs:30,38`; `SponsorsController.cs:104`; `MatchingController.cs:51`; `OpsController.cs:29` | Deferred with #6 | H | H | L |
| 12 | Error handling | **Missing** | No `IExceptionHandler`, `UseExceptionHandler`, or `AddProblemDetails`; unhandled exceptions fall through to a bare Kestrel 500. `UseDeveloperExceptionPage` is correctly gated on `IsDevelopment()` (`Program.cs:215`). Validation failures do use ProblemDetails (`AssignmentsController.cs:161`) | Add `IExceptionHandler` + `AddProblemDetails`, correlation id in the body | H | L | S |
| 13 | Outbound timeouts | **Missing** | No `IHttpClientFactory` or Polly registrations at all. `ServiceBusClient` constructed **per message** in `ServiceBusNotificationPublisher.cs:41`, `ServiceBusMatchingJobPublisher.cs:39`, `ServiceBusFolderProvisioningJobPublisher.cs:39`. `GraphServiceClient` is a DI singleton (`InfrastructureServiceCollectionExtensions.cs:94`) but `GraphEmailNotifier.cs:43` rebuilds one per send | Hoist clients to singletons; add explicit timeouts | H | M | M |
| 14 | Resilience | Missing | No circuit breakers, fallbacks, or resilience pipelines. Only graceful config-degradation no-ops (`GraphEmailNotifier.cs:36-42`, `NoOpAiClients.cs`) | Deferred | M | M | M |
| 15 | Duplicate submission | **Done** | Serializable-transaction check-then-insert (`AssignmentRepository.cs:211-250`, `252-315`), unique indexes `UX_Review_Once`, `UX_Assignment_Active`, `UX_ProjectTodo_LinkedReview_Once`, `UX_ProjectInterest_Candidate_Project`, race-safe hashtag upsert (`CommunityRepository.cs:107-128`). No `Idempotency-Key` header handling, but constraint-based protection is sound | None | — | — | — |
| 16 | Duplicate payments | **N/A** | No payment, billing, Stripe, or invoice code anywhere in the repo | None | — | — | — |
| 17 | Upload limits | Partial | Avatar: content-type allowlist + 2 MB + spoof-proof stream cap (`MeController.cs:56-71`). Community image: allowlist + 8 MB (`CommunityRequestValidators.cs:7-29`). **Deliverables: size-only validation, no content-type allowlist and no magic-byte sniffing** (`AssignmentRequestValidators.cs:13-20`). Sniffing exists only for resumes (`ResumeTextExtractor.cs:74-85`) | Deferred — security-adjacent, own change | M | M | S |
| 18 | Upload compression | Missing | Images stored as received; no resize, format conversion, or metadata stripping | Deferred | L | L | M |
| 19 | Spending caps | N/A | Azure OpenAI is interface-only no-ops (`NoOpAiClients.cs`; `ResumeParsingFunction.cs:23` is a TODO). Graph mail and Service Bus are real but unguarded. No SendGrid/Twilio | Manual: configure Azure budget alerts | M | L | S |

## C. Frontend

| # | Item | Status | Evidence | Proposed change | Impact | Risk | Effort |
|---|---|---|---|---|---|---|---|
| 20 | Loading states | Done | Fluent `Spinner` in ~30 views (`OpsDashboard.tsx:89`, `MyProfile.tsx:101`, `Community.tsx:64`, guards at `RequireRole.tsx:21`, …). **No `Skeleton` used anywhere** → content-height jump on resolve. Gaps: `NavMenu.tsx:166,176` (no `isLoading`), `SubmitReview.tsx`, `SkillTagPicker.tsx:56` (`categoriesLoading` destructured, never rendered) | Skeletons deferred; `NavMenu`/`BrandMark` avoided (conflict with PR #37) | L | L | M |
| 21 | Empty states | **Done** | Near-universal: `ApprovalQueue.tsx:73`, `TalentPipeline.tsx:170-171` (distinguishes "no data" from "no filter match"), `Community.tsx:66`, `ProjectMarketplace.tsx:170`, +18 more. Sole gap: `AssignedCandidatesSection.tsx:49` returns `null` | None | — | — | — |
| 22 | Error states | Partial | `isError` rendered as plain `<Body1>` text in ~25 views. **No ErrorBoundary anywhere** — a render throw white-screens the app. **Zero `refetch()` calls or retry buttons** in the whole tree. `authedFetch.ts:82-90` logs decoded JWT claims to the browser console | Add ErrorBoundary, retry affordance, remove the JWT logging | H | L | M |
| 23 | Debouncing | Missing | 7 filter inputs update state per keystroke with no debounce/`useDeferredValue`: `ProjectMarketplace.tsx:150`, `OpsProjects.tsx:100`, `TalentPipeline.tsx:162`, `ProjectApprovals.tsx:254`, `SkillPicker.tsx:96`, `SkillTagPicker.tsx:109`, `HelpCenter.tsx:361`. All filter client-side, so no request storm — but each keystroke re-renders the full list. Submit buttons **are** correctly disabled on `isPending` | Add `useDebouncedValue`, memoize filtered arrays | M | L | M |
| 24 | Re-renders | Partial | `ActiveRoleContext.tsx:38` context value is an inline object literal recreated every render; `useRoles.ts:19` returns a fresh `[]` when claims are absent. `ThemeModeContext.tsx:39` does this correctly with `useMemo`. No `React.memo`/`useCallback` anywhere. `filtered` arrays unmemoized in `ProjectMarketplace.tsx:126`, `OpsProjects.tsx:79`, `TalentPipeline.tsx:118` | Memoize context value + stable empty array | M | L | S |
| 25 | Code splitting | **Missing** | Zero `React.lazy`/`Suspense`/dynamic `import()`. All 34 `<Route>` components statically imported at `App.tsx:15-42`, so every user downloads ops + exec + sponsor + candidate code. `recharts` (`OpsDashboard.tsx:17`, `ExecutiveDashboard.tsx:14`) and `react-avatar-editor` (`AvatarEditorDialog.tsx:2`) sit in the main chunk. No `loading="lazy"` on any `<img>` | Route-level lazy + vendor `manualChunks` | H | L | M |
| 26 | Deferred scripts | **N/A** | `index.html` contains only the module entry (line 16). No analytics, chat widget, or font CDN | None | — | — | — |
| 27 | Minification | Partial | `vite.config.ts` is 5 lines — `plugins: [react()]` only. esbuild minification is on by default and sourcemaps are off in prod (verified: no `.map` in `dist/assets`). But output is a **single 1,713.97 kB chunk**, no `manualChunks`, no size budget in CI. 61 barrel imports of `@fluentui/react-components`, 27 of `@fluentui/react-icons` | Add `manualChunks` + `chunkSizeWarningLimit` | H | L | S |
| 28 | Static images | Partial | `public/brand/launchpad-logo-320.png` 60 kB (WebP ≈10 kB); `rocket-launch.webp` 275 kB eagerly loaded; no `srcset`/`<picture>` anywhere. `BrandMark.tsx:59` sets `height` only with `width:'auto'` → layout shift | Re-encode logo to WebP; lazy-load below-fold images | M | L | S |

## D. Dependencies & Build

| # | Item | Status | Evidence | Proposed change | Impact | Risk | Effort |
|---|---|---|---|---|---|---|---|
| 29 | Unused dependencies | Partial | `net9.0` across all 8 csproj. **No `Directory.Packages.props`, no `global.json`, no `nuget.config`** — versions duplicated per project. **`Microsoft.EntityFrameworkCore.InMemory` ships in the production API package** (`LaunchPad.Api.csproj:20`) purely for the local-demo path. `Serilog.AspNetCore 8.0.3` is a major behind the net9.0 line; `Swashbuckle 6.9.0` is old (Development-only exposure). npm: all deps imported somewhere; `react-avatar-editor` in 1 file, `recharts` in 2. `package-lock.json` present. **No `engines` field, no `.nvmrc`** — Node 22 pinned only in CI (`deploy.yml:38`) | Gate `EFCore.InMemory`; add `.nvmrc` | M | L | S |

## E. Infrastructure & Operations

| # | Item | Status | Evidence | Proposed change | Impact | Risk | Effort |
|---|---|---|---|---|---|---|---|
| 30 | CDN | Partial | SPA on Azure Static Web Apps (`staticWebApp.bicep:10-22`), Free in dev/test, Standard in prod. **No Front Door / Azure CDN resource anywhere**; the API sits on a public App Service hostname with no WAF. **No `staticwebapp.config.json` in the repo** → no `Cache-Control`, no SPA `navigationFallback`, no CSP. Vite default hashing is in effect | Add `staticwebapp.config.json` | H | L | S |
| 31 | Scaling readiness | **Partial / broken** | Plan `P0v3` prod / `B1` non-prod, **no `capacity` set → 1 instance, no `autoscalesettings` anywhere**. Health check correctly wired (`appService.bicep:57` → `Program.cs:229,234`), `alwaysOn: true`. **`Storage__AccountUrl` is absent from `appService.bicep:61-67`**, so `Program.cs:163-172` selects `LocalDiskProfilePictureStorage` in every deployed environment — avatars written to `ContentRootPath/App_Data/avatars`, lost on restart/swap and unshareable across instances. Rate limiter is in-process (per-instance once scaled). No `AddDataProtection` key persistence. Staging slot has no sticky settings | **Fix `Storage__AccountUrl` (Group 1)**; autoscale deferred | H | L | S |
| 32 | Error logging | Partial | Workspace-based App Insights (`appInsights.bicep:9-18`, Log Analytics `PerGB2018`, 90-day retention), injected into both hosts. Serilog with `Enrich.FromLogContext` and a correlation-id middleware that accepts and echoes `X-Correlation-Id` (`CorrelationIdMiddleware.cs:15-28`, registered first at `Program.cs:220`). Gaps: **no API sampling** (only Functions, `host.json:5-8`), **no `diagnosticSettings` on SQL / Service Bus / Storage / Key Vault**, no daily cap | Deferred | M | L | M |
| 33 | Uptime monitoring | **Missing** | No `webtests`, `metricAlerts`, `scheduledQueryRules`, or `actionGroups` in any Bicep file. The four alerts at `launchpad-build-guide.md:909` are documented but never codified. No dead-letter alerting despite 4 queues | Add `alerts.bicep` | H | L | M |
| 34 | Load testing | Missing | No k6/Artillery/JMeter/Azure Load Testing assets in the repo. `scripts/smoke-test.sh` is a 2-request curl check, not a load test | Deferred | M | L | M |
| 35 | Backup & restore | Missing | `sql.bicep:84-100` sets `requestedBackupStorageRedundancy` but has **no `backupShortTermRetentionPolicies` and no `backupLongTermRetentionPolicies`** → PITR at the 7-day default; the documented 12-week/12-month LTR (`launchpad-build-guide.md:816`) is never applied. `storage.bicep:71-74` has **no soft delete, no versioning, no change feed** — deleted uploads are unrecoverable. Key Vault has soft delete but no purge protection. No restore runbook (`README.md` is one line) | Add retention + soft delete; write restore runbook | H | L | M |

---

## Prioritized plan

Highest impact / lowest risk first. One commit per group.

| Group | Change | Impact | Risk | Status |
|---|---|---|---|---|
| 0 | This audit document | — | — | ☐ |
| 1 | `Storage__AccountUrl` in Bicep — uploads reach Blob, not local disk | H | L | ☐ |
| 2 | Response compression + global `IExceptionHandler`/ProblemDetails + body cap | H | L | ☐ |
| 3 | Candidate N+1 fix + `AsNoTracking` on read paths + command timeout | H | M | ☐ |
| 4 | 8 performance indexes (migration generated, **not applied**) | H | L | ☐ |
| 5 | Global rate limit + singleton Service Bus / Graph clients with timeouts | H | M | ☐ |
| 6 | Route-level code splitting + vendor chunks | H | L | ☐ |
| 7 | ErrorBoundary + request timeout + retry affordance + JWT-log removal | H | L | ☐ |
| 8 | Debounced filters + lazy images + logo re-encode | M | L | ☐ |
| 9 | vitest + tests for changed components | M | L | ☐ |
| 10 | `staticwebapp.config.json` + backup retention + alerts Bicep | H | L | ☐ |
| 11 | Final report | — | — | ☐ |

---

## Deferred work

Recorded deliberately, not done in this pass:

**Needs its own plan (API contract change).** Pagination for the ~25 unbounded list
endpoints (#6, #11) — the frontend consumes full arrays today, so this is a coordinated
backend + frontend migration.

**Needs design work.** Query caching with TTL and invalidation (#4); ETag / response
caching (#9); Polly circuit breakers and fallbacks (#14); query pushdown for
`SearchController` and `ProjectsController.GetEligibleCandidates` (#3); upload image
compression (#18); Skeleton components to replace spinners (#20).

**Security-adjacent, flagged separately.** Deliverable uploads accept any content type
with no magic-byte sniffing (`AssignmentRequestValidators.cs:13-20`) — every other
upload path validates, so this is an inconsistency worth its own reviewed change.

**Pre-existing CI/CD issues found during the audit, outside this scope.**
- EF migrations run from a GitHub-hosted runner against private-endpoint-only SQL
  (`deploy.yml:113,150`) — **this will fail as written**; needs a self-hosted runner or
  a firewall exception.
- `integration-real-sql` is deliberately excluded from both deploy jobs' `needs`
  (`deploy.yml:58-60`), so it cannot block a release.
- Coverage is collected but never thresholded or uploaded.
- No rollback / swap-back step after the prod slot swap (`deploy.yml:156-159`).
- SWA deploy uses long-lived PATs, the only non-OIDC secrets in the pipeline.
- `scripts/setup-entra.sh` is gitignored (`.gitignore:34`) — a required onboarding step
  lives outside version control.

---

## Manual follow-ups

To be completed after this pass; see the final report section for the full list.
