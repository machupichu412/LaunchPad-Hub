#!/usr/bin/env bash
set -euo pipefail

# Hits the staging slot's liveness/readiness endpoints before the CD pipeline swaps
# it into Prod — see launchpad-build-guide.md §9.3 and §11.
target="${1:?Usage: smoke-test.sh <base-url>}"

for path in /healthz /healthz/ready; do
  echo "Checking ${target}${path}..."
  status=$(curl -s -o /dev/null -w '%{http_code}' "${target}${path}")
  if [[ "$status" != "200" ]]; then
    echo "Smoke test failed: ${target}${path} returned ${status}" >&2
    exit 1
  fi
done

echo "Smoke test passed."
