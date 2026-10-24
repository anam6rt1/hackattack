import { existsSync } from "node:fs";
import { defineConfig, devices } from "@playwright/test";

// Browser tests (Chromium, Firefox and WebKit, Safari's engine) against a throwaway stack, never the real site or database:
//   the seeded test API (database/tools/MarkAi.Previzualizare --e2e) on 127.0.0.1:5081, with its own PostgreSQL container, and
//   a separate Next.js build (.next-e2e) on 127.0.0.1:3001 whose /api proxy points at that API.
// Run: npx playwright test   (both servers start and stop by themselves)
const API = "http://127.0.0.1:5081";
const SITE = "http://127.0.0.1:3001";

// Testcontainers needs the container engine's socket; on Fedora/Bazzite that is rootless podman's.
const podman = `/run/user/${process.getuid?.() ?? 1000}/podman/podman.sock`;
const dockerHost = process.env.DOCKER_HOST ?? (existsSync(podman) ? `unix://${podman}` : undefined);

export default defineConfig({
  testDir: "e2e",
  testIgnore: "upload-preview.spec.ts",
  globalSetup: "./e2e/pregatire.ts",
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  workers: process.env.CI ? 2 : 4,
  forbidOnly: !!process.env.CI,
  // A test that fails now and then is fixed, not retried until it passes (plan, Phase 6).
  retries: 0,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: SITE,
    locale: "ro-RO",
    timezoneId: "Europe/Bucharest",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } },
  ],
  webServer: [
    {
      command: "dotnet run --project ../database/tools/MarkAi.Previzualizare -- --port 5081 --e2e",
      url: `${API}/judete`,
      timeout: 300_000,
      reuseExistingServer: !process.env.CI,
      gracefulShutdown: { signal: "SIGTERM", timeout: 20_000 },
      env: dockerHost ? { DOCKER_HOST: dockerHost } : {},
      stdout: "ignore",
      stderr: "pipe",
    },
    {
      command: "npx next build && npx next start -p 3001 -H 127.0.0.1",
      url: `${SITE}/api/judete`,
      timeout: 300_000,
      reuseExistingServer: !process.env.CI,
      env: { MARKAI_DIST_DIR: ".next-e2e", API_INTERN_URL: API, NEXT_PUBLIC_API_URL: "/api" },
      stdout: "ignore",
      stderr: "pipe",
    },
  ],
});
