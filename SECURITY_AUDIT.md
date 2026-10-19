# Security audit — 2026-10-05

Scope: repository code, API authorization and middleware, upload/storage implementations,
background jobs, React request handling, dependency feeds, Azure Bicep, and deployment workflows.
This is a code and infrastructure-template audit. It does not attest to live Azure settings,
Entra assignments, Graph application permissions, deployed binaries, or subscription billing.
All findings below have corresponding repository changes.

## Findings and implemented changes

| Severity | Finding | Implemented mitigation |
| --- | --- | --- |
| High | `[Authorize]` and the fallback accepted authenticated principals without a recognized application role; role-only policies did not require a valid object ID or delegated scope. | Default and fallback policies plus a global controller authorization filter require a recognized role, nonempty GUID object ID, and `access_as_user`. App-only principals fail closed. Invalid principals cannot provision AppUser rows. |
| High | Candidate + ProgramOps principals could use the assignment ownership bypass to submit a candidate review for another participant. | Candidate review submissions explicitly require the assignment candidate's identity, regardless of other roles. Both candidate review types have regression tests. |
| High | Rate limiting occurred after provisioning/authorization, allowing rejected requests to query or write SQL. Per-user limits alone did not bound traffic across accounts. | Process-wide rate/concurrency admission before authentication; shared anonymous and per-user quotas before authorization/provisioning; mutation quotas, no queues, positive configuration validation, and Retry-After on rejection. |
| High | Upload, download, and matching endpoints lacked cost-specific limits. Matching attempts could repeatedly queue expensive cohort work. | Separate user upload quota, hourly/daily instance upload quotas, two concurrent uploads, daily instance download quota, download burst quota, per-cohort matching cooldown shared across Ops, and total matching quota. Anonymous callers cannot consume a named cohort's cooldown. |
| High | Azure dev deployments used `Development`, exposing diagnostics/Swagger and allowing persona-auth/demo flags to activate in cloud deployments. | Every Azure API environment uses Staging or Production; IaC explicitly disables persona auth and the in-memory demo database. Local Development remains available. |
| High | Dependency feeds reported vulnerable Kiota, SQLite, nanoid, PostCSS, and React Router packages. | Kiota abstractions **and HTTP transport** pinned to 1.22.0, test SQLite bundle moved to 3.0.3, and compatible npm lockfile updates installed. NuGet audit warnings now fail restore; npm audit gates CI. Router's RSC advisory is not the SPA's execution mode, but the affected version is patched anyway. |
| High | Production disabled the public Service Bus endpoint while using Standard without a private route. Storage/Service Bus shared-key authentication was still available. | Production uses one Premium messaging unit with a private endpoint and existing private DNS. Nonproduction retains Standard with Entra RBAC. Shared-key storage access and Service Bus local auth are disabled; TLS 1.2 is explicit. Readiness uses managed identity and the sender-compatible check mode. |
| Medium | Functions could scale to 100 instances, with default concurrency/timeout/retries. | Flex ceiling reduced to 40; Service Bus concurrency limited to two per worker, prefetch disabled, dynamic concurrency disabled, blob concurrency one, function timeout five minutes, message retries three, queue size 1 GiB and expiry one day. |
| Medium | Resume extraction buffered an entire stream before checking size; compressed DOCX inputs could expand dramatically. | Stop reading at 12 MiB, bound ZIP entry count, per-entry and total expanded size, bound XML characters, and honor cancellation while walking paragraphs. Malicious-input tests exercise both bounds. |
| Medium | A repeated SharePoint filename overwrote an existing item, potentially changing older deliverables' contents; local filenames were not sanitized at the storage boundary. | Storage-generated unique filenames and sanitization in both Graph and local implementations. Original user-facing filenames remain on the deliverable. |
| Medium | Blob versions/snapshots had no cleanup, telemetry no daily cap, and alert recipients could be empty with all operational alerts disabled. | Delete historical versions/snapshots after 30 days, cap Log Analytics at 1 GiB/day, create resource-group monthly budget with actual/forecast notifications, require an alert recipient in the deployment template and CI. |
| Medium | API responses lacked explicit no-store/nosniff controls; correlation IDs reflected arbitrary header values; browsers hid Retry-After and retried throttled requests. | API no-store/nosniff/frame controls and HSTS, GUID-only correlation IDs, exposed Retry-After, user-readable 429 errors and no automatic query retry on 429. SPA adds CSP, HSTS and Permissions-Policy. |
| Medium | All CI jobs had OIDC permissions, and Functions artifacts were built but never deployed. | OIDC token permission restricted to deployment jobs, workflow runs serialized, and both deployments publish the Functions artifact so host limits ship with the API. |

Existing resource ownership checks, ProgramOps-only approvals/status changes, server-side
numeric-score redaction, private SQL/storage/Key Vault, and managed-identity data access remain
in place and are covered by the existing regression suite.

## Request and workload defaults

Limits count attempts, including invalid payloads. They operate per API process unless stated
otherwise. All rejection responses include Retry-After; no waiting queues accumulate work.

| Control | Default | Configuration |
| --- | --- | --- |
| Pre-auth admission | 3,000 requests/minute, 100 concurrent | `RateLimiting:InstancePermitLimit`, `RateLimiting:ConcurrentRequests` |
| General requests | 300/user/minute; invalid/anonymous principals share one partition | `RateLimiting:PermitLimit`, `RateLimiting:WindowSeconds` |
| Mutations | 30/user/minute | `RateLimiting:WritePermitLimit` |
| Avatar/deliverable uploads and multipart community post attempts | 6/user/hour, 60/instance/hour, 100/instance/day, two concurrent | `RateLimiting:UploadsPerHour`, `RateLimiting:InstanceUploadsPerHour`, `RateLimiting:InstanceUploadsPerDay` |
| Community posts/comments | 10/user/minute, plus general mutation and upload limits; images capped at 8 MiB by validation | Named CommunityWrite policy |
| Deliverable file downloads | 30/user/minute, 500/instance/day | Named Download policy, `RateLimiting:InstanceDownloadsPerDay` |
| Avatar/community image reads | 120/user/minute, 10,000/instance/day | Named MediaRead policy, `RateLimiting:InstanceMediaReadsPerDay` |
| Matching | One attempt/cohort/five minutes, ten attempts/instance/hour | Named Matching policy |
| Readiness dependency probes | Six/instance/minute | Named Readiness policy |
| Ordinary controller request bodies | 1 MiB | Global MVC request-size filter |
| Upload transport envelopes | Avatar 3 MiB, community post 9 MiB, deliverable 110 MiB | Action request-size limits; actual file validators remain stricter |

The dependency-free `/healthz` liveness endpoint remains exempt so admission limits do not
cause healthy instances to fail platform probes. Readiness stays public for the staging smoke
test, with a shared throttle and no user provisioning.

## Deployment and billing implications

- Commit changes are not deployed by this audit. Deployment must use the hardened templates
  and rebuild both API and Functions. Existing Azure settings need comparison against them.
- Set the GitHub environment variable `AZURE_ALERT_EMAIL` for development and production.
  Direct template deployments must pass a real `alertEmail`; the empty checked-in parameter
  values intentionally fail validation until overridden. No recipient was invented.
- Monthly budgets are initial, adjustable thresholds: **300** for dev/test and **1,500** for
  production, in the subscription's billing currency. Confirm these with the owner before
  deploying. Azure budget notifications are delayed monitoring, not a spending shutdown.
- Production Service Bus Premium introduces a higher fixed baseline and may require a new
  namespace/migration if the existing Standard namespace cannot be upgraded in place. Preserve
  queued jobs and verify managed identities, DNS, and connectivity before cutover. Dev/test
  remain Standard. The API and Functions never receive namespace-wide Data Owner rights.
- API limits are in memory: restarts reset counters; multiple instances or an active staging
  slot multiply allowances. IaC explicitly fixes the API plan at one instance. A gateway or
  distributed quota store is required before scale-out or for subscription-wide durable quotas.
- Flex Consumption supports a minimum maximum-instance setting of 40; it is not a forty-instance
  reservation. Scale limits apply per function group, so this is not an absolute forty-worker
  app-wide spending cap. No always-ready instances are added.
- Log caps can suppress diagnostics after the daily allowance; they are not instantaneous hard
  billing guarantees. Blob base data is retained; historical-version cleanup does not delete
  live HR records or establish an HR retention policy.
- Application controls reduce database/processing/storage abuse after traffic reaches the host.
  They cannot guarantee a fixed bill or prevent volumetric ingress/CDN abuse. Corporate ingress
  restrictions or an edge WAF must be configured with real network/origin details if the public
  API/SPA exposure is outside the intended internal-access model.
- Verify Entra tenant/audience settings, enterprise-app assignment-required, group-to-role
  mappings, Graph permissions restricted to the intended site/mailbox, runtime SQL grants, and
  CORS/custom API origins in the actual environment. SPA CSP currently allows Azure App Service
  API hosts; a custom API domain must be added explicitly. These tenant-specific settings were
  not available as evidence in this repository audit.

## Validation

Final verification:

- Release solution build: passed, zero warnings/errors.
- Release tests: 425 passed (1 Domain, 146 Application, 278 API integration).
- Frontend: 16 tests passed; production TypeScript/Vite build passed.
- Frontend lint: passed with three existing Fast Refresh export warnings.
- Repository `dotnet format --verify-no-changes --no-restore`: passed.
- NuGet vulnerability audit across all projects/transitive dependencies: no reported vulnerabilities.
- npm audit: zero reported vulnerabilities.
- Bicep compilation: passed; four existing hardcoded-environment-URL lint warnings in network.bicep.
- JSON configurations, deployment workflow YAML, and `git diff --check`: passed.

 SQL Server-specific tests require
`SQLSERVER_TEST_CONNECTION`; absent that environment, their existing harness does not exercise
real SQL. The SQLite and in-memory integration tests still exercise role/resource boundaries.

## References

- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [Flex Consumption scaling](https://learn.microsoft.com/en-us/azure/azure-functions/flex-consumption-plan)
- [Service Bus security and Premium Private Link](https://learn.microsoft.com/en-us/azure/service-bus-messaging/secure-service-bus)
- [Functions GitHub deployment](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-github-actions)
- [Kiota advisory](https://github.com/advisories/GHSA-7j59-v9qr-6fq9)
- [SQLite advisory](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)
