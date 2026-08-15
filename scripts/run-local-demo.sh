#!/usr/bin/env bash
set -euo pipefail

# Runs the local demo: the API against an in-memory, pre-seeded database (no
# Azure SQL needed — see LocalDemoSeeder.cs and the "LocalDemo" launch profile
# in src/LaunchPad.Api/Properties/launchSettings.json) alongside the frontend
# dev server, both in one terminal. Ctrl+C stops both. A real Entra ID tenant
# is still required to sign in — auth isn't emulated by this script, only the
# database is (see scripts/setup-entra.sh). For a real local SQL Server
# instead of in-memory, see scripts/run-local-full.sh.
#
# Usage: ./scripts/run-local-demo.sh

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

api_port=5254
web_port=5173

check_port_free() {
  local port="$1" name="$2"
  if lsof -nP -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port ${port} (${name}) is already in use:" >&2
    lsof -nP -iTCP:"$port" -sTCP:LISTEN >&2
    echo "Stop that process first, or the local demo will fail to bind." >&2
    exit 1
  fi
}

# Prefers whichever of these actually supports net9.0 (this repo's target
# framework) — a PATH with an older SDK ahead of a newer one it doesn't matter
# where (e.g. a Homebrew/apt dotnet shadowing a dotnet-install.sh install under
# ~/.dotnet) is a common multi-SDK gotcha, not something worth failing on with
# a cryptic NETSDK1045 wall of text.
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

check_port_free "$api_port" api
check_port_free "$web_port" web

if [[ ! -d "src/LaunchPad.Web/node_modules" ]]; then
  echo "Installing frontend dependencies..."
  npm --prefix src/LaunchPad.Web install
fi

cleanup() {
  echo
  echo "Stopping local demo..."
  kill "$api_pid" "$web_pid" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "Starting API (LocalDemo profile, in-memory seeded DB) on http://localhost:${api_port}..."
"$dotnet_bin" run --launch-profile LocalDemo --project src/LaunchPad.Api > >(sed -u 's/^/[api] /') 2>&1 &
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

LaunchPad local demo is running:
  Frontend: http://localhost:${web_port}
  API:      http://localhost:${api_port}/swagger

This runs against an in-memory, pre-seeded database — no SQL Server needed.
Signing in still requires a real Entra ID tenant with your account in a
SG-LaunchPad-* group (see scripts/setup-entra.sh). Press Ctrl+C to stop both
processes.
EOF

wait
