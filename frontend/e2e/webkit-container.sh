#!/usr/bin/env bash
# WebKit (Safari's engine) on a machine where Playwright's WebKit build lacks its Ubuntu libraries (Fedora, Bazzite):
# start the same test servers as playwright.config.ts on the host, run only the WebKit tests in the official Playwright
# container (host network, so it reaches 127.0.0.1:3001 and :5081 and reuses the running servers), then stop the servers.
# Run from frontend/: npm run test:e2e:webkit [-- extra playwright arguments]
set -euo pipefail
cd "$(dirname "$0")/.."

for port in 3001 5081; do
  if ss -ltnH "sport = :$port" | grep -q .; then echo "Port $port is in use; stop that server first." >&2; exit 1; fi
done

versiune=$(node -p "require('@playwright/test/package.json').version")
podman_sock="/run/user/$(id -u)/podman/podman.sock"
export DOCKER_HOST="${DOCKER_HOST:-unix://$podman_sock}"
jurnale=$(mktemp -d)

opreste() {
  for port in 3001 5081; do
    pid=$(ss -ltnpH "sport = :$port" | grep -oP 'pid=\K[0-9]+' | head -1 || true)
    [ -n "$pid" ] && kill "$pid" 2>/dev/null || true
  done
}
trap opreste EXIT

dotnet run --project ../database/tools/MarkAi.Previzualizare -- --port 5081 --e2e > "$jurnale/api.log" 2>&1 &
MARKAI_DIST_DIR=.next-e2e API_INTERN_URL=http://127.0.0.1:5081 NEXT_PUBLIC_API_URL=/api \
  sh -c 'npx next build && npx next start -p 3001 -H 127.0.0.1' > "$jurnale/site.log" 2>&1 &

echo "Starting the test API and site (logs in $jurnale)..."
for _ in $(seq 1 120); do
  curl -sf http://127.0.0.1:5081/judete > /dev/null && curl -sf http://127.0.0.1:3001/api/judete > /dev/null && break
  sleep 5
done
curl -sf http://127.0.0.1:3001/api/judete > /dev/null || { echo "The test servers did not start; see $jurnale." >&2; exit 1; }

podman run --rm --network=host --ipc=host --userns=keep-id -v "$PWD":/w:z -w /w -e HOME=/tmp \
  "mcr.microsoft.com/playwright:v$versiune-noble" npx playwright test --project=webkit "$@"
