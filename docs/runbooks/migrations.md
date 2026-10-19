# Applying a database migration

CI cannot do this for you, and the pipeline no longer pretends to.

Azure SQL here is private-endpoint-only in every environment, and public network access is
denied tenant-wide by policy — see the `publicNetworkAccess` comment in
[`infra/modules/sql.bicep`](../../infra/modules/sql.bicep). A GitHub-hosted runner has no route
to the server and cannot be given one by opening a firewall rule. Separately, CLAUDE.md puts
DDL rights on a dedicated deployment identity, not on the app's runtime identity, so the API
applying its own migrations at startup is not an option either.

So: **CI generates the script, a human applies it from inside the network.**

## What CI gives you

Every `build-api` run uploads a `migrations` artifact containing `migrations.sql`, generated
with `dotnet ef migrations script --idempotent`. Each migration in it is wrapped in a check
against `__EFMigrationsHistory`, so running it twice is a no-op and running it against a
database that is already partly migrated applies only what is missing.

## Applying it

1. Download the `migrations` artifact from the build you are about to deploy.
2. Get on the network — Bastion host, point-to-site VPN, or an Azure Cloud Shell instance in a
   VNet-injected subnet. Anything with a route to the private endpoint works.
3. Authenticate as a member of the SQL Entra admin group (`aadAdminObjectId` in the env's
   params file). SQL authentication is disabled; Entra is the only way in.
4. Review the script. For anything that drops or rewrites, confirm it is what you expect.
5. Apply it:

   ```bash
   sqlcmd -S sql-launchpad-<env>-<suffix>.database.windows.net -d launchpad \
     --authentication-method ActiveDirectoryAzureCli -i migrations.sql
   ```

6. **For Prod, do this before approving the `deploy-prod` environment gate.** The gate exists
   to give you that window. A build that reaches a schema it does not expect fails at runtime,
   not at deploy time.

## Currently pending

`AddPerformanceIndexes` has never been applied to any environment. It drops four single-column
FK indexes and creates eight composites that lead with the same columns, so foreign-key lookups
keep their coverage — the reasoning is written out at the bottom of
[`PERF_AUDIT.md`](../../PERF_AUDIT.md). Review that section before running it the first time.

## If this becomes painful

The fix is a runner with a route to the VNet — a self-hosted GitHub runner on a VM in the
private subnet, or a VNet-joined `Microsoft.Resources/deploymentScripts` container invoked from
the Bicep deployment. Both are real infrastructure with real upkeep, which is why neither is
here yet. Trade for them when the manual step starts getting skipped, because a skipped
migration is how Prod gets a schema nobody can reproduce.
