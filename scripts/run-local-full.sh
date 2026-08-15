#!/usr/bin/env bash
set -euo pipefail

# Runs the "Tier 1" homelab setup: the API against a REAL SQL Server (a local
# Docker container via docker-compose.homelab.yml, migrated + seeded), instead
# of run-local-demo.sh's in-memory database. This exercises SQL-Server-only
# behavior the in-memory provider can't represent at all — the filtered
# unique index on Assignment (one active assignment per candidate) and the
# vCandidateRisk view — before a migration ever reaches Azure SQL dev.
#
# Still requires a real Entra ID tenant to sign in (see scripts/setup-entra.sh)
# — this only replaces the database, not auth. See the "Homelab Azure-service
# emulation" plan for the full picture of what is/isn't emulated and why.
#
# Usage: ./scripts/run-local-full.sh
# Prereqs: Docker running, and a .env file (copy .env.example) with MSSQL_SA_PASSWORD set.

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

api_port=5254
web_port=5173
sql_port=1433
compose_file="docker-compose.homelab.yml"

check_port_free() {
  local port="$1" name="$2"
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port ${port} (${name}) is already in use:" >&2
    lsof -nP -iTCP:"$port" -sTCP:LISTEN >&2
    echo "Stop that process first, or the local stack will fail to bind." >&2
    exit 1
  fi
}

resolve_dotnet() {
  local candidate major
  for candidate in dotnet "$HOME/.dotnet/dotnet"; do
    command -v "$candidate" >/dev/null 2>&1 || continue
    major="$("$candidate" --version 2>/dev/null | cut -d. -f1)"
    [[ "$major" =~ ^[0-9]+$ ]] || continue
    if (( major >= 9 )); then
      echo "$candidate"
      return 0
    fi
  done
  return 1
}

dotnet_bin="$(resolve_dotnet)" || {
  echo "No .NET 9 SDK found on PATH or at ~/.dotnet/dotnet." >&2
  echo "Install it from https://dotnet.microsoft.com/download/dotnet/9.0" >&2
  exit 1
}
command -v npm >/dev/null 2>&1 || { echo "npm not found on PATH." >&2; exit 1; }
command -v docker >/dev/null 2>&1 || { echo "docker not found on PATH." >&2; exit 1; }
command -v nc >/dev/null 2>&1 || { echo "nc (netcat) not found on PATH — needed to poll SQL Server readiness." >&2; exit 1; }

if [[ ! -f .env ]]; then
  echo ".env not found — copy .env.example to .env and set MSSQL_SA_PASSWORD first." >&2
  exit 1
fi
# shellcheck disable=SC1091
source .env
: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD not set in .env}"

check_port_free "$api_port" api
check_port_free "$web_port" web

if [[ ! -d "src/LaunchPad.Web/node_modules" ]]; then
  echo "Installing frontend dependencies..."
  npm --prefix src/LaunchPad.Web install
fi

echo "Starting local SQL Server container..."
docker compose -f "$compose_file" up -d

# Polled from the host rather than a container-level `healthcheck:` — the
# azure-sql-edge image doesn't bundle sqlcmd/mssql-tools to check readiness
# with from the inside (see docker-compose.homelab.yml's comment). `nc -z` is
# used over bash's /dev/tcp pseudo-device — the latter is a bash-only
# extension that isn't reliably enabled in every shell/environment, where nc
# itself is close to universal (preinstalled on macOS and most Linux distros).
# A bare TCP connect only proves the port is listening, not that SQL Server
# has finished initializing system databases — `dotnet ef database update`
# below leans on EF Core's EnableRetryOnFailure to absorb whatever gap remains.
echo "Waiting for SQL Server to accept connections on port ${sql_port}..."
sql_ready=false
for _ in $(seq 1 60); do
  if nc -z -w 2 localhost "$sql_port" 2>/dev/null; then
    sql_ready=true
    break
  fi
  sleep 2
done
if [[ "$sql_ready" != true ]]; then
  echo "SQL Server didn't start accepting connections within 120s — check 'docker compose -f ${compose_file} logs sql'." >&2
  exit 1
fi
# Give SQL Server's own internal startup a little more room before the first
# connection attempt — EnableRetryOnFailure covers transient failures during
# `dotnet ef database update`, but a fixed pause here means fewer retries logged.
sleep 5

# Not committed anywhere (see docker-compose.homelab.yml's comment on why this
# isn't baked into launchSettings.json's LocalFull profile) — exported into
# this script's own environment only, inherited by the dotnet processes it
# spawns below.
export ConnectionStrings__Sql="Server=localhost,${sql_port};Database=launchpad;User Id=sa;Password=${MSSQL_SA_PASSWORD};TrustServerCertificate=True;Encrypt=True;"

echo "Applying EF Core migrations..."
"$dotnet_bin" ef database update \
  --project src/LaunchPad.Infrastructure \
  --startup-project src/LaunchPad.Api

cleanup() {
  echo
  echo "Stopping local stack..."
  kill "$api_pid" "$web_pid" 2>/dev/null || true
  echo "(SQL Server container left running — 'docker compose -f ${compose_file} down' to stop it, add -v to also drop its data volume.)"
}
trap cleanup EXIT INT TERM

echo "Starting API (LocalFull profile, real SQL Server, seeded on startup) on http://localhost:${api_port}..."
"$dotnet_bin" run --launch-profile LocalFull --project src/LaunchPad.Api > >(sed -u 's/^/[api] /') 2>&1 &
api_pid=$!

echo "Starting frontend dev server on http://localhost:${web_port}..."
npm --prefix src/LaunchPad.Web run dev > >(sed -u 's/^/[web] /') 2>&1 &
web_pid=$!

echo
echo "Waiting for the API to become healthy..."
api_ready=false
for _ in $(seq 1 60); do
  if curl -sf "http://localhost:${api_port}/healthz" >/dev/null 2>&1; then
    api_ready=true
    break
  fi
  sleep 1
done
if [[ "$api_ready" != true ]]; then
  echo "API didn't report healthy within 60s — check the [api] log lines above." >&2
fi

cat <<EOF

LaunchPad local-full stack is running:
  Frontend:   http://localhost:${web_port}
  API:        http://localhost:${api_port}/swagger
  SQL Server: localhost:${sql_port} (sa / see .env)

This runs against a real local SQL Server, but still requires a real Entra ID
sign-in — see scripts/setup-entra.sh and add your account to a SG-LaunchPad-*
group. Press Ctrl+C to stop the API and web server (the SQL container keeps
running; see the cleanup note above).
EOF

wait
